using System.Text.Json;
using System.Text.Json.Nodes;
using LoLRemote.Agent.Core.Input;
using LoLRemote.Agent.Core.Protocol;

namespace LoLRemote.Agent.Core.Tests;

public class ProtocolTests
{
    private static readonly string Contracts = Path.Combine(AppContext.BaseDirectory, "contracts");

    public static TheoryData<string> ValidExamples => Examples("examples/control");

    public static TheoryData<string> InvalidExamples => Examples("examples/control/invalid");

    [Theory]
    [MemberData(nameof(ValidExamples))]
    public void Valid_contract_examples_parse_and_round_trip(string file)
    {
        var json = File.ReadAllText(Path.Combine(Contracts, file));

        Assert.True(ControlMessageSerializer.TryParse(json, out var message), file);
        var roundTrip = ControlMessageSerializer.Serialize(message!);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(roundTrip)), $"{file}: {roundTrip}");
    }

    [Theory]
    [MemberData(nameof(InvalidExamples))]
    public void Invalid_contract_examples_are_refused(string file)
    {
        var json = File.ReadAllText(Path.Combine(Contracts, file));

        Assert.False(ControlMessageSerializer.TryParse(json, out _), file);
    }

    [Fact]
    public void Tap_example_has_expected_values()
    {
        Assert.True(ControlMessageSerializer.TryParse(
            File.ReadAllText(Path.Combine(Contracts, "examples/control/tap.json")), out var message));

        var tap = Assert.IsType<TapMessage>(message);
        Assert.Equal(42, tap.Sequence);
        Assert.Equal(0.61, tap.Y, 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"v\":1,\"seq\":1}")]
    [InlineData("[1,2,3]")]
    public void Garbage_is_refused(string json)
    {
        Assert.False(ControlMessageSerializer.TryParse(json, out _));
    }

    [Fact]
    public void Oversized_message_is_refused_before_parsing()
    {
        var json = "{\"type\":\"input.tap\",\"pad\":\"" + new string('x', ControlMessageSerializer.MaxMessageLength) + "\"}";

        Assert.False(ControlMessageSerializer.TryParse(json, out _));
    }

    [Fact]
    public void Reject_reasons_match_the_schema()
    {
        var codeValues = Enum.GetNames<InputRejectReason>().Select(JsonNamingPolicy.KebabCaseLower.ConvertName);

        AssertSameSet(SchemaEnum("rejectReason"), codeValues);
    }

    [Fact]
    public void Game_phases_match_the_schema()
    {
        AssertSameSet(SchemaEnum("phase"), Enum.GetNames<GamePhase>());
    }

    private static void AssertSameSet(IEnumerable<string> schema, IEnumerable<string> code)
    {
        var onlyInSchema = schema.Except(code, StringComparer.Ordinal).ToList();
        var onlyInCode = code.Except(schema, StringComparer.Ordinal).ToList();
        Assert.True(
            onlyInSchema.Count == 0 && onlyInCode.Count == 0,
            $"Só no schema: [{string.Join(", ", onlyInSchema)}]; só no código: [{string.Join(", ", onlyInCode)}]");
    }

    private static List<string> SchemaEnum(string definition)
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(Contracts, "schemas/control-message.schema.json")));
        return schema.RootElement.GetProperty("$defs").GetProperty(definition).GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static TheoryData<string> Examples(string folder)
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Contracts, folder), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(Contracts, file).Replace('\\', '/'));
        }

        return data;
    }
}
