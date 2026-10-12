# The API image for Render (P8-10, ADR 0003). Build context: this folder.
#   docker build -t lorebound-api api
# Configuration comes from environment variables at run time (see ADR 0003,
# "Configuration"); nothing secret is baked into the image.

# Build: restore first, so the package layer is cached until the project
# file changes.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Lorebound.Api.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish --configuration Release --no-restore --output /app /p:UseAppHost=false

# Run: the ASP.NET runtime only, as the image's non-root user. The image
# listens on 8080 (ASPNETCORE_HTTP_PORTS), the port Render routes to.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Lorebound.Api.dll"]
