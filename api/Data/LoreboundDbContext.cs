using Lorebound.Api.Models;
using Lorebound.Api.Sharing;
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

  public DbSet<SettingMembership> SettingMemberships
    => Set<SettingMembership>();

  public DbSet<SettingEntryRelationship> SettingEntryRelationships
      => Set<SettingEntryRelationship>();

  public DbSet<SettingInvite> SettingInvites => Set<SettingInvite>();

  public DbSet<Character> Characters => Set<Character>();

  public DbSet<CharacterChoice> CharacterChoices => Set<CharacterChoice>();

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

    // Case-insensitive text for entry names (P4-01).
    modelBuilder.HasPostgresExtension("citext");

    modelBuilder.Entity<SettingEntry>(entry =>
    {
      entry.Property(e => e.Name)
          .IsRequired()
          .HasColumnType("citext");

      // "Aster" and "aster" of the same type in one setting are duplicates.
      entry.HasIndex(e => new { e.CampaignSettingId, e.EntryType, e.Name })
          .IsUnique();

      // The list endpoint filters by type and the visibility filter by
      // IsGmOnly, both within one setting.
      entry.HasIndex(e => new { e.CampaignSettingId, e.EntryType });

      entry.HasIndex(e => new { e.CampaignSettingId, e.IsGmOnly });
    });

    modelBuilder.Entity<CampaignSetting>()
        .HasMany(setting => setting.Entries)
        .WithOne(entry => entry.CampaignSetting)
        .HasForeignKey(entry => entry.CampaignSettingId);

    modelBuilder.Entity<CampaignSetting>()
        .HasMany(setting => setting.Relationships)
        .WithOne(relationship => relationship.CampaignSetting)
        .HasForeignKey(relationship => relationship.CampaignSettingId);

    modelBuilder.Entity<SettingEntryRelationship>(relationship =>
    {
      relationship
          .HasOne(r => r.SourceEntry)
          .WithMany(entry => entry.OutgoingRelationships)
          .HasForeignKey(r => r.SourceEntryId)
          .OnDelete(DeleteBehavior.NoAction);

      relationship
          .HasOne(r => r.TargetEntry)
          .WithMany(entry => entry.IncomingRelationships)
          .HasForeignKey(r => r.TargetEntryId)
          .OnDelete(DeleteBehavior.NoAction);

      // Readable labels compared ignoring case (P5-05, ADR 0002). citext has
      // no length, so a check constraint keeps the 60-character limit.
      relationship.Property(r => r.RelationshipType)
          .IsRequired()
          .HasColumnType("citext");

      relationship.Property(r => r.Description)
          .HasMaxLength(1000);

      // The same link may exist once per type (P5-01). This index also
      // serves lookups by SourceEntryId, so EF drops the single-column one.
      relationship
          .HasIndex(r => new { r.SourceEntryId, r.TargetEntryId, r.RelationshipType })
          .HasDatabaseName("IX_SettingEntryRelationships_Source_Target_Type")
          .IsUnique();

      relationship.HasIndex(r => r.TargetEntryId);

      relationship.HasIndex(r => r.CampaignSettingId);

      // An entry cannot relate to itself. Both endpoints belonging to the
      // relationship's setting is a cross-table rule, checked in P5-03.
      relationship.ToTable(table =>
      {
        table.HasCheckConstraint(
            "CK_SettingEntryRelationships_NoSelfLink",
            "\"SourceEntryId\" <> \"TargetEntryId\"");

        table.HasCheckConstraint(
            "CK_SettingEntryRelationships_RelationshipTypeLength",
            $"char_length(\"RelationshipType\") <= {RelationshipTypes.MaxLength}");
      });
    });

    modelBuilder.Entity<SettingMembership>(membership =>
    {
      membership
          .HasOne(m => m.CampaignSetting)
          .WithMany(setting => setting.Memberships)
          .HasForeignKey(m => m.CampaignSettingId)
          .OnDelete(DeleteBehavior.Cascade);

      membership
          .HasOne(m => m.User)
          .WithMany(user => user.SettingMemberships)
          .HasForeignKey(m => m.UserId)
          .OnDelete(DeleteBehavior.Restrict);

      membership
          .HasIndex(m => new
          {
            m.CampaignSettingId,
            m.UserId
          })
          .IsUnique();
    });

    modelBuilder.Entity<SettingInvite>(invite =>
    {
      invite.Property(i => i.Code)
          .IsRequired()
          .HasMaxLength(InviteCodes.Length);

      invite.HasIndex(i => i.Code).IsUnique();

      invite.HasIndex(i => i.CampaignSettingId);

      invite
          .HasOne(i => i.CampaignSetting)
          .WithMany(setting => setting.Invites)
          .HasForeignKey(i => i.CampaignSettingId)
          .OnDelete(DeleteBehavior.Cascade);

      // Like memberships, a user who created invites cannot be hard-deleted
      // (see P6-11).
      invite
          .HasOne(i => i.CreatedBy)
          .WithMany()
          .HasForeignKey(i => i.CreatedByUserId)
          .OnDelete(DeleteBehavior.Restrict);

      // Accepting increments UseCount with a bulk update (P3-05); the
      // database refuses one that would go past MaxUses.
      invite.ToTable(table =>
      {
        table.HasCheckConstraint(
            "CK_SettingInvites_UseCount",
            "\"UseCount\" >= 0 AND (\"MaxUses\" IS NULL OR \"UseCount\" <= \"MaxUses\")");
        table.HasCheckConstraint(
            "CK_SettingInvites_MaxUses",
            "\"MaxUses\" IS NULL OR \"MaxUses\" > 0");
      });
    });

    modelBuilder.Entity<Character>(character =>
    {
      character.Property(c => c.Name)
          .IsRequired()
          .HasMaxLength(Character.NameMaxLength);

      character.Property(c => c.Backstory)
          .HasMaxLength(Character.BackstoryMaxLength);

      // Deleting a setting deletes its characters (P6-01).
      character
          .HasOne(c => c.CampaignSetting)
          .WithMany(setting => setting.Characters)
          .HasForeignKey(c => c.CampaignSettingId)
          .OnDelete(DeleteBehavior.Cascade);

      // A user who owns characters cannot be hard-deleted; account deletion
      // removes them first (P6-11).
      character
          .HasOne(c => c.Owner)
          .WithMany()
          .HasForeignKey(c => c.OwnerUserId)
          .OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<CharacterChoice>(choice =>
    {
      choice.Property(c => c.StepKey)
          .IsRequired()
          .HasMaxLength(CharacterChoice.StepKeyMaxLength);

      choice.Property(c => c.FreeText)
          .HasMaxLength(CharacterChoice.FreeTextMaxLength);

      choice
          .HasOne(c => c.Character)
          .WithMany(character => character.Choices)
          .HasForeignKey(c => c.CharacterId)
          .OnDelete(DeleteBehavior.Cascade);

      // Deleting an entry keeps the character; the choice just loses its
      // entry (P6-09).
      choice
          .HasOne(c => c.Entry)
          .WithMany()
          .HasForeignKey(c => c.EntryId)
          .OnDelete(DeleteBehavior.SetNull);

      // One answer per (step, ordinal). This index also serves lookups by
      // CharacterId, so EF drops the single-column one.
      choice
          .HasIndex(c => new { c.CharacterId, c.StepKey, c.Ordinal })
          .IsUnique();

      choice.HasIndex(c => c.EntryId);

      // A choice is an entry or free text, never both. "Neither" is refused
      // when a choice is saved (P7-03), not here: a deleted entry leaves a
      // choice with both null (P6-09), which a stricter check would block.
      choice.ToTable(table =>
      {
        table.HasCheckConstraint(
            "CK_CharacterChoices_EntryOrFreeText",
            "\"EntryId\" IS NULL OR \"FreeText\" IS NULL");
      });
    });
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

    foreach (var entry in ChangeTracker.Entries<SettingMembership>())
    {
      if (entry.State == EntityState.Added && entry.Entity.JoinedAt == default)
      {
        entry.Entity.JoinedAt = now;
      }
    }
  }
}