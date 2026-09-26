namespace LoLRemote.Simulator.Core;

/// <summary>
/// Máquina de estados determinística do fluxo simulado do League Client.
/// O tempo vem de um <see cref="TimeProvider"/> para permitir testes sem espera.
/// Não é thread-safe: use a partir de uma única thread (a thread de UI no app).
/// </summary>
public sealed class SimulatorEngine
{
    /// <summary>Quantidade máxima de cliques mantidos em memória.</summary>
    public const int ClickLogCapacity = 50;

    private readonly TimeProvider _time;
    private readonly SimulatorTimings _timings;
    private readonly LinkedList<ClickRecord> _clickLog = new();

    /// <summary>Cria o simulador na sala inicial.</summary>
    public SimulatorEngine(TimeProvider time, SimulatorTimings timings)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(timings);
        _time = time;
        _timings = timings;
    }

    /// <summary>Disparado sempre que o estado muda.</summary>
    public event EventHandler<SimulatorState>? StateChanged;

    /// <summary>Estado atual. Chame <see cref="Update"/> antes de ler para aplicar expirações.</summary>
    public SimulatorState State { get; private set; } = SimulatorState.Initial;

    /// <summary>Cliques recebidos durante a partida desde a criação ou o último reset.</summary>
    public int GameplayViolationCount { get; private set; }

    /// <summary>Cliques mais recentes, do mais novo para o mais antigo.</summary>
    public IReadOnlyCollection<ClickRecord> RecentClicks => _clickLog;

    /// <summary>Tempo restante da etapa atual, se temporizada.</summary>
    public TimeSpan? Remaining
    {
        get
        {
            if (State.Deadline is not { } deadline)
            {
                return null;
            }

            var remaining = deadline - _time.GetUtcNow();
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>Aplica as transições por tempo esgotado.</summary>
    public void Update()
    {
        var now = _time.GetUtcNow();
        while (State.Deadline is { } deadline && now >= deadline)
        {
            Expire(now);
        }
    }

    /// <summary>Entrega um clique em coordenadas de referência (1280x720).</summary>
    public ClickRecord Click(double x, double y)
    {
        Update();
        var now = _time.GetUtcNow();
        var phase = State.Phase;

        ClickRecord record;
        if (double.IsNaN(x) || double.IsNaN(y) || !SimLayout.Bounds.Contains(x, y))
        {
            record = new ClickRecord(now, x, y, phase, null, ClickOutcome.OutOfBounds);
        }
        else if (phase == SimPhase.InProgress)
        {
            GameplayViolationCount++;
            record = new ClickRecord(now, x, y, phase, RegionIds.GameplayArea, ClickOutcome.GameplayViolation);
        }
        else if (SimLayout.HitTest(phase, State.Stage, x, y) is not { } region)
        {
            record = new ClickRecord(now, x, y, phase, null, ClickOutcome.Missed);
        }
        else
        {
            var outcome = Activate(region.Id, now) ? ClickOutcome.Applied : ClickOutcome.Ignored;
            record = new ClickRecord(now, x, y, phase, region.Id, outcome);
        }

        _clickLog.AddFirst(record);
        if (_clickLog.Count > ClickLogCapacity)
        {
            _clickLog.RemoveLast();
        }

        return record;
    }

    /// <summary>Controle de depuração: encerra a partida e volta à sala.</summary>
    public void EndGame()
    {
        if (State.Phase == SimPhase.InProgress)
        {
            SetState(SimulatorState.Initial with { LastEvent = "Partida encerrada." });
        }
    }

    /// <summary>Controle de depuração: volta ao estado inicial e limpa contadores.</summary>
    public void Reset()
    {
        _clickLog.Clear();
        GameplayViolationCount = 0;
        SetState(SimulatorState.Initial);
    }

    private bool Activate(string regionId, DateTimeOffset now)
    {
        var s = State;
        switch (s.Phase, regionId)
        {
            case (SimPhase.Lobby, RegionIds.FindMatch):
                SetState(SimulatorState.Initial with
                {
                    Phase = SimPhase.Matchmaking,
                    Deadline = now + _timings.Matchmaking,
                });
                return true;

            case (SimPhase.Matchmaking, RegionIds.CancelMatchmaking):
                SetState(SimulatorState.Initial with { LastEvent = "Busca cancelada." });
                return true;

            case (SimPhase.ReadyCheck, RegionIds.Accept) when s.ReadyCheck == ReadyCheckResponse.None:
                SetState(s with { ReadyCheck = ReadyCheckResponse.Accepted });
                return true;

            case (SimPhase.ReadyCheck, RegionIds.Decline) when s.ReadyCheck == ReadyCheckResponse.None:
                SetState(SimulatorState.Initial with { LastEvent = "Partida recusada." });
                return true;

            case (SimPhase.ChampSelect, RegionIds.LockIn) when s.HoveredChampion is { } hovered:
                if (s.Stage == ChampSelectStage.Ban)
                {
                    SetState(s with
                    {
                        Stage = ChampSelectStage.Pick,
                        Deadline = now + _timings.Pick,
                        HoveredChampion = null,
                        BannedChampion = hovered,
                    });
                    return true;
                }

                if (s.Stage == ChampSelectStage.Pick)
                {
                    SetState(s with
                    {
                        Stage = ChampSelectStage.Finalization,
                        Deadline = now + _timings.Finalization,
                        HoveredChampion = null,
                        PickedChampion = hovered,
                    });
                    return true;
                }

                return false;

            case (SimPhase.ChampSelect, _) when RegionIds.TryParseChampion(regionId, out var index):
                if (s.Stage is not (ChampSelectStage.Ban or ChampSelectStage.Pick)
                    || index == s.BannedChampion
                    || index == s.HoveredChampion)
                {
                    return false;
                }

                SetState(s with { HoveredChampion = index });
                return true;

            default:
                return false;
        }
    }

    private void Expire(DateTimeOffset now)
    {
        var s = State;
        switch (s.Phase, s.Stage)
        {
            case (SimPhase.Matchmaking, _):
                SetState(s with
                {
                    Phase = SimPhase.ReadyCheck,
                    Deadline = now + _timings.ReadyCheck,
                    ReadyCheck = ReadyCheckResponse.None,
                    LastEvent = null,
                });
                break;

            case (SimPhase.ReadyCheck, _) when s.ReadyCheck == ReadyCheckResponse.Accepted:
                SetState(s with
                {
                    Phase = SimPhase.ChampSelect,
                    Stage = ChampSelectStage.Ban,
                    Deadline = now + _timings.Ban,
                });
                break;

            case (SimPhase.ReadyCheck, _):
                SetState(SimulatorState.Initial with { LastEvent = "Partida não aceita a tempo." });
                break;

            case (SimPhase.ChampSelect, ChampSelectStage.Ban):
                SetState(s with
                {
                    Stage = ChampSelectStage.Pick,
                    Deadline = now + _timings.Pick,
                    HoveredChampion = null,
                    LastEvent = "Tempo de banimento esgotado.",
                });
                break;

            case (SimPhase.ChampSelect, ChampSelectStage.Pick):
                SetState(SimulatorState.Initial with { LastEvent = "Tempo de escolha esgotado: saída da seleção." });
                break;

            case (SimPhase.ChampSelect, ChampSelectStage.Finalization):
                SetState(s with
                {
                    Phase = SimPhase.InProgress,
                    Stage = ChampSelectStage.None,
                    Deadline = null,
                    LastEvent = null,
                });
                break;

            default:
                // Estado temporizado sem transição conhecida: remove o prazo para não travar o loop.
                SetState(s with { Deadline = null });
                break;
        }
    }

    private void SetState(SimulatorState next)
    {
        if (next == State)
        {
            return;
        }

        State = next;
        StateChanged?.Invoke(this, next);
    }
}
