using Microsoft.EntityFrameworkCore;
using RevEx.Domain.Entities;

namespace RevEx.Infrastructure.Persistence;

public static class PostgresModel
{
    public static void Configure(ModelBuilder model)
    {
        ConfigureCatalog(model);
        model.Entity<ContentHistoryEvent>().ToTable("content_history_events");
        model.Entity<ContentHistoryEvent>().Property(x => x.Id).UseIdentityAlwaysColumn();
        model.Entity<ContentHistoryEvent>().Property(x => x.Author).HasMaxLength(300);
        model.Entity<ContentHistoryEvent>().Property(x => x.OccurredAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        model.Entity<ContentHistoryEvent>().HasIndex(x => new { x.ContentId, x.OccurredAt, x.Id }).IsDescending(false,true,true).HasDatabaseName("ix_content_history_events_content_time");
        model.Entity<ContentSubscription>().ToTable("content_subscriptions");
        model.Entity<ContentSubscription>().Property(x => x.Id).UseIdentityAlwaysColumn();
        model.Entity<ContentSubscription>().Property(x => x.SubscribedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        model.Entity<ContentSubscription>().HasIndex(x => new { x.ContentId, x.UserId }).IsUnique();
        model.Entity<ContentNotificationDelivery>().ToTable("content_notification_deliveries").HasKey(x => new { x.EventId, x.UserId });
        model.Entity<ContentNotificationDelivery>().Property(x => x.DeliveredAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        model.Entity<OutboxEntry>().ToTable("kafka_outbox");
        model.Entity<OutboxEntry>().Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        model.Entity<OutboxEntry>().Property(x => x.NextAttemptAt).HasDefaultValueSql("now()");
        model.Entity<OutboxEntry>().Property(x => x.Attempts).HasDefaultValue(0);
        model.Entity<OutboxEntry>().HasIndex(x => x.NextAttemptAt).HasDatabaseName("ix_kafka_outbox_pending");
        model.Entity<DesktopInstallation>().ToTable("desktop_installations").HasKey(x => x.InstallationId);
        model.Entity<DesktopInstallation>().Property(x => x.KeyHash).HasMaxLength(64);
        model.Entity<DesktopInstallation>().Property(x => x.LastSeenAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        model.Entity<DesktopInstallation>().HasIndex(x => x.UserId).HasDatabaseName("ix_desktop_installations_user");
        model.Entity<DesktopOffer>().ToTable("desktop_update_offers");
        model.Entity<DesktopOffer>().Property(x => x.Policy).HasColumnType("jsonb");
        model.Entity<DesktopOffer>().Property(x => x.OfferedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        model.Entity<DesktopOffer>().HasOne<DesktopInstallation>().WithMany().HasForeignKey(x => x.InstallationId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DesktopOffer>().HasIndex(x => new { x.InstallationId,x.OfferedAt }).IsDescending(false,true).HasDatabaseName("ix_desktop_update_offers_installation");
        model.Entity<DesktopDownload>().ToTable("desktop_downloads");
        model.Entity<DesktopDownload>().Property(x => x.StartedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        model.Entity<DesktopDownload>().Property(x => x.Status).HasDefaultValue("started");
        model.Entity<DesktopDownload>().Property(x => x.FileName).HasMaxLength(180);
        model.Entity<DesktopDownload>().HasOne<DesktopInstallation>().WithMany().HasForeignKey(x => x.InstallationId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DesktopDownload>().HasOne<DesktopOffer>().WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<DesktopDownload>().HasIndex(x => x.StartedAt).IsDescending().HasDatabaseName("ix_desktop_downloads_time");
        model.Entity<RevEx.Domain.Entities.CommentSet>().ToTable("comment_sets", t => t.HasComment("Наборы комментариев: отдельные обсуждения."));
        model.Entity<RevEx.Domain.Entities.CommentSet>().Property(x => x.Name).HasMaxLength(200).HasComment("Название набора комментариев.");
        model.Entity<RevEx.Domain.Entities.CommentSet>().Property(x => x.AuthorId).HasComment("Неизменяемый идентификатор создателя из токена.");
        model.Entity<RevEx.Domain.Entities.Comment>().ToTable("comments", t => t.HasComment("Комментарии, принадлежащие одному набору."));
        model.Entity<RevEx.Domain.Entities.Comment>().Property(x => x.Text).HasMaxLength(10000).HasComment("Текст комментария.");
        model.Entity<RevEx.Domain.Entities.Comment>().Property(x => x.AuthorId).HasComment("Неизменяемый идентификатор автора из токена.");
        model.Entity<RevEx.Domain.Entities.Comment>().Property(x => x.Author).HasComment("Отображаемое имя автора на момент создания.");
        model.Entity<RevEx.Domain.Entities.Comment>().Property(x => x.CreatedAt).HasComment("Время создания в UTC.");
        model.Entity<RevEx.Domain.Entities.Comment>().Property(x => x.UpdatedAt).HasComment("Время последнего изменения в UTC; null до первого редактирования.");
        model.Entity<RevEx.Domain.Entities.Comment>().HasOne(x => x.CommentSet).WithMany(x => x.Comments).HasForeignKey(x => x.CommentSetId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<RevEx.Domain.Entities.Comment>().HasIndex(x => new { x.CommentSetId,x.CreatedAt,x.Id });
        model.Entity<RevEx.Domain.Entities.CommentSet>().HasIndex(x => new { x.CreatedAt,x.Id });
        foreach(var entity in model.Model.GetEntityTypes()) foreach(var property in entity.GetProperties())
        {
            var name = System.Text.RegularExpressions.Regex.Replace(property.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant();
            property.SetColumnName(name);
            if (entity.ClrType.Name.StartsWith("Desktop") && property.ClrType == typeof(string))
            {
                if(property.Name is "UserId" or "UserName") property.SetMaxLength(300);
                if(property.Name is "Platform" or "Version" or "FromVersion" or "Status") property.SetMaxLength(32);
            }
        }
    }

    private static void ConfigureCatalog(ModelBuilder model)
    {
        model.Entity<Category>().ToTable("categories");
        model.Entity<Category>().Property(entity => entity.Id).UseIdentityByDefaultColumn();
        model.Entity<Category>().Property(entity => entity.Name).HasMaxLength(300);

        model.Entity<ContentStatus>().ToTable("content_statuses");
        model.Entity<ContentStatus>().Property(entity => entity.Id).UseIdentityByDefaultColumn();
        model.Entity<ContentStatus>().Property(entity => entity.Name).HasMaxLength(100);

        model.Entity<TagGroup>().ToTable("tag_groups");
        model.Entity<TagGroup>().Property(entity => entity.Id).UseIdentityByDefaultColumn();
        model.Entity<TagGroup>().Property(entity => entity.Name).HasMaxLength(300);

        model.Entity<Tag>().ToTable("tags");
        model.Entity<Tag>().Property(entity => entity.Id).UseIdentityByDefaultColumn();
        model.Entity<Tag>().Property(entity => entity.Name).HasMaxLength(300);
        model.Entity<Tag>()
            .HasOne(entity => entity.TagGroup)
            .WithMany(entity => entity.Tags)
            .HasForeignKey(entity => entity.TagGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<Content>().ToTable("contents");
        model.Entity<Content>().Property(entity => entity.Id).UseIdentityByDefaultColumn();
        model.Entity<Content>().Property(entity => entity.Name).HasMaxLength(500);
        model.Entity<Content>().Ignore(entity => entity.TagIds);
        model.Entity<Content>().HasIndex(entity => entity.Cid).IsUnique();
        model.Entity<Content>()
            .HasOne(entity => entity.Category)
            .WithMany(entity => entity.Contents)
            .HasForeignKey(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<Content>()
            .HasOne(entity => entity.ContentStatus)
            .WithMany(entity => entity.Contents)
            .HasForeignKey(entity => entity.ContentStatusId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<Content>()
            .HasOne(entity => entity.PreviewFile)
            .WithMany()
            .HasForeignKey(entity => entity.PreviewFileId)
            .OnDelete(DeleteBehavior.SetNull);
        model.Entity<Content>()
            .HasMany(entity => entity.Tags)
            .WithMany(entity => entity.Contents)
            .UsingEntity(join => join.ToTable("content_tags"));

        model.Entity<ContentVersion>().ToTable("content_versions");
        model.Entity<ContentVersion>().Property(entity => entity.Id).UseIdentityByDefaultColumn();
        model.Entity<ContentVersion>().Property(entity => entity.Name).HasMaxLength(100);
        model.Entity<ContentVersion>().Property(entity => entity.Status).HasMaxLength(100);
        model.Entity<ContentVersion>().Property(entity => entity.Application).HasMaxLength(100);
        model.Entity<ContentVersion>().Property(entity => entity.ApplicationVersion).HasMaxLength(100);
        model.Entity<ContentVersion>().Property(entity => entity.Platform).HasConversion<int>();
        model.Entity<ContentVersion>().Property(entity => entity.PublishStatus).HasConversion<int>();
        model.Entity<ContentVersion>().HasIndex(entity => entity.Vid).IsUnique();
        model.Entity<ContentVersion>()
            .HasOne(entity => entity.Content)
            .WithMany(entity => entity.Versions)
            .HasForeignKey(entity => entity.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        model.Entity<ContentFile>().ToTable("content_files");
        model.Entity<ContentFile>().Property(entity => entity.Id).UseIdentityByDefaultColumn();
        model.Entity<ContentFile>().Property(entity => entity.OriginalFileName).HasMaxLength(500);
        model.Entity<ContentFile>().Property(entity => entity.Extension).HasMaxLength(50);
        model.Entity<ContentFile>().Property(entity => entity.Role).HasMaxLength(100);
        model.Entity<ContentFile>().Ignore(entity => entity.MetadataJson);
        model.Entity<ContentFile>()
            .HasOne(entity => entity.ContentVersion)
            .WithMany(entity => entity.Files)
            .HasForeignKey(entity => entity.ContentVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        model.Entity<ContentFileMetadata>().ToTable("content_file_metadata");
        model.Entity<ContentFileMetadata>().Property(entity => entity.Id).UseIdentityByDefaultColumn();
        model.Entity<ContentFileMetadata>().Property(entity => entity.MetadataJson).HasColumnType("jsonb");
        model.Entity<ContentFileMetadata>().HasIndex(entity => entity.FileId).IsUnique();
        model.Entity<ContentFileMetadata>()
            .HasOne<ContentFile>()
            .WithOne()
            .HasForeignKey<ContentFileMetadata>(entity => entity.FileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

