using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LoLRemote.Simulator.Core;

namespace LoLRemote.Simulator;

/// <summary>
/// Janela separada com controles de depuração. Fica fora da janela simulada
/// para não alterar o que o agente captura, e ajuda a testar se o agente
/// escolhe o HWND correto quando o mesmo processo tem mais de uma janela.
/// </summary>
internal sealed partial class ControlWindow : Window
{
    private readonly SimulatorEngine _engine;

    public ControlWindow(SimulatorEngine engine)
    {
        InitializeComponent();
        _engine = engine;
        Refresh(includeLog: true);
    }

    public void Refresh(bool includeLog)
    {
        var state = _engine.State;
        PhaseText.Text = state.Stage == ChampSelectStage.None
            ? $"Fase: {state.Phase}"
            : $"Fase: {state.Phase} / {state.Stage}";

        TimerText.Text = _engine.Remaining is { } remaining
            ? string.Create(CultureInfo.InvariantCulture, $"Restante: {remaining.TotalSeconds:0.0}s")
            : "Restante: -";

        var violations = _engine.GameplayViolationCount;
        ViolationText.Text = $"Violações (input durante a partida): {violations}";
        ViolationText.Foreground = violations > 0 ? Brushes.Firebrick : Brushes.DarkGreen;

        if (includeLog)
        {
            ClickList.ItemsSource = _engine.RecentClicks.Select(Format).ToList();
        }
    }

    private static string Format(ClickRecord click) => string.Create(
        CultureInfo.InvariantCulture,
        $"{click.At.ToLocalTime():HH:mm:ss.f} {click.Phase,-11} ({click.X,6:0.0}, {click.Y,5:0.0}) {click.Outcome} {click.RegionId}");

    private void OnEndGame(object sender, RoutedEventArgs e)
    {
        _engine.EndGame();
        Refresh(includeLog: true);
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _engine.Reset();
        Refresh(includeLog: true);
    }
}
