using RevEx.Application.Subscriptions;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RevEx.Infrastructure.Notifications;

public sealed record EmailNotification(IReadOnlyCollection<string> To, string Subject, string Body,
    IReadOnlyCollection<string>? Cc = null, bool IsHtml = false);
