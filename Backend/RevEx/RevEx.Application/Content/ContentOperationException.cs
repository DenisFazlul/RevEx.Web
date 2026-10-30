
namespace RevEx.Application.Content;

public sealed class ContentOperationException(ContentFailure failure, string? message = null) : Exception(message)
{
    public ContentFailure Failure { get; } = failure;
    public string? Detail { get; } = message;
}
