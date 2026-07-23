namespace SymphonyTest1.Api.Features.Greetings;

public record Greeting
{
    public Guid Id { get; init; }
    public Guid LanguageId { get; init; }
    public string GreetingText { get; init; } = string.Empty;
    public bool Formal { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateGreetingRequest(Guid LanguageId, string GreetingText, bool Formal);
public record UpdateGreetingRequest(Guid LanguageId, string GreetingText, bool Formal);
public record GreetingResponse(Guid Id, Guid LanguageId, string GreetingText, bool Formal, DateTime CreatedAt, DateTime UpdatedAt);
public record GreetingByLanguageResponse(string Language, string LanguageCode, string GreetingText, bool Formal);
