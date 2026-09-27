using System.Globalization;
using LoLRemote.Agent.Core.Geometry;

namespace LoLRemote.Agent.Core.Input;

/// <summary>Campos comuns a todo comando de input.</summary>
public interface IInputCommand
{
    /// <summary>Versão do protocolo.</summary>
    int Version { get; }

    /// <summary>Sequência estritamente crescente por sessão, compartilhada entre os tipos.</summary>
    long Sequence { get; }

    /// <summary>Horário de envio no relógio do agente.</summary>
    long SentAtMs { get; }
}

/// <summary>Toque (clique esquerdo) em um ponto do vídeo.</summary>
/// <param name="Version">Versão do protocolo.</param>
/// <param name="Sequence">Sequência.</param>
/// <param name="SentAtMs">Horário de envio no relógio do agente.</param>
/// <param name="Touch">Toque normalizado sobre o vídeo.</param>
public sealed record TapCommand(int Version, long Sequence, long SentAtMs, NormalizedPoint Touch) : IInputCommand;

/// <summary>Rolagem no ponto do vídeo; <paramref name="Notches"/> &gt; 0 rola o conteúdo para baixo.</summary>
/// <param name="Version">Versão do protocolo.</param>
/// <param name="Sequence">Sequência.</param>
/// <param name="SentAtMs">Horário de envio no relógio do agente.</param>
/// <param name="Touch">Ponto onde rolar.</param>
/// <param name="Notches">Quantidade de "cliques" da roda, de -5 a 5, diferente de 0.</param>
public sealed record ScrollCommand(int Version, long Sequence, long SentAtMs, NormalizedPoint Touch, int Notches) : IInputCommand;

/// <summary>Texto digitado pela pessoa, enviado à janela alvo.</summary>
/// <param name="Version">Versão do protocolo.</param>
/// <param name="Sequence">Sequência.</param>
/// <param name="SentAtMs">Horário de envio no relógio do agente.</param>
/// <param name="Text">De 1 a 64 caracteres, sem caracteres de controle.</param>
public sealed record TextCommand(int Version, long Sequence, long SentAtMs, string Text) : IInputCommand;

/// <summary>Teclas especiais permitidas.</summary>
public enum SpecialKey
{
    /// <summary>Enter.</summary>
    Enter,

    /// <summary>Apagar (Backspace).</summary>
    Backspace,

    /// <summary>Esc.</summary>
    Escape,

    /// <summary>Tab.</summary>
    Tab,
}

/// <summary>Tecla especial.</summary>
/// <param name="Version">Versão do protocolo.</param>
/// <param name="Sequence">Sequência.</param>
/// <param name="SentAtMs">Horário de envio no relógio do agente.</param>
/// <param name="Key">Tecla.</param>
public sealed record KeyCommand(int Version, long Sequence, long SentAtMs, SpecialKey Key) : IInputCommand;

/// <summary>Resultado da validação: aceito (com ponto, para toque e rolagem) ou recusado.</summary>
public sealed record InputDecision(ClientPoint? Point, InputRejectReason? Reason)
{
    /// <summary>Indica se o comando foi aceito.</summary>
    public bool Accepted => Reason is null;

    /// <summary>Comando aceito com ponto de destino.</summary>
    public static InputDecision Accept(ClientPoint point) => new(point, null);

    /// <summary>Comando aceito sem ponto (teclado).</summary>
    public static InputDecision AcceptWithoutPoint() => new(null, null);

    /// <summary>Comando recusado.</summary>
    public static InputDecision Reject(InputRejectReason reason) => new(null, reason);
}

/// <summary>Limites da validação.</summary>
/// <param name="MaxAgeMs">Idade máxima de um comando.</param>
/// <param name="MaxFutureMs">Tolerância para relógio adiantado.</param>
/// <param name="MaxCommandsPerSecond">Limite de comandos por segundo, somando todos os tipos.</param>
public sealed record InputLimits(int MaxAgeMs = 1000, int MaxFutureMs = 250, int MaxCommandsPerSecond = 20)
{
    /// <summary>Protocolo suportado.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Tamanho máximo de um texto.</summary>
    public const int MaxTextLength = 64;

    /// <summary>Máximo de "cliques" de roda por comando.</summary>
    public const int MaxScrollNotches = 5;
}

/// <summary>
/// Valida cada comando antes de qualquer input no Windows. Uma instância por
/// sessão; não é thread-safe. Toda sequência maior que a última é consumida,
/// mesmo quando o comando é recusado depois, para impedir reenvio.
/// </summary>
public sealed class InputValidator
{
    private readonly InputLimits _limits;
    private readonly Queue<long> _recent = new();
    private long _lastSequence = long.MinValue;

    /// <summary>Cria o validador de uma sessão.</summary>
    public InputValidator(InputLimits? limits = null)
    {
        _limits = limits ?? new InputLimits();
    }

    /// <summary>Última sequência consumida.</summary>
    public long LastSequence => _lastSequence;

    /// <summary>Valida um toque.</summary>
    public InputDecision Validate(TapCommand command, long nowMs, InputGateState gate, VideoLayout layout, PixelRect clientArea)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(layout);
        return ValidateCommon(command, nowMs, gate) is { } rejected
            ? InputDecision.Reject(rejected)
            : Map(command.Touch, layout, clientArea);
    }

    /// <summary>Valida uma rolagem.</summary>
    public InputDecision Validate(ScrollCommand command, long nowMs, InputGateState gate, VideoLayout layout, PixelRect clientArea)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(layout);
        if (ValidateCommon(command, nowMs, gate) is { } rejected)
        {
            return InputDecision.Reject(rejected);
        }

        return command.Notches == 0 || Math.Abs(command.Notches) > InputLimits.MaxScrollNotches
            ? InputDecision.Reject(InputRejectReason.InvalidScroll)
            : Map(command.Touch, layout, clientArea);
    }

    /// <summary>Valida um texto.</summary>
    public InputDecision Validate(TextCommand command, long nowMs, InputGateState gate)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (ValidateCommon(command, nowMs, gate) is { } rejected)
        {
            return InputDecision.Reject(rejected);
        }

        return IsAcceptableText(command.Text)
            ? InputDecision.AcceptWithoutPoint()
            : InputDecision.Reject(InputRejectReason.InvalidText);
    }

    /// <summary>Valida uma tecla especial.</summary>
    public InputDecision Validate(KeyCommand command, long nowMs, InputGateState gate)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (ValidateCommon(command, nowMs, gate) is { } rejected)
        {
            return InputDecision.Reject(rejected);
        }

        return Enum.IsDefined(command.Key)
            ? InputDecision.AcceptWithoutPoint()
            : InputDecision.Reject(InputRejectReason.InvalidKey);
    }

    /// <summary>
    /// Texto aceitável: 1 a 64 unidades UTF-16, sem caracteres de controle e com
    /// pares substitutos (emojis etc.) bem formados.
    /// </summary>
    public static bool IsAcceptableText(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > InputLimits.MaxTextLength)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return false;
                }

                i++;
                continue;
            }

            var category = char.GetUnicodeCategory(c);
            if (char.IsLowSurrogate(c)
                || category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                return false;
            }
        }

        return true;
    }

    private InputRejectReason? ValidateCommon(IInputCommand command, long nowMs, InputGateState gate)
    {
        ArgumentNullException.ThrowIfNull(gate);

        if (command.Version != InputLimits.ProtocolVersion)
        {
            return InputRejectReason.UnsupportedVersion;
        }

        if (command.Sequence <= _lastSequence)
        {
            return InputRejectReason.OutOfOrder;
        }

        _lastSequence = command.Sequence;

        if (nowMs - command.SentAtMs > _limits.MaxAgeMs)
        {
            return InputRejectReason.Expired;
        }

        if (command.SentAtMs - nowMs > _limits.MaxFutureMs)
        {
            return InputRejectReason.FromFuture;
        }

        while (_recent.Count > 0 && nowMs - _recent.Peek() >= 1000)
        {
            _recent.Dequeue();
        }

        if (_recent.Count >= _limits.MaxCommandsPerSecond)
        {
            return InputRejectReason.RateLimited;
        }

        _recent.Enqueue(nowMs);
        return InputGate.Evaluate(gate);
    }

    private static InputDecision Map(NormalizedPoint touch, VideoLayout layout, PixelRect clientArea) =>
        CoordinateMapper.TryMap(touch, layout, clientArea, out var point) switch
        {
            MappingFailure.None => InputDecision.Accept(point),
            MappingFailure.InvalidCoordinates => InputDecision.Reject(InputRejectReason.InvalidCoordinates),
            MappingFailure.Letterbox => InputDecision.Reject(InputRejectReason.Letterbox),
            _ => InputDecision.Reject(InputRejectReason.OutsideClientArea),
        };
}
