using Microsoft.AspNetCore.Mvc;
using RevEx.Workers;

namespace RevEx.Api.Middleware;

public sealed class UnexpectedErrorMiddleware(
    RequestDelegate next,
    BackendErrorQueue queue,
    IHostEnvironment environment,
    ILogger<UnexpectedErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            var error = new BackendErrorEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                context.TraceIdentifier,
                context.Request.Method,
                route,
                exception.GetType().FullName ?? exception.GetType().Name,
                environment.EnvironmentName);

            logger.LogError(exception, "Unhandled backend error {EventId} for {Method} {Route}. TraceId {TraceId}",
                error.EventId, error.Method, error.Route, error.TraceId);

            try
            {
                await queue.EnqueueAsync(error);
            }
            catch (Exception queueException) when (queueException is IOException or UnauthorizedAccessException)
            {
                logger.LogError(queueException, "Could not persist backend error event {EventId}", error.EventId);
            }

            if (context.Response.HasStarted)
            {
                context.Abort();
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problem = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = "Ошибка сервера",
                Detail = "На сервере произошёл сбой. Если проблема повторится, передайте код обращения в поддержку."
            };
            problem.Extensions["kind"] = "unexpected";
            problem.Extensions["code"] = "server.unexpected";
            problem.Extensions["traceId"] = error.TraceId;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
    }
}
