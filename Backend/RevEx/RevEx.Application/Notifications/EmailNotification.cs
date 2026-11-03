namespace RevEx.Application.Notifications;

public sealed record EmailNotification(IReadOnlyCollection<string> To, string Subject, string Body,
    IReadOnlyCollection<string>? Cc = null, bool IsHtml = false);
