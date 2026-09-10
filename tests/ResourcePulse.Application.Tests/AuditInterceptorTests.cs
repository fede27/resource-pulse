using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Auth;
using ResourcePulse.Domain.Teams;
using ResourcePulse.Persistence;

namespace ResourcePulse.Application.Tests;

// What the audit stamp actually promises. It could not be tested at all until the
// interceptor took its clock from DI: "CreatedAt is roughly now" is not an
// assertion, and the immutability rule below had no way to be shown at all.
public class AuditInterceptorTests
{
    private static readonly DateTimeOffset Created = new(2026, 3, 1, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Modified = new(2026, 4, 2, 14, 0, 0, TimeSpan.Zero);

    private sealed class Accessor(string sub) : ICurrentUserAccessor
    {
        public CurrentUser User { get; } = new(sub, "", "", new Dictionary<string, string>());
        public bool IsAuthenticated => sub.Length > 0;
        public string? AuthenticationScheme => "test";
    }

    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ResourcePulseDbContext ContextOver(
        string name, ICurrentUserAccessor accessor, TimeProvider clock) =>
        new(new DbContextOptionsBuilder<ResourcePulseDbContext>()
            .UseInMemoryDatabase(name)
            .AddInterceptors(new AuditInterceptor(accessor, clock))
            .Options);

    [Fact]
    public async Task AnInsertIsStampedWithTheClockAndTheSubject()
    {
        var clock = new MovableClock(Created);
        await using var db = ContextOver($"audit-{Guid.NewGuid()}", new Accessor("user-a"), clock);

        var team = Team.Create("Platform");
        db.Add(team);
        await db.SaveChangesAsync();

        team.CreatedAt.Should().Be(Created.UtcDateTime);
        team.CreatedBy.Should().Be("user-a");
        // Not "created and immediately updated" — the update columns stay empty
        // until something actually updates the row.
        team.UpdatedAt.Should().BeNull();
        team.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public async Task AnUpdateStampsTheUpdateColumnsAndLeavesCreationAlone()
    {
        var clock = new MovableClock(Created);
        var name = $"audit-{Guid.NewGuid()}";
        await using var db = ContextOver(name, new Accessor("user-a"), clock);

        var team = Team.Create("Platform");
        db.Add(team);
        await db.SaveChangesAsync();

        // A different person, a month later.
        await using var second = ContextOver(name, new Accessor("user-b"), clock);
        clock.Now = Modified;
        var loaded = await second.Set<Team>().SingleAsync(t => t.Id == team.Id);
        loaded.Rename("Platform & Tooling");
        await second.SaveChangesAsync();

        loaded.UpdatedAt.Should().Be(Modified.UtcDateTime);
        loaded.UpdatedBy.Should().Be("user-b");
        loaded.CreatedAt.Should().Be(Created.UtcDateTime);
        loaded.CreatedBy.Should().Be("user-a");
    }

    [Fact]
    public async Task CreationCannotBeRewrittenByAnUpdate()
    {
        // The interceptor marks CreatedAt/CreatedBy unmodified rather than trusting
        // the caller. Without that, anything holding a stale or hand-built entity
        // silently rewrites who created the row — the one fact an audit trail
        // exists to keep.
        var clock = new MovableClock(Created);
        var name = $"audit-{Guid.NewGuid()}";
        await using var db = ContextOver(name, new Accessor("user-a"), clock);

        var team = Team.Create("Platform");
        db.Add(team);
        await db.SaveChangesAsync();

        await using var second = ContextOver(name, new Accessor("attacker"), clock);
        clock.Now = Modified;
        var loaded = await second.Set<Team>().SingleAsync(t => t.Id == team.Id);
        loaded.CreatedBy = "attacker";
        loaded.CreatedAt = Modified.UtcDateTime;
        loaded.Rename("Renamed");
        await second.SaveChangesAsync();

        await using var third = ContextOver(name, new Accessor("reader"), clock);
        var reread = await third.Set<Team>().SingleAsync(t => t.Id == team.Id);
        reread.CreatedBy.Should().Be("user-a");
        reread.CreatedAt.Should().Be(Created.UtcDateTime);
    }

    [Fact]
    public async Task SavingWithNoSubjectIsRefused()
    {
        // Fail loud: an unattributable write is worse than a failed one, and the
        // seeders exist precisely so nothing has to save anonymously.
        var clock = new MovableClock(Created);
        await using var db = ContextOver($"audit-{Guid.NewGuid()}", new Accessor(""), clock);

        db.Add(Team.Create("Platform"));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current user Sub is empty*");
    }
}
