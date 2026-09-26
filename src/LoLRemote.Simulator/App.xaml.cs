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

        var window = new MainWindow(engine);
        MainWindow = window;
        window.Show();
    }
}
