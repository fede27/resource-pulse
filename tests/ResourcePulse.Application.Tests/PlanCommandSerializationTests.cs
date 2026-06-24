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
    [InlineData("createPlaceholder")]
    [InlineData("edit")]
    [InlineData("splitAt")]
    [InlineData("changeRateFrom")]
    [InlineData("move")]
    [InlineData("retarget")]
    [InlineData("resize")]
    [InlineData("shiftFrom")]
    [InlineData("convertToPlaceholder")]
    [InlineData("reassign")]
    [InlineData("changeStatus")]
    [InlineData("delete")]
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
    public void Retarget_DeserializesModeEnum()
    {
        // Enums serialize as integers in this app (KeepHours = 1).
        var json = """
            { "kind": "retarget", "id": "11111111-1111-1111-1111-111111111111",
              "newPeriodStart": "2026-06-08", "newPeriodEnd": "2026-06-21", "mode": 1 }
            """;

        var cmd = JsonSerializer.Deserialize<PlanCommand>(json, Options);

        cmd.Should().BeOfType<RetargetCommand>();
        ((RetargetCommand)cmd!).Mode.Should().Be(MoveMode.KeepHours);
    }

    [Fact]
    public void UnknownKind_FailsDeserialization()
    {
        var json = """{ "kind": "frobnicate", "id": "11111111-1111-1111-1111-111111111111" }""";

        var act = () => JsonSerializer.Deserialize<PlanCommand>(json, Options);

        act.Should().Throw<JsonException>();
    }
}
