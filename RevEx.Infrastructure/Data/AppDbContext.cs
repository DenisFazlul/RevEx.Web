using Microsoft.EntityFrameworkCore;
using RevEx.Domain.Entities;

namespace RevEx.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
    public DbSet<ContentFile> Files => Set<ContentFile>();
    public DbSet<ContentStatus> ContentStatuses => Set<ContentStatus>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TagGroup> TagGroups => Set<TagGroup>();

    public override int SaveChanges()
    {
        SetAuditDates();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditDates();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Content>(entity =>
        {
            entity.Property(item => item.Name).IsRequired().HasMaxLength(200);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(2000);
            entity.HasIndex(item => item.CategoryId);
            entity.HasIndex(item => item.ContentStatusId);
            entity.HasIndex(item => item.PreviewFileId).IsUnique();

            entity.HasOne(item => item.Category)
                .WithMany(item => item.Contents)
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(item => item.Versions)
                .WithOne(item => item.Content)
                .HasForeignKey(item => item.ContentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.ContentStatus)
                .WithMany(item => item.Contents)
                .HasForeignKey(item => item.ContentStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(item => item.Tags)
                .WithMany(item => item.Contents);
            entity.Navigation(item => item.Tags).AutoInclude();

            entity.HasOne(item => item.PreviewFile)
                .WithOne()
                .HasForeignKey<Content>(item => item.PreviewFileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(item => item.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(item => item.Name);
            entity.HasData(new Category
            {
                Id = 1,
                Name = "Общие",
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch
            });
        });

        modelBuilder.Entity<ContentVersion>(entity =>
        {
            entity.Property(item => item.Name).IsRequired().HasMaxLength(200);
            entity.Property(item => item.Status).IsRequired().HasMaxLength(32);
            entity.Property(item => item.Application).IsRequired().HasMaxLength(32);
            entity.Property(item => item.ApplicationVersion).IsRequired().HasMaxLength(32);
            entity.HasIndex(item => item.ContentId);
            entity.Navigation(item => item.Files).AutoInclude();
        });

        modelBuilder.Entity<ContentFile>(entity =>
        {
            entity.Property(item => item.Url).IsRequired().HasMaxLength(2048);
            entity.Property(item => item.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(item => item.Extension).IsRequired().HasMaxLength(32);
            entity.Property(item => item.Role).IsRequired().HasMaxLength(32);
            entity.HasIndex(item => item.ContentVersionId);
            entity.HasOne(item => item.ContentVersion)
                .WithMany(item => item.Files)
                .HasForeignKey(item => item.ContentVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContentStatus>(entity =>
        {
            entity.Property(item => item.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasData(new ContentStatus
            {
                Id = 1,
                Name = "draft",
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch
            });
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.Property(item => item.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => item.TagGroupId);
            entity.HasOne(item => item.TagGroup)
                .WithMany(item => item.Tags)
                .HasForeignKey(item => item.TagGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Navigation(item => item.TagGroup).AutoInclude();
        });

        modelBuilder.Entity<TagGroup>(entity =>
        {
            entity.Property(item => item.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasData(new TagGroup
            {
                Id = 1,
                Name = "Общие",
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch
            });
        });
    }

    private void SetAuditDates()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(item => item.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
