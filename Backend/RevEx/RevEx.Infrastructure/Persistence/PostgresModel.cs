using Microsoft.EntityFrameworkCore;
namespace RevEx.Infrastructure.Persistence;
public static class PostgresModel
{
    public static void Configure(ModelBuilder model)
    {
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
}

