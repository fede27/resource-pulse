using System.Text.Json;
using TimeSloth.Domain.Allocations;
using TimeSloth.Services.Plan;

namespace TimeSloth.Application.Tests;

// The "kind" discriminator round-trip is the contract the OpenAPI oneOf
// describes (ADR-0018). If these hold, the wire shape orval consumes is correct.
public class PlanCommandSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SplitAt_RoundTripsThroughBaseType_WithKindDiscriminator()
    {
        PlanCommand cmd = new SplitAtCommand { Id = Guid.NewGuid(), Date = new DateOnly(2026, 6, 8), DryRun = true };

        var json = JsonSerializer.Serialize(cmd, Options);
        using (var doc = JsonDocument.Parse(json))
        {
            doc.RootElement.GetProperty("kind").GetString().Should().Be("splitAt");
            doc.RootElement.GetProperty("dryRun").GetBoolean().Should().BeTrue();
        }

        var back = JsonSerializer.Deserialize<PlanCommand>(json, Options);
        back.Should().BeOfType<SplitAtCommand>();
        ((SplitAtCommand)back!).Id.Should().Be(((SplitAtCommand)cmd).Id);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("createByHours")]
    [InlineData("coverInferred")]
    [InlineData("edit")]
    [InlineData("splitAt")]
    [InlineData("changeRateFrom")]
    [InlineData("move")]
    [InlineData("retarget")]
    [InlineData("resize")]
    [InlineData("shiftFrom")]
    [InlineData("reassign")]
    [InlineData("changeStatus")]
    [InlineData("delete")]
    [InlineData("createDemand")]
    [InlineData("editDemand")]
    [InlineData("deleteDemand")]
    [InlineData("followNode")]
    [InlineData("pin")]
    [InlineData("replanNode")]
    [InlineData("moveSubtree")]
    public void EveryKind_DeserializesToConcreteCommand(string kind)
    {
        var json = $$"""{ "kind": "{{kind}}", "id": "{{Guid.NewGuid()}}" }""";

        var cmd = JsonSerializer.Deserialize<PlanCommand>(json, Options);

        cmd.Should().NotBeNull();
        cmd.Should().BeAssignableTo<PlanCommand>();
    }

    [Fact]
    public void Move_DeserializesDeltaDays()
    {
        var json = """{ "kind": "move", "id": "11111111-1111-1111-1111-111111111111", "deltaDays": -3 }""";

        var cmd = JsonSerializer.Deserialize<PlanCommand>(json, Options);

        cmd.Should().BeOfType<MoveCommand>();
        ((MoveCommand)cmd!).DeltaDays.Should().Be(-3);
    }

    [Fact]
    public void Retarget_DeserializesTargetDemand()
    {
        var json = """
            { "kind": "retarget", "id": "11111111-1111-1111-1111-111111111111",
              "demandId": "22222222-2222-2222-2222-222222222222" }
            """;

        var cmd = JsonSerializer.Deserialize<PlanCommand>(json, Options);

        cmd.Should().BeOfType<RetargetCommand>();
        ((RetargetCommand)cmd!).DemandId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }

    [Fact]
    public void UnknownKind_FailsDeserialization()
    {
        var json = """{ "kind": "frobnicate", "id": "11111111-1111-1111-1111-111111111111" }""";

        var act = () => JsonSerializer.Deserialize<PlanCommand>(json, Options);

        act.Should().Throw<JsonException>();
    }

    // Enums travel as integers (no JsonStringEnumConverter is registered; the
    // OpenAPI document carries the names via x-enum-varnames), so the wire
    // sample here uses the numeric value: BoundaryEdge.End = 1.
    [Fact]
    public void FollowNode_DeserializesTheEdge_AndTakesNoReferent()
    {
        var json = """
            { "kind": "followNode", "id": "11111111-1111-1111-1111-111111111111", "edge": 1 }
            """;

        var cmd = JsonSerializer.Deserialize<PlanCommand>(json, Options);

        cmd.Should().BeOfType<FollowNodeCommand>();
        ((FollowNodeCommand)cmd!).Edge.Should().Be(BoundaryEdge.End);
    }

    [Fact]
    public void Create_AcceptsAnEmptyWindow_BothEndsNull()
    {
        // The wire shape of "born as wide as the node" (ADR-0035 §2): the
        // absence of a date IS the instruction to follow.
        var json = """
            { "kind": "create", "demandId": "11111111-1111-1111-1111-111111111111",
              "resourceId": "22222222-2222-2222-2222-222222222222", "percent": 60 }
            """;

        var cmd = (CreateCommand)JsonSerializer.Deserialize<PlanCommand>(json, Options)!;

        cmd.PeriodStart.Should().BeNull();
        cmd.PeriodEnd.Should().BeNull();
        cmd.Percent.Should().Be(60m);
    }

    [Fact]
    public void Create_AcceptsOneEndOnly()
    {
        var json = """
            { "kind": "create", "demandId": "11111111-1111-1111-1111-111111111111",
              "resourceId": "22222222-2222-2222-2222-222222222222",
              "periodEnd": "2026-06-14", "percent": 50 }
            """;

        var cmd = (CreateCommand)JsonSerializer.Deserialize<PlanCommand>(json, Options)!;

        cmd.PeriodStart.Should().BeNull();
        cmd.PeriodEnd.Should().Be(new DateOnly(2026, 6, 14));
    }

    [Theory]
    [InlineData("setAnchor")]
    [InlineData("setAvailability")]
    [InlineData("moveConstraint")]
    public void RetiredKinds_NoLongerDeserialize(string kind)
    {
        // ADR-0035 removed these. A client still sending one must fail loudly
        // rather than have it silently ignored.
        var json = $$"""{ "kind": "{{kind}}", "id": "{{Guid.NewGuid()}}" }""";

        var act = () => JsonSerializer.Deserialize<PlanCommand>(json, Options);

        act.Should().Throw<JsonException>();
    }
}
