namespace ResourcePulse.Common.Auth;

public sealed record CurrentUser(
    string Sub,
    string Email,
    string Name,
    IReadOnlyDictionary<string, string> Claims)
{
    public static readonly CurrentUser Anonymous = new(
        string.Empty,
        string.Empty,
        string.Empty,
        new Dictionary<string, string>());
}
