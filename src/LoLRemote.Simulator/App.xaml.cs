using System.Windows;
using LoLRemote.Simulator.Core;

namespace LoLRemote.Simulator;

internal sealed partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var fast = e.Args.Any(a => string.Equals(a, "--fast", StringComparison.OrdinalIgnoreCase));
        var engine = new SimulatorEngine(
            TimeProvider.System,
            fast ? SimulatorTimings.Fast : SimulatorTimings.Default);

        // Informa a fase ao agente do mesmo jeito que o League Client (lockfile + API local).
        var phaseServer = new PhaseServer();
        Exit += (_, _) => phaseServer.Dispose();
        phaseServer.SetPhase(LcuGameflow.ToGameflowPhase(engine.State.Phase));
        engine.StateChanged += (_, state) => phaseServer.SetPhase(LcuGameflow.ToGameflowPhase(state.Phase));
        phaseServer.Start();

        var window = new MainWindow(engine);
        MainWindow = window;
        window.Show();
    }
}
