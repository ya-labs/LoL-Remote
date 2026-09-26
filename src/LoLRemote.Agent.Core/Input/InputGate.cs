namespace LoLRemote.Agent.Core.Input;

/// <summary>Estado do sistema que decide se input remoto é permitido agora.</summary>
/// <param name="TargetValid">Janela alvo encontrada e validada.</param>
/// <param name="TargetMinimized">Janela alvo minimizada.</param>
/// <param name="CaptureFresh">Há frame recente da janela.</param>
/// <param name="Phase">Fase atual do jogo.</param>
/// <param name="LocalActivity">Houve atividade local recente no PC.</param>
/// <param name="RemoteModeActive">O modo remoto está ativo e dentro do prazo.</param>
public sealed record InputGateState(
    bool TargetValid,
    bool TargetMinimized,
    bool CaptureFresh,
    GamePhase Phase,
    bool LocalActivity,
    bool RemoteModeActive);

/// <summary>Política de bloqueio: falha fechada, na ordem mais segura.</summary>
public static class InputGate
{
    /// <summary>Retorna o motivo do bloqueio, ou <c>null</c> se o input é permitido.</summary>
    public static InputRejectReason? Evaluate(InputGateState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.RemoteModeActive)
        {
            return InputRejectReason.RemoteModeInactive;
        }

        if (state.Phase == GamePhase.Unknown)
        {
            return InputRejectReason.PhaseUnknown;
        }

        if (!GamePhases.AllowsInput(state.Phase))
        {
            return InputRejectReason.PhaseBlocked;
        }

        if (!state.TargetValid)
        {
            return InputRejectReason.TargetUnavailable;
        }

        if (state.TargetMinimized)
        {
            return InputRejectReason.TargetMinimized;
        }

        if (!state.CaptureFresh)
        {
            return InputRejectReason.CaptureStale;
        }

        return state.LocalActivity ? InputRejectReason.LocalActivity : null;
    }
}
