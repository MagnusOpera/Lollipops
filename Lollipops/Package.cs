namespace MagnusOpera.Lollipops;

public record Package {
    public required string Id { get; init; }
    public string? Version { get; init; }
    public bool PreRelease { get; init; }
}
