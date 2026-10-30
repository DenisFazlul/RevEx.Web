# Email notifications

Inject NotificationService.INotificationService into backend services and call
SendAsync(new EmailNotification(to, subject, body, Cc: copies), cancellationToken).
Recipients and copies are collections of email addresses. HTML is opt-in via IsHtml.
Errors and cancellation propagate to the caller. There is no queue or automatic retry.

Configure NotificationService in appsettings.json or SMTP_* variables in .env for
Docker Compose; see .env.example. Set the real host, sender and credentials before use.
EnableSsl uses STARTTLS (implicit TLS on port 465 is unsupported).
Blank username selects unauthenticated SMTP. Never commit .env or passwords.
