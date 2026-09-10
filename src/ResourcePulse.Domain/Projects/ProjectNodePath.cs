namespace ResourcePulse.Domain.Projects;

/// <summary>
/// Reading the materialized path (<see href="ADR-0001"/>): <c>"/{rootId}/{childId}/…"</c>.
/// </summary>
/// <remarks>
/// The shape of the path is a domain rule, so the one place that decodes it lives
/// with the aggregate rather than being re-derived by every caller that needs a
/// root id. Before this existed it was open-coded in five places across three
/// services — the sort of duplication that stays correct right up until the day
/// the format gains a segment.
/// </remarks>
public static class ProjectNodePath
{
    public const char Separator = '/';

    /// <summary>
    /// The root project's id — the first segment. False when the path is empty or
    /// its first segment is not a GUID, which means the row is corrupt rather than
    /// that the caller asked something unreasonable.
    /// </summary>
    public static bool TryGetRootId(string? path, out Guid rootId)
    {
        rootId = Guid.Empty;
        if (string.IsNullOrEmpty(path)) return false;

        var segments = path.AsSpan().TrimStart(Separator);
        var end = segments.IndexOf(Separator);

        return Guid.TryParse(end < 0 ? segments : segments[..end], out rootId);
    }

    /// <summary>
    /// The root project's id, for the callers that treat a malformed path as
    /// impossible.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT a <c>DomainException</c>: services translate those into a
    /// 409, and a path that cannot be read is corrupt data, not a conflict the
    /// caller could resolve by trying something else.
    /// </remarks>
    public static Guid RootId(string path) =>
        TryGetRootId(path, out var rootId)
            ? rootId
            : throw new ArgumentException(
                $"'{path}' is not a materialized project-node path.", nameof(path));
}
