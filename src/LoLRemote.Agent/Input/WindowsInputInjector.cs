using LoLRemote.Agent.Core.Geometry;
using LoLRemote.Agent.Core.Input;
using LoLRemote.Agent.Platform;

namespace LoLRemote.Agent.Input;

/// <summary>Resultado de uma tentativa de clique.</summary>
internal enum InjectionResult
{
    Clicked,
    TargetUnavailable,
    TargetMinimized,
    TargetObscured,
}

/// <summary>
/// Injeta um clique esquerdo na área cliente da janela alvo com SendInput.
/// Antes de injetar, traz a janela para o primeiro plano e confirma que o
/// ponto da tela pertence a ela; se não pertencer, não clica.
/// </summary>
internal sealed unsafe class WindowsInputInjector
{
    private readonly Lock _gate = new();
    private uint? _lastInjectedTick;

    /// <summary>Contador do último clique injetado, para descontar da atividade local.</summary>
    public uint? LastInjectedTick
    {
        get
        {
            lock (_gate)
            {
                return _lastInjectedTick;
            }
        }
    }

    /// <summary>Descrição do último bloqueio por janela na frente, sem coordenadas.</summary>
    public string? LastDiagnostic { get; private set; }

    /// <summary>Indica se alguém usou mouse ou teclado no PC nos últimos segundos.</summary>
    public bool LocalActivityDetected()
    {
        var info = new NativeMethods.LastInputInfo { Size = (uint)sizeof(NativeMethods.LastInputInfo) };
        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return true;
        }

        return LocalActivity.IsActive(NativeMethods.GetTickCount(), info.Time, LastInjectedTick);
    }

    public InjectionResult Click(TargetWindow target, ClientPoint point)
    {
        lock (_gate)
        {
            if (!target.Describe().Valid)
            {
                return InjectionResult.TargetUnavailable;
            }

            if (NativeMethods.IsIconic(target.Handle))
            {
                return InjectionResult.TargetMinimized;
            }

            // O botão de minimizar do League Client não usa o estado minimizado do
            // Windows. Se a janela ou a dona dela estiver escondida, restaura antes.
            RestoreIfHidden(target.Handle);

            var screen = new NativeMethods.Point { X = point.X, Y = point.Y };
            if (!NativeMethods.ClientToScreen(target.Handle, ref screen))
            {
                return InjectionResult.TargetUnavailable;
            }

            if (!EnsureForeground(target.Handle))
            {
                LastDiagnostic = Diagnose(target.Handle, NativeMethods.GetForegroundWindow());
                return InjectionResult.TargetObscured;
            }

            var hit = NativeMethods.WindowFromPoint(screen);
            if (hit == 0 || NativeMethods.GetAncestor(hit, NativeMethods.GaRoot) != target.Handle)
            {
                LastDiagnostic = Diagnose(target.Handle, hit);
                return InjectionResult.TargetObscured;
            }

            var desktop = new PixelRect(
                NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen),
                NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen),
                NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen),
                NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen));
            if (desktop.Width < 2 || desktop.Height < 2 || !desktop.Contains(screen.X, screen.Y))
            {
                return InjectionResult.TargetObscured;
            }

            var (x, y) = CoordinateMapper.ToAbsoluteInput(screen.X, screen.Y, desktop);
            const uint position = NativeMethods.MouseEventAbsolute | NativeMethods.MouseEventVirtualDesk | NativeMethods.MouseEventMove;
            var inputs = stackalloc NativeMethods.Input[3];
            inputs[0] = MouseInput(x, y, position);
            inputs[1] = MouseInput(x, y, position | NativeMethods.MouseEventLeftDown);
            inputs[2] = MouseInput(x, y, position | NativeMethods.MouseEventLeftUp);

            var sent = NativeMethods.SendInput(3, inputs, sizeof(NativeMethods.Input));
            _lastInjectedTick = NativeMethods.GetTickCount();

            // Menos de 3 eventos: o Windows bloqueou (por exemplo, janela com privilégio maior).
            return sent == 3 ? InjectionResult.Clicked : InjectionResult.TargetObscured;
        }
    }

    private static void RestoreIfHidden(nint hwnd)
    {
        var owner = NativeMethods.GetWindow(hwnd, NativeMethods.GwOwner);
        if (owner != 0 && NativeMethods.IsIconic(owner))
        {
            _ = NativeMethods.ShowWindow(owner, NativeMethods.SwRestore);
        }

        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd))
        {
            _ = NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
        }
    }

    private static string Diagnose(nint target, nint other)
    {
        var owner = NativeMethods.GetWindow(target, NativeMethods.GwOwner);
        var otherRoot = other == 0 ? 0 : NativeMethods.GetAncestor(other, NativeMethods.GaRoot);
        var otherProcess = "nenhuma";
        if (otherRoot != 0)
        {
            _ = NativeMethods.GetWindowThreadProcessId(otherRoot, out var pid);
            otherProcess = TargetWindow.ProcessNameOf(pid) ?? "?";
        }

        return $"alvo visível={NativeMethods.IsWindowVisible(target)}, minimizado={NativeMethods.IsIconic(target)}, " +
            $"dona={(owner == 0 ? "nenhuma" : $"minimizada={NativeMethods.IsIconic(owner)}")}, " +
            $"janela no caminho: {otherProcess}";
    }

    private static NativeMethods.Input MouseInput(int x, int y, uint flags) => new()
    {
        Type = NativeMethods.InputMouse,
        Mouse = new NativeMethods.MouseInput
        {
            Dx = x,
            Dy = y,
            Flags = flags,
            ExtraInfo = NativeMethods.InjectedMarker,
        },
    };

    private static bool EnsureForeground(nint hwnd)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground == hwnd)
            {
                return true;
            }

            var foregroundThread = foreground == 0 ? 0 : NativeMethods.GetWindowThreadProcessId(foreground, out _);
            var currentThread = NativeMethods.GetCurrentThreadId();
            var attached = foregroundThread != 0
                && foregroundThread != currentThread
                && NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                _ = NativeMethods.BringWindowToTop(hwnd);
                _ = NativeMethods.SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached)
                {
                    _ = NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
                }
            }

            Thread.Sleep(30);
        }

        return NativeMethods.GetForegroundWindow() == hwnd;
    }
}
