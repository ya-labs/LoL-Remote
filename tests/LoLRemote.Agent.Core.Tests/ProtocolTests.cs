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

    public static TheoryData<string> RejectedExamples => Examples("examples/control/rejected");

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

    /// <summary>
    /// Fora do schema, mas bem formadas: o agente lê e responde com input.ack
    /// rejected (a validação de valores fica no InputValidator).
    /// </summary>
    [Theory]
    [MemberData(nameof(RejectedExamples))]
    public void Rejected_examples_parse_so_the_agent_can_answer(string file)
    {
        var json = File.ReadAllText(Path.Combine(Contracts, file));

        Assert.True(ControlMessageSerializer.TryParse(json, out _), file);
    }

    [Fact]
    public void Text_message_never_prints_its_content()
    {
        Assert.True(ControlMessageSerializer.TryParse(
            File.ReadAllText(Path.Combine(Contracts, "examples/control/text.json")), out var message));

        var text = Assert.IsType<TextMessage>(message);
        Assert.Equal("Ahri", text.Text);
        Assert.DoesNotContain("Ahri", text.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Scroll_and_key_examples_have_expected_values()
    {
        Assert.True(ControlMessageSerializer.TryParse(
            File.ReadAllText(Path.Combine(Contracts, "examples/control/scroll.json")), out var scroll));
        Assert.Equal(2, Assert.IsType<ScrollMessage>(scroll).Notches);

        Assert.True(ControlMessageSerializer.TryParse(
            File.ReadAllText(Path.Combine(Contracts, "examples/control/key.json")), out var key));
        Assert.Equal(SpecialKey.Enter, Assert.IsType<KeyMessage>(key).Key);
    }

    [Fact]
    public void Special_keys_match_the_schema()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(Contracts, "schemas/control-message.schema.json")));
        var schemaKeys = schema.RootElement.GetProperty("$defs").GetProperty("key").GetProperty("properties")
            .GetProperty("key").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!);

        AssertSameSet(schemaKeys, Enum.GetNames<SpecialKey>().Select(JsonNamingPolicy.KebabCaseLower.ConvertName));
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
