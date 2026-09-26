using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using LoLRemote.Simulator.Core;

namespace LoLRemote.Simulator;

/// <summary>
/// Janela alvo do simulador. Desenha o layout de <see cref="SimLayout"/> e
/// encaminha cada clique, já convertido para o espaço 1280x720, ao motor.
/// </summary>
internal sealed partial class MainWindow : Window
{
    private static readonly Brush Gold = Frozen(Color.FromRgb(0xC8, 0xAA, 0x6E));
    private static readonly Brush Cream = Frozen(Color.FromRgb(0xF0, 0xE6, 0xD2));
    private static readonly Brush Muted = Frozen(Color.FromRgb(0x78, 0x5A, 0x28));
    private static readonly Brush PanelBrush = Frozen(Color.FromRgb(0x1E, 0x28, 0x2D));
    private static readonly Brush Teal = Frozen(Color.FromRgb(0x0A, 0xC8, 0xB9));
    private static readonly Brush Danger = Frozen(Color.FromRgb(0xE8, 0x40, 0x57));
    private static readonly Brush DangerBackground = Frozen(Color.FromRgb(0x2A, 0x05, 0x0A));
    private static readonly Brush ScreenBackground = Frozen(Color.FromRgb(0x01, 0x0A, 0x13));

    private readonly SimulatorEngine _engine;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _timerText = CenteredText(string.Empty, 32, Cream, FontWeights.SemiBold);
    private ControlWindow? _control;

    public MainWindow(SimulatorEngine engine)
    {
        InitializeComponent();
        _engine = engine;
        _engine.StateChanged += (_, _) => Render();

        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        Loaded += OnLoaded;
        Closed += OnClosed;

        // O construtor com callback inicia o timer imediatamente.
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, OnTick, Dispatcher);
        Render();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _control = new ControlWindow(_engine)
        {
            Left = Left + ActualWidth + 8,
            Top = Top,
        };
        _control.Closed += (_, _) => _control = null;
        _control.Show();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _control?.Close();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _engine.Update();
        UpdateTimerText();
        _control?.Refresh(includeLog: false);
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Posição relativa ao Canvas: o Viewbox já desfaz escala, DPI e letterbox.
        // Cliques nas faixas pretas geram coordenadas fora de 0..1280 x 0..720.
        var position = e.GetPosition(Screen);
        var record = _engine.Click(position.X, position.Y);

        if (record.Outcome == ClickOutcome.GameplayViolation)
        {
            Render();
        }

        ShowClickMarker(position, record.Outcome);
        _control?.Refresh(includeLog: true);
        e.Handled = true;
    }

    private void Render()
    {
        Screen.Children.Clear();
        var state = _engine.State;
        Screen.Background = state.Phase == SimPhase.InProgress ? DangerBackground : ScreenBackground;

        switch (state.Phase)
        {
            case SimPhase.Lobby:
                AddText("SALA", 60, 40, Gold, FontWeights.Bold);
                AddText("Fila simulada - nenhum dado do League é usado aqui", 260, 22, Cream);
                AddRegionButton(RegionIds.FindMatch, "ENCONTRAR PARTIDA", enabled: true, fontSize: 24);
                break;

            case SimPhase.Matchmaking:
                AddText("PROCURANDO PARTIDA", 60, 40, Gold, FontWeights.Bold);
                AddTimer(110);
                AddRegionButton(RegionIds.CancelMatchmaking, "CANCELAR", enabled: true, fontSize: 20);
                break;

            case SimPhase.ReadyCheck:
                AddText("PARTIDA ENCONTRADA!", 220, 48, Gold, FontWeights.Bold);
                AddTimer(300);
                var accepted = state.ReadyCheck == ReadyCheckResponse.Accepted;
                AddRegionButton(RegionIds.Accept, accepted ? "ACEITO" : "ACEITAR!", enabled: !accepted, fontSize: 30);
                if (!accepted)
                {
                    AddRegionButton(RegionIds.Decline, "Recusar", enabled: true, fontSize: 18);
                }
                else
                {
                    AddText("Aguardando os outros jogadores...", 510, 20, Cream);
                }

                break;

            case SimPhase.ChampSelect:
                RenderChampSelect(state);
                break;

            case SimPhase.InProgress:
                AddText("PARTIDA EM ANDAMENTO", 220, 52, Danger, FontWeights.Bold);
                AddText("O agente deve bloquear todo input remoto nesta fase.", 320, 24, Cream);
                AddText(
                    $"Violações registradas: {_engine.GameplayViolationCount}",
                    400,
                    28,
                    _engine.GameplayViolationCount > 0 ? Danger : Teal,
                    FontWeights.SemiBold);
                AddText("Use \"Encerrar partida\" na janela de controle para voltar à sala.", 620, 18, Muted);
                break;
        }

        if (state.LastEvent is { } message)
        {
            AddText(message, 670, 20, Teal);
        }

        UpdateTimerText();
    }

    private void RenderChampSelect(SimulatorState state)
    {
        AddText("SELEÇÃO DE CAMPEÕES", 40, 36, Gold, FontWeights.Bold);

        if (state.Stage == ChampSelectStage.Finalization)
        {
            var picked = state.PickedChampion is { } p ? ChampionLabel(p) : "-";
            AddText($"Você escolheu {picked}", 260, 36, Cream, FontWeights.SemiBold);
            AddText("A partida começa em", 330, 22, Cream);
            AddTimer(370);
            return;
        }

        var banning = state.Stage == ChampSelectStage.Ban;
        AddText(banning ? "Sua vez de BANIR" : "Sua vez de ESCOLHER", 90, 24, banning ? Danger : Teal, FontWeights.SemiBold);
        AddTimer(120);

        for (var i = 0; i < SimLayout.ChampionCount; i++)
        {
            var cell = SimLayout.ChampionCell(i);
            var banned = state.BannedChampion == i;
            var hovered = state.HoveredChampion == i;

            var border = new Border
            {
                Width = cell.Width,
                Height = cell.Height,
                Background = PanelBrush,
                BorderBrush = hovered ? Gold : Muted,
                BorderThickness = new Thickness(hovered ? 4 : 1),
                Opacity = banned ? 0.35 : 1,
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = banned ? $"{ChampionLabel(i)}\nBANIDO" : ChampionLabel(i),
                    Foreground = banned ? Danger : Cream,
                    FontSize = 22,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Place(border, cell.X, cell.Y);
        }

        AddRegionButton(
            RegionIds.LockIn,
            banning ? "BANIR" : "CONFIRMAR",
            enabled: state.HoveredChampion is not null,
            fontSize: 22);

        if (!banning && state.BannedChampion is { } bannedIndex)
        {
            AddText($"Banido: {ChampionLabel(bannedIndex)}", 570, 18, Muted);
        }
    }

    private void AddRegionButton(string regionId, string label, bool enabled, double fontSize)
    {
        var state = _engine.State;
        var region = SimLayout.RegionsFor(state.Phase, state.Stage).Single(r => r.Id == regionId).Bounds;
        var border = new Border
        {
            Width = region.Width,
            Height = region.Height,
            Background = enabled ? PanelBrush : Brushes.Transparent,
            BorderBrush = enabled ? Gold : Muted,
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = label,
                Foreground = enabled ? Cream : Muted,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Place(border, region.X, region.Y);
    }

    private void AddText(string text, double top, double fontSize, Brush brush, FontWeight? weight = null)
    {
        Place(CenteredText(text, fontSize, brush, weight ?? FontWeights.Normal), 0, top);
    }

    private void AddTimer(double top)
    {
        Place(_timerText, 0, top);
    }

    private void UpdateTimerText()
    {
        _timerText.Text = _engine.Remaining is { } remaining
            ? Math.Ceiling(remaining.TotalSeconds).ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private void ShowClickMarker(Point position, ClickOutcome outcome)
    {
        if (!SimLayout.Bounds.Contains(position.X, position.Y))
        {
            return;
        }

        const double size = 28;
        var marker = new Ellipse
        {
            Width = size,
            Height = size,
            StrokeThickness = 3,
            Stroke = outcome switch
            {
                ClickOutcome.Applied => Teal,
                ClickOutcome.Ignored => Gold,
                ClickOutcome.GameplayViolation => Danger,
                _ => Muted,
            },
        };
        Canvas.SetLeft(marker, position.X - (size / 2));
        Canvas.SetTop(marker, position.Y - (size / 2));
        Overlay.Children.Add(marker);

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(700));
        fade.Completed += (_, _) => Overlay.Children.Remove(marker);
        marker.BeginAnimation(OpacityProperty, fade);
    }

    private void Place(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        Screen.Children.Add(element);
    }

    private static TextBlock CenteredText(string text, double fontSize, Brush brush, FontWeight weight) => new()
    {
        Text = text,
        Width = SimLayout.ReferenceWidth,
        TextAlignment = TextAlignment.Center,
        FontSize = fontSize,
        FontWeight = weight,
        Foreground = brush,
        IsHitTestVisible = false,
    };

    private static string ChampionLabel(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"C{index + 1:00}");

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
