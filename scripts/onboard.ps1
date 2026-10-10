<#
.SYNOPSIS
    Gets a developer from a fresh clone to a migrated local database.

.DESCRIPTION
    Run from anywhere in the repository:

        powershell -ExecutionPolicy Bypass -File .\scripts\onboard.ps1

    Safe to re-run at any time: it only creates what is missing and never
    overwrites .env or an existing connection-string user secret. Run it again
    after pulling changes that add a migration.

.PARAMETER SkipDbUpdate
    Skip applying EF Core migrations.
#>
[CmdletBinding()]
param(
    [switch]$SkipDbUpdate
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ApiProject = Join-Path $RepoRoot 'api'
$EnvFile = Join-Path $RepoRoot '.env'
$EnvExample = Join-Path $RepoRoot '.env.example'
$ContainerName = 'lorebound-postgres'
$SecretKey = 'ConnectionStrings:DefaultConnection'
$TotalSteps = 7
$PasswordHelp = 'See "Changing the database password" in README.md.'

function Write-Step([int]$Number, [string]$Message) {
    Write-Host ''
    Write-Host "Step $Number/$TotalSteps`: $Message" -ForegroundColor Cyan
}

function Write-Ok([string]$Message) {
    Write-Host "  OK  $Message" -ForegroundColor Green
}

function Write-Info([string]$Message) {
    Write-Host "      $Message"
}

function Stop-Onboarding([string]$Message, [string]$Hint) {
    Write-Host ''
    Write-Host "  FAILED  $Message" -ForegroundColor Red
    if ($Hint) {
        Write-Host "  $Hint" -ForegroundColor Yellow
    }
    exit 1
}

# Runs a native command, echoes its output only on failure, and returns it.
function Invoke-Native([string]$FailureMessage, [scriptblock]$Command, [string]$Hint) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & $Command 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    $ErrorActionPreference = $previous

    if ($code -ne 0) {
        $output | Select-Object -Last 15 | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
        Stop-Onboarding $FailureMessage $Hint
    }

    return $output
}

function Read-EnvFile([string]$Path) {
    $values = @{}
    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) {
            continue
        }
        $separator = $trimmed.IndexOf('=')
        if ($separator -gt 0) {
            $values[$trimmed.Substring(0, $separator).Trim()] = $trimmed.Substring($separator + 1).Trim()
        }
    }
    return $values
}

function Get-EnvValue([hashtable]$Values, [string]$Key, [string]$Default) {
    if ($Values.ContainsKey($Key) -and $Values[$Key]) {
        return $Values[$Key]
    }
    return $Default
}

Write-Host 'Lorebound local onboarding' -ForegroundColor White
Write-Host "Repository: $RepoRoot"

# 1. Prerequisites -----------------------------------------------------------
Write-Step 1 'Checking prerequisites (.NET SDK 10, Docker Desktop)'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Stop-Onboarding '.NET SDK not found.' 'Install the .NET 10 SDK (see Prerequisites in README.md) and open a new terminal.'
}
$sdks = Invoke-Native 'Could not list .NET SDKs.' { dotnet --list-sdks }
$sdk10 = $sdks | Where-Object { $_ -match '^10\.' } | Select-Object -Last 1
if (-not $sdk10) {
    Stop-Onboarding '.NET SDK 10.x is not installed.' 'Install the .NET 10 SDK (see Prerequisites in README.md).'
}
Write-Ok ".NET SDK $(($sdk10 -split ' ')[0])"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Stop-Onboarding 'Docker not found.' 'Install Docker Desktop (see Prerequisites in README.md).'
}
Invoke-Native 'Docker Desktop is not running.' { docker info --format '{{.ServerVersion}}' } 'Start Docker Desktop, wait until it says "Engine running", then re-run this script.' | Out-Null
Write-Ok 'Docker Desktop is running'

# 2. .env ----------------------------------------------------------------------
Write-Step 2 'Checking .env (local database credentials)'

if (Test-Path $EnvFile) {
    Write-Ok '.env already exists (left unchanged)'
} else {
    if (-not (Test-Path $EnvExample)) {
        Stop-Onboarding '.env.example is missing.' 'Pull the latest dev branch.'
    }
    $password = [guid]::NewGuid().ToString('N')
    $content = (Get-Content -Path $EnvExample) -replace '^POSTGRES_PASSWORD=.*$', "POSTGRES_PASSWORD=$password"
    Set-Content -Path $EnvFile -Value $content -Encoding ASCII
    Write-Ok 'Created .env from .env.example with a random password (git-ignored; never commit it)'
}

$envValues = Read-EnvFile $EnvFile
$dbName = Get-EnvValue $envValues 'POSTGRES_DB' 'lorebound'
$dbUser = Get-EnvValue $envValues 'POSTGRES_USER' 'lorebound'
$dbPassword = Get-EnvValue $envValues 'POSTGRES_PASSWORD' ''
$dbPort = Get-EnvValue $envValues 'POSTGRES_PORT' '5432'
if (-not $dbPassword) {
    Stop-Onboarding 'POSTGRES_PASSWORD is empty in .env.' 'Set a password in .env, then re-run this script.'
}

# 3. Database container ------------------------------------------------------
Write-Step 3 'Starting the PostgreSQL container (docker compose up -d)'

Push-Location $RepoRoot
try {
    Invoke-Native 'docker compose up failed.' { docker compose up -d } "If port $dbPort is in use, stop the other PostgreSQL or change POSTGRES_PORT in .env." | Out-Null
} finally {
    Pop-Location
}

$deadline = (Get-Date).AddSeconds(90)
$health = ''
while ((Get-Date) -lt $deadline) {
    $health = (& docker inspect -f '{{.State.Health.Status}}' $ContainerName 2>$null | Out-String).Trim()
    if ($health -eq 'healthy') {
        break
    }
    Start-Sleep -Seconds 2
}
if ($health -ne 'healthy') {
    Stop-Onboarding "Container $ContainerName did not become healthy (status: '$health')." 'Check its logs with: docker compose logs postgres'
}
Write-Ok "$ContainerName is healthy on port $dbPort"

# 4. Tools and packages ------------------------------------------------------
Write-Step 4 'Restoring .NET tools and NuGet packages'

Push-Location $RepoRoot
try {
    Invoke-Native 'dotnet tool restore failed.' { dotnet tool restore } | Out-Null
    Write-Ok 'dotnet-ef restored from dotnet-tools.json'
    Invoke-Native 'dotnet restore failed.' { dotnet restore Lorebound.slnx } | Out-Null
    Write-Ok 'NuGet packages restored'
} finally {
    Pop-Location
}

# 5. Connection string -------------------------------------------------------
Write-Step 5 'Checking the API connection string (user secrets)'

$secrets = Invoke-Native 'Could not read API user secrets.' { dotnet user-secrets list --project $ApiProject }
$existing = $secrets | Where-Object { $_ -like "$SecretKey = *" } | Select-Object -First 1
if ($existing) {
    Write-Ok "$SecretKey is already set (left unchanged)"
    if ($existing -notlike "*Password=$dbPassword*") {
        Write-Host "      Warning: its password does not match POSTGRES_PASSWORD in .env." -ForegroundColor Yellow
        Write-Host "      $PasswordHelp" -ForegroundColor Yellow
    }
} else {
    $connection = "Host=localhost;Port=$dbPort;Database=$dbName;Username=$dbUser;Password=$dbPassword"
    Invoke-Native 'Could not set the connection-string user secret.' { dotnet user-secrets set $SecretKey $connection --project $ApiProject } | Out-Null
    Write-Ok "$SecretKey set from .env"
}

# 6. Migrations --------------------------------------------------------------
Write-Step 6 'Applying database migrations (dotnet ef database update)'

if ($SkipDbUpdate) {
    Write-Info 'Skipped (-SkipDbUpdate).'
} else {
    Push-Location $ApiProject
    try {
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $efOutput = & dotnet ef database update 2>&1 | ForEach-Object { "$_" }
        $efCode = $LASTEXITCODE
        $ErrorActionPreference = $previous
    } finally {
        Pop-Location
    }

    if ($efCode -ne 0) {
        $efOutput | Select-Object -Last 15 | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
        if ($efOutput -match 'password authentication failed') {
            Stop-Onboarding 'The database rejected the password in your user secret.' $PasswordHelp
        }
        Stop-Onboarding 'dotnet ef database update failed.' 'See the output above.'
    }
    Write-Ok 'Database is up to date'
}

# 7. Summary -----------------------------------------------------------------
Write-Step 7 'Done'
Write-Host ''
Write-Host 'Your local environment is ready.' -ForegroundColor Green
Write-Host '  Run the API:      cd api; dotnet run      then open http://localhost:5110/api/health'
Write-Host '  Add sample data:  cd api; dotnet run -- --seed   (logins in README "Seed data")'
Write-Host '  Run the frontend: cd frontend; npm install; npm run dev'
Write-Host '  Run the tests:    dotnet test Lorebound.slnx   (Docker Desktop must be running)'
Write-Host '  Re-run this script after pulling changes that add a migration.'
exit 0
