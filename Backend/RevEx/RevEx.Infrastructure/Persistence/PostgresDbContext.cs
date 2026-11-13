using Microsoft.EntityFrameworkCore;

namespace RevEx.Infrastructure.Persistence;

public sealed class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
{
    public DbSet<RevEx.Domain.Entities.Category> Categories => Set<RevEx.Domain.Entities.Category>();

    public DbSet<RevEx.Domain.Entities.Content> Contents => Set<RevEx.Domain.Entities.Content>();

    public DbSet<RevEx.Domain.Entities.ContentVersion> ContentVersions => Set<RevEx.Domain.Entities.ContentVersion>();

    public DbSet<RevEx.Domain.Entities.ContentFile> ContentFiles => Set<RevEx.Domain.Entities.ContentFile>();

    public DbSet<RevEx.Domain.Entities.ContentFileMetadata> ContentFileMetadata => Set<RevEx.Domain.Entities.ContentFileMetadata>();

    public DbSet<RevEx.Domain.Entities.ContentStatus> ContentStatuses => Set<RevEx.Domain.Entities.ContentStatus>();

    public DbSet<RevEx.Domain.Entities.Tag> Tags => Set<RevEx.Domain.Entities.Tag>();

    public DbSet<RevEx.Domain.Entities.TagGroup> TagGroups => Set<RevEx.Domain.Entities.TagGroup>();

    /// <summary>История изменений контента: описание события, автор и время изменения.</summary>
    public DbSet<ContentHistoryEvent> History => Set<ContentHistoryEvent>();

    /// <summary>Подписки пользователей на изменения контента с адресом электронной почты для уведомлений.</summary>
    public DbSet<ContentSubscription> ContentSubscriptions => Set<ContentSubscription>();

    /// <summary>Успешные доставки уведомлений пользователям; предотвращают повторную отправку одного события.</summary>
    public DbSet<ContentNotificationDelivery> ContentNotificationDeliveries => Set<ContentNotificationDelivery>();

    /// <summary>Очередь сообщений для отправки в Kafka с числом попыток, временем повтора и последней ошибкой.</summary>
    public DbSet<OutboxEntry> KafkaOutbox => Set<OutboxEntry>();

    /// <summary>Установки Desktop: идентификатор и хеш ключа установки, платформа, текущая версия, пользователь и время последней активности.</summary>
    public DbSet<DesktopInstallation> DesktopInstallations => Set<DesktopInstallation>();

    /// <summary>Предложения обновления для конкретных установок Desktop, назначенный пакет, подтверждение установки и ошибка обновления.</summary>
    public DbSet<DesktopOffer> DesktopUpdateOffers => Set<DesktopOffer>();

    /// <summary>История загрузок пакетов Desktop: файл, версия, установка, пользователь, время начала и завершения и результат передачи.</summary>
    public DbSet<DesktopDownload> DesktopDownloads => Set<DesktopDownload>();

    /// <summary>Наборы комментариев: название обсуждения, создатель и дата создания.</summary>
    public DbSet<RevEx.Domain.Entities.CommentSet> CommentSets => Set<RevEx.Domain.Entities.CommentSet>();

    /// <summary>Комментарии внутри наборов: текст, автор, дата создания и последнего изменения.</summary>
    public DbSet<RevEx.Domain.Entities.Comment> Comments => Set<RevEx.Domain.Entities.Comment>();

    protected override void OnModelCreating(ModelBuilder model) => PostgresModel.Configure(model);
}

