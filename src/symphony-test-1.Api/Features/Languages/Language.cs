namespace SymphonyTest1.Api.Features.Languages;

public record Language
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateLanguageRequest(string Name, string Code);
public record UpdateLanguageRequest(string Name, string Code);
public record LanguageResponse(Guid Id, string Name, string Code, DateTime CreatedAt, DateTime UpdatedAt);
