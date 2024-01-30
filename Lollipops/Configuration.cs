namespace MagnusOpera.Lollipops;


public record Configuration {
    public string? Source { get; init; }
    public bool Debug { get; init; }
    public required Package[] Packages { get; init; }
}
