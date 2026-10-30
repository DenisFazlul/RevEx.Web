namespace RevEx.Application.Abstractions.Identity;

public interface ICurrentUser
{
    string Author { get; }
}
