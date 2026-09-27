using System.Runtime.InteropServices;
using LoLRemote.Agent.Capture;
using LoLRemote.Agent.League;

namespace LoLRemote.Agent.Platform;

/// <summary>
/// Mantém a janela alvo e a captura dela. Quando a janela some (o League Client
/// recria a janela de interface depois de uma partida, por exemplo), procura de
/// novo a cada segundo com as mesmas regras de falha fechada, recria a captura
/// e atualiza a fonte da fase. Enquanto não reencontra, o input fica bloqueado
/// (target-unavailable).
/// </summary>
internal sealed class TargetTracker : IAsyncDisposable
{
    private readonly TargetSpec _spec;
    private readonly Func<TargetWindow, PhaseSource?> _phaseSourceFor;
    private readonly PhaseMonitor _phase;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private TargetWindow _target;
    private FrameSource _frames;
    private Task? _loop;

    public TargetTracker(TargetSpec spec, TargetWindow initial, Func<TargetWindow, PhaseSource?> phaseSourceFor, PhaseMonitor phase)
    {
        _spec = spec;
        _phaseSourceFor = phaseSourceFor;
        _phase = phase;
        _target = initial;
        _frames = new FrameSource(initial.Handle);
    }

    public TargetWindow Target
    {
        get
        {
            lock (_gate)
            {
                return _target;
            }
        }
    }

    public CapturedFrame? LatestFrame
    {
        get
        {
            lock (_gate)
            {
                return _frames.Latest;
            }
        }
    }

    public void Start() => _loop = Task.Run(() => RunAsync(_stop.Token));

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Encerramento normal.
            }
        }

        lock (_gate)
        {
            _frames.Dispose();
        }

        _stop.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var reported = false;
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            if (Target.Describe().Valid)
            {
                reported = false;
                continue;
            }

            if (!reported)
            {
                Console.WriteLine("Janela do alvo sumiu; procurando de novo...");
                reported = true;
            }

            var found = TargetWindow.Find(_spec, out _);
            if (found is null || _phaseSourceFor(found) is not { } source)
            {
                continue;
            }

            FrameSource frames;
            try
            {
                frames = new FrameSource(found.Handle);
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            {
                continue;
            }

            FrameSource old;
            lock (_gate)
            {
                old = _frames;
                _frames = frames;
                _target = found;
            }

            old.Dispose();
            _phase.Replace(source);
            reported = false;
            Console.WriteLine($"Janela do alvo reencontrada: {found.ProcessName} (PID {found.ProcessId}).");
        }
    }
}
