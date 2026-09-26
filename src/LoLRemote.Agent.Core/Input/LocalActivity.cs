namespace LoLRemote.Agent.Core.Input;

/// <summary>
/// Decide se alguém está usando o PC localmente, a partir dos contadores de
/// milissegundos do Windows (GetTickCount, 32 bits, com volta a zero). O último
/// input registrado pelo Windows inclui o input injetado pelo próprio agente,
/// que é descontado.
/// </summary>
public static class LocalActivity
{
    /// <summary>Tempo sem input local para liberar o controle remoto.</summary>
    public const uint DefaultIdleMs = 10_000;

    /// <summary>Folga para reconhecer o input como sendo do agente.</summary>
    public const uint DefaultInjectionToleranceMs = 150;

    /// <summary>Indica atividade local recente.</summary>
    /// <param name="nowTick">Contador atual.</param>
    /// <param name="lastInputTick">Último input registrado pelo Windows.</param>
    /// <param name="lastInjectedTick">Último input injetado pelo agente, se houver.</param>
    /// <param name="idleMs">Tempo sem input local para liberar.</param>
    /// <param name="toleranceMs">Folga para atribuir o input ao agente.</param>
    public static bool IsActive(
        uint nowTick,
        uint lastInputTick,
        uint? lastInjectedTick,
        uint idleMs = DefaultIdleMs,
        uint toleranceMs = DefaultInjectionToleranceMs)
    {
        var sinceInput = unchecked(nowTick - lastInputTick);
        if (sinceInput >= idleMs)
        {
            return false;
        }

        if (lastInjectedTick is { } injected)
        {
            var afterInjection = unchecked((int)(lastInputTick - injected));
            if (afterInjection <= (int)toleranceMs)
            {
                return false;
            }
        }

        return true;
    }
}
