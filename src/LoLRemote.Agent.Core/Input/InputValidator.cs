using LoLRemote.Agent.Core.Geometry;

namespace LoLRemote.Agent.Core.Input;

/// <summary>Comando de toque recebido do celular.</summary>
/// <param name="Version">Versão do protocolo.</param>
/// <param name="Sequence">Número de sequência, estritamente crescente por sessão.</param>
/// <param name="SentAtMs">Horário de envio no relógio do agente (o celular sincroniza).</param>
/// <param name="Touch">Toque normalizado sobre o vídeo.</param>
public sealed record TapCommand(int Version, long Sequence, long SentAtMs, NormalizedPoint Touch);

/// <summary>Resultado da validação: aceito com o ponto de clique, ou recusado.</summary>
public sealed record InputDecision(ClientPoint? Point, InputRejectReason? Reason)
{
    /// <summary>Indica se o comando foi aceito.</summary>
    public bool Accepted => Reason is null;

    /// <summary>Comando aceito.</summary>
    public static InputDecision Accept(ClientPoint point) => new(point, null);

    /// <summary>Comando recusado.</summary>
    public static InputDecision Reject(InputRejectReason reason) => new(null, reason);
}

/// <summary>Limites da validação.</summary>
/// <param name="MaxAgeMs">Idade máxima de um comando.</param>
/// <param name="MaxFutureMs">Tolerância para relógio adiantado.</param>
/// <param name="MaxCommandsPerSecond">Limite de comandos por segundo.</param>
public sealed record InputLimits(int MaxAgeMs = 1000, int MaxFutureMs = 250, int MaxCommandsPerSecond = 20)
{
    /// <summary>Protocolo suportado.</summary>
    public const int ProtocolVersion = 1;
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

    /// <summary>Valida o comando no instante <paramref name="nowMs"/> (relógio do agente).</summary>
    public InputDecision Validate(
        TapCommand command,
        long nowMs,
        InputGateState gate,
        VideoLayout layout,
        PixelRect clientArea)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(layout);

        if (command.Version != InputLimits.ProtocolVersion)
        {
            return InputDecision.Reject(InputRejectReason.UnsupportedVersion);
        }

        if (command.Sequence <= _lastSequence)
        {
            return InputDecision.Reject(InputRejectReason.OutOfOrder);
        }

        _lastSequence = command.Sequence;

        if (nowMs - command.SentAtMs > _limits.MaxAgeMs)
        {
            return InputDecision.Reject(InputRejectReason.Expired);
        }

        if (command.SentAtMs - nowMs > _limits.MaxFutureMs)
        {
            return InputDecision.Reject(InputRejectReason.FromFuture);
        }

        while (_recent.Count > 0 && nowMs - _recent.Peek() >= 1000)
        {
            _recent.Dequeue();
        }

        if (_recent.Count >= _limits.MaxCommandsPerSecond)
        {
            return InputDecision.Reject(InputRejectReason.RateLimited);
        }

        _recent.Enqueue(nowMs);

        if (InputGate.Evaluate(gate) is { } blocked)
        {
            return InputDecision.Reject(blocked);
        }

        return CoordinateMapper.TryMap(command.Touch, layout, clientArea, out var point) switch
        {
            MappingFailure.None => InputDecision.Accept(point),
            MappingFailure.InvalidCoordinates => InputDecision.Reject(InputRejectReason.InvalidCoordinates),
            MappingFailure.Letterbox => InputDecision.Reject(InputRejectReason.Letterbox),
            _ => InputDecision.Reject(InputRejectReason.OutsideClientArea),
        };
    }
}
