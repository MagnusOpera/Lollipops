namespace Lollipops;


public record Configuration {
    public string? Source { get; init; }
    public required Package[] Packages { get; init; }
}

