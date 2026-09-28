using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Data;

// Identity supplies the Users DbSet. Its roles tables stay unused because
// roles are per-setting (see P2-01).
public class LoreboundDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
  private readonly TimeProvider _timeProvider;

  public LoreboundDbContext(
      DbContextOptions<LoreboundDbContext> options,
      TimeProvider timeProvider)
      : base(options)
  {
    _timeProvider = timeProvider;
  }

  public DbSet<CampaignSetting> CampaignSettings => Set<CampaignSetting>();

  public DbSet<SettingEntry> SettingEntries => Set<SettingEntry>();

  public DbSet<SettingEntryRelationship> SettingEntryRelationships
      => Set<SettingEntryRelationship>();

  protected override void ConfigureConventions(
      ModelConfigurationBuilder configurationBuilder)
  {
    // Every enum column is stored as its name, including enums added later.
    configurationBuilder.Properties<Enum>().HaveConversion<string>();
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<ApplicationUser>(user =>
    {
      user.Property(u => u.DisplayName)
          .IsRequired()
          .HasMaxLength(60);

      // Identity's EmailIndex is non-unique; RequireUniqueEmail is only an
      // app-level check, so enforce it in the database too.
      user.HasIndex(u => u.NormalizedEmail)
          .HasDatabaseName("EmailIndex")
          .IsUnique();

      // A user who owns settings cannot be hard-deleted (see P6-11).
      user.HasMany(u => u.CampaignSettings)
          .WithOne(setting => setting.Owner)
          .HasForeignKey(setting => setting.OwnerUserId)
          .OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<CampaignSetting>()
        .HasMany(setting => setting.Entries)
        .WithOne(entry => entry.CampaignSetting)
        .HasForeignKey(entry => entry.CampaignSettingId);

    modelBuilder.Entity<CampaignSetting>()
        .HasMany(setting => setting.Relationships)
        .WithOne(relationship => relationship.CampaignSetting)
        .HasForeignKey(relationship => relationship.CampaignSettingId);

    modelBuilder.Entity<SettingEntryRelationship>()
        .HasOne(relationship => relationship.SourceEntry)
        .WithMany(entry => entry.OutgoingRelationships)
        .HasForeignKey(relationship => relationship.SourceEntryId)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<SettingEntryRelationship>()
        .HasOne(relationship => relationship.TargetEntry)
        .WithMany(entry => entry.IncomingRelationships)
        .HasForeignKey(relationship => relationship.TargetEntryId)
        .OnDelete(DeleteBehavior.Restrict);
  }

  // ExecuteUpdate/ExecuteDelete bypass these overrides; bulk updates must
  // set UpdatedAt explicitly.
  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    ApplyTimestamps();
    return base.SaveChanges(acceptAllChangesOnSuccess);
  }

  public override Task<int> SaveChangesAsync(
      bool acceptAllChangesOnSuccess,
      CancellationToken cancellationToken = default)
  {
    ApplyTimestamps();
    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
  }

  private void ApplyTimestamps()
  {
    var now = _timeProvider.GetUtcNow();

    foreach (var entry in ChangeTracker.Entries<ICreatedAt>())
    {
      if (entry.State == EntityState.Added)
      {
        entry.Entity.CreatedAt = now;
      }
      else if (entry.State == EntityState.Modified)
      {
        // CreatedAt is write-once.
        entry.Property(e => e.CreatedAt).IsModified = false;
      }

      if (entry.Entity is ITimestamped timestamped
          && entry.State is EntityState.Added or EntityState.Modified)
      {
        timestamped.UpdatedAt = now;
      }
    }
  }
}