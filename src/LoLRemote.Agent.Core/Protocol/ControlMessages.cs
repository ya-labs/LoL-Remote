using System.Text.Json.Serialization;
using LoLRemote.Agent.Core.Input;

namespace LoLRemote.Agent.Core.Protocol;

/// <summary>
/// Mensagem de controle trocada no canal de dados WebRTC "control". O campo
/// "type" identifica a mensagem; o contrato está em
/// contracts/schemas/control-message.schema.json.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TapMessage), "input.tap")]
[JsonDerivedType(typeof(ScrollMessage), "input.scroll")]
[JsonDerivedType(typeof(TextMessage), "input.text")]
[JsonDerivedType(typeof(KeyMessage), "input.key")]
[JsonDerivedType(typeof(InputAckMessage), "input.ack")]
[JsonDerivedType(typeof(StateMessage), "state")]
public abstract record ControlMessage
{
    /// <summary>Versão do protocolo.</summary>
    [JsonPropertyName("v")]
    public required int Version { get; init; }
}

/// <summary>Celular → agente: toque em um ponto do vídeo.</summary>
public sealed record TapMessage : ControlMessage
{
    /// <summary>Sequência estritamente crescente por sessão.</summary>
    [JsonPropertyName("seq")]
    public required long Sequence { get; init; }

    /// <summary>Horário de envio no relógio do agente, em ms.</summary>
    [JsonPropertyName("sentAt")]
    public required long SentAtMs { get; init; }

    /// <summary>X normalizado (0..1) sobre o vídeo.</summary>
    [JsonPropertyName("x")]
    public required double X { get; init; }

    /// <summary>Y normalizado (0..1) sobre o vídeo.</summary>
    [JsonPropertyName("y")]
    public required double Y { get; init; }
}

/// <summary>Celular → agente: rolagem no ponto do vídeo.</summary>
public sealed record ScrollMessage : ControlMessage
{
    /// <summary>Sequência estritamente crescente por sessão.</summary>
    [JsonPropertyName("seq")]
    public required long Sequence { get; init; }

    /// <summary>Horário de envio no relógio do agente, em ms.</summary>
    [JsonPropertyName("sentAt")]
    public required long SentAtMs { get; init; }

    /// <summary>X normalizado (0..1) sobre o vídeo.</summary>
    [JsonPropertyName("x")]
    public required double X { get; init; }

    /// <summary>Y normalizado (0..1) sobre o vídeo.</summary>
    [JsonPropertyName("y")]
    public required double Y { get; init; }

    /// <summary>"Cliques" de roda; positivo rola o conteúdo para baixo.</summary>
    [JsonPropertyName("dy")]
    public required int Notches { get; init; }
}

/// <summary>Celular → agente: texto a digitar na janela alvo.</summary>
public sealed record TextMessage : ControlMessage
{
    /// <summary>Sequência estritamente crescente por sessão.</summary>
    [JsonPropertyName("seq")]
    public required long Sequence { get; init; }

    /// <summary>Horário de envio no relógio do agente, em ms.</summary>
    [JsonPropertyName("sentAt")]
    public required long SentAtMs { get; init; }

    /// <summary>Texto; nunca é registrado em log.</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>Não expõe o texto em logs acidentais.</summary>
    public override string ToString() => $"TextMessage {{ Sequence = {Sequence} }}";
}

/// <summary>Celular → agente: tecla especial.</summary>
public sealed record KeyMessage : ControlMessage
{
    /// <summary>Sequência estritamente crescente por sessão.</summary>
    [JsonPropertyName("seq")]
    public required long Sequence { get; init; }

    /// <summary>Horário de envio no relógio do agente, em ms.</summary>
    [JsonPropertyName("sentAt")]
    public required long SentAtMs { get; init; }

    /// <summary>Tecla.</summary>
    [JsonPropertyName("key")]
    [JsonConverter(typeof(KebabCaseEnumConverter<SpecialKey>))]
    public required SpecialKey Key { get; init; }
}

/// <summary>Resultado de um comando.</summary>
[JsonConverter(typeof(KebabCaseEnumConverter<AckStatus>))]
public enum AckStatus
{
    /// <summary>Comando executado.</summary>
    Accepted,

    /// <summary>Comando recusado; veja o motivo.</summary>
    Rejected,
}

/// <summary>Agente → celular: confirmação explícita de cada comando.</summary>
public sealed record InputAckMessage : ControlMessage
{
    /// <summary>Sequência do comando confirmado.</summary>
    [JsonPropertyName("seq")]
    public required long Sequence { get; init; }

    /// <summary>Aceito ou recusado.</summary>
    [JsonPropertyName("status")]
    public required AckStatus Status { get; init; }

    /// <summary>Motivo da recusa; ausente quando aceito.</summary>
    [JsonPropertyName("reason")]
    [JsonConverter(typeof(KebabCaseEnumConverter<InputRejectReason>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputRejectReason? Reason { get; init; }
}

/// <summary>Agente → celular: estado atual e se o input está liberado.</summary>
public sealed record StateMessage : ControlMessage
{
    /// <summary>Fase do jogo, com os nomes da LCU.</summary>
    [JsonPropertyName("phase")]
    [JsonConverter(typeof(JsonStringEnumConverter<GamePhase>))]
    public required GamePhase Phase { get; init; }

    /// <summary>Se toques serão aceitos agora.</summary>
    [JsonPropertyName("inputAllowed")]
    public required bool InputAllowed { get; init; }

    /// <summary>Motivo do bloqueio, quando houver.</summary>
    [JsonPropertyName("reason")]
    [JsonConverter(typeof(KebabCaseEnumConverter<InputRejectReason>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputRejectReason? Reason { get; init; }
}
