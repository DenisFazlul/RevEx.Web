namespace RevEx.Application.DTOs.Category;

public sealed record CategoryDto(int Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
