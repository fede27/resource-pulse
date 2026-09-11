using System.Text.Json;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Services.Plan;

namespace ResourcePulse.Application.Tests;

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
    [InlineData("setAnchor")]
    [InlineData("pin")]
    [InlineData("replanNode")]
    [InlineData("moveSubtree")]
    [InlineData("setAvailability")]
    [InlineData("moveConstraint")]
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
    // samples here use the numeric values: BoundaryEdge.End = 1, AnchorKind.NodeEnd = 2.
    [Fact]
    public void SetAnchor_DeserializesEdgeAndAnchorSpec()
    {
        var json = """
            { "kind": "setAnchor", "id": "11111111-1111-1111-1111-111111111111",
              "edge": 1, "anchor": { "kind": 2, "nodeId": "22222222-2222-2222-2222-222222222222" } }
            """;

        var cmd = JsonSerializer.Deserialize<PlanCommand>(json, Options);

        cmd.Should().BeOfType<SetAnchorCommand>();
        var set = (SetAnchorCommand)cmd!;
        set.Edge.Should().Be(BoundaryEdge.End);
        set.Anchor.Kind.Should().Be(AnchorKind.NodeEnd);
        set.Anchor.NodeId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }

    [Fact]
    public void Create_DeserializesOptionalAnchors_NullWhenAbsent()
    {
        var json = """
            { "kind": "create", "demandId": "11111111-1111-1111-1111-111111111111",
              "resourceId": "22222222-2222-2222-2222-222222222222",
              "periodStart": "2026-06-01", "periodEnd": "2026-06-14", "percent": 50,
              "endAnchor": { "kind": 2, "nodeId": "33333333-3333-3333-3333-333333333333" } }
            """;

        var cmd = (CreateCommand)JsonSerializer.Deserialize<PlanCommand>(json, Options)!;

        cmd.StartAnchor.Should().BeNull();
        cmd.EndAnchor!.Kind.Should().Be(AnchorKind.NodeEnd);
    }
}
