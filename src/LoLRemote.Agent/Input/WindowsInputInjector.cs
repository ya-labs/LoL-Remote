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

    /// <summary>Clique esquerdo no ponto da área cliente.</summary>
    public InjectionResult Click(TargetWindow target, ClientPoint point) =>
        AtPoint(target, point, (x, y, position, inputs) =>
        {
            inputs[0] = MouseInput(x, y, position);
            inputs[1] = MouseInput(x, y, position | NativeMethods.MouseEventLeftDown);
            inputs[2] = MouseInput(x, y, position | NativeMethods.MouseEventLeftUp);
            return 3;
        });

    /// <summary>Rolagem no ponto da área cliente; <paramref name="notches"/> &gt; 0 rola o conteúdo para baixo.</summary>
    public InjectionResult Scroll(TargetWindow target, ClientPoint point, int notches) =>
        AtPoint(target, point, (x, y, position, inputs) =>
        {
            inputs[0] = MouseInput(x, y, position);
            inputs[1] = MouseInput(x, y, position | NativeMethods.MouseEventWheel, -notches * NativeMethods.WheelDelta);
            return 2;
        });

    /// <summary>Digita o texto onde estiver o foco da janela alvo (Unicode, sem layout de teclado).</summary>
    public InjectionResult TypeText(TargetWindow target, string text)
    {
        lock (_gate)
        {
            if (Prepare(target) is { } problem)
            {
                return problem;
            }

            var count = text.Length * 2;
            var inputs = stackalloc NativeMethods.Input[count];
            for (var i = 0; i < text.Length; i++)
            {
                inputs[i * 2] = KeyInput(0, text[i], NativeMethods.KeyEventUnicode);
                inputs[(i * 2) + 1] = KeyInput(0, text[i], NativeMethods.KeyEventUnicode | NativeMethods.KeyEventKeyUp);
            }

            return Send(inputs, count);
        }
    }

    /// <summary>Pressiona e solta uma tecla especial na janela alvo.</summary>
    public InjectionResult Press(TargetWindow target, SpecialKey key)
    {
        ushort virtualKey = key switch
        {
            SpecialKey.Enter => 0x0D,
            SpecialKey.Backspace => 0x08,
            SpecialKey.Escape => 0x1B,
            SpecialKey.Tab => 0x09,
            _ => 0,
        };
        if (virtualKey == 0)
        {
            return InjectionResult.TargetObscured;
        }

        lock (_gate)
        {
            if (Prepare(target) is { } problem)
            {
                return problem;
            }

            var inputs = stackalloc NativeMethods.Input[2];
            inputs[0] = KeyInput(virtualKey, 0, 0);
            inputs[1] = KeyInput(virtualKey, 0, NativeMethods.KeyEventKeyUp);
            return Send(inputs, 2);
        }
    }

    private delegate int FillPointer(int x, int y, uint position, NativeMethods.Input* inputs);

    /// <summary>Valida a janela, restaura se escondida e traz para o primeiro plano.</summary>
    private InjectionResult? Prepare(TargetWindow target)
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

        if (!EnsureForeground(target.Handle))
        {
            LastDiagnostic = Diagnose(target.Handle, NativeMethods.GetForegroundWindow());
            return InjectionResult.TargetObscured;
        }

        return null;
    }

    private InjectionResult AtPoint(TargetWindow target, ClientPoint point, FillPointer fill)
    {
        lock (_gate)
        {
            if (Prepare(target) is { } problem)
            {
                return problem;
            }

            var screen = new NativeMethods.Point { X = point.X, Y = point.Y };
            if (!NativeMethods.ClientToScreen(target.Handle, ref screen))
            {
                return InjectionResult.TargetUnavailable;
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
            var inputs = stackalloc NativeMethods.Input[5];
            var count = fill(x, y, position, inputs);

            // Depois do clique ou da rolagem, tira o cursor de cima do cliente:
            // senão o cliente mostra o conteúdo de "passar o mouse" (por exemplo,
            // o vídeo de habilidades do campeão) como se alguém estivesse apontando.
            if (ParkingSpot(target, desktop) is { } park)
            {
                var (parkX, parkY) = CoordinateMapper.ToAbsoluteInput(park.X, park.Y, desktop);
                inputs[count++] = MouseInput(parkX, parkY, position);
            }

            return Send(inputs, count);
        }
    }

    /// <summary>
    /// Ponto da tela logo fora da janela alvo (à esquerda, à direita, acima ou
    /// abaixo), dentro da área de trabalho. Null se a janela ocupa a tela toda.
    /// </summary>
    private static NativeMethods.Point? ParkingSpot(TargetWindow target, PixelRect desktop)
    {
        var frame = target.Describe().FrameOnScreen;
        var middleY = frame.Y + (frame.Height / 2);
        var middleX = frame.X + (frame.Width / 2);
        (int X, int Y)[] candidates =
        [
            (frame.X - 8, middleY),
            (frame.Right + 8, middleY),
            (middleX, frame.Y - 8),
            (middleX, frame.Bottom + 8),
        ];

        foreach (var (x, y) in candidates)
        {
            if (!desktop.Contains(x, y))
            {
                continue;
            }

            var point = new NativeMethods.Point { X = x, Y = y };
            var hit = NativeMethods.WindowFromPoint(point);
            if (hit == 0 || NativeMethods.GetAncestor(hit, NativeMethods.GaRoot) != target.Handle)
            {
                return point;
            }
        }

        return null;
    }

    private InjectionResult Send(NativeMethods.Input* inputs, int count)
    {
        var sent = NativeMethods.SendInput((uint)count, inputs, sizeof(NativeMethods.Input));
        _lastInjectedTick = NativeMethods.GetTickCount();

        // Menos eventos que o pedido: o Windows bloqueou (por exemplo, janela com privilégio maior).
        return sent == count ? InjectionResult.Clicked : InjectionResult.TargetObscured;
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

    private static NativeMethods.Input MouseInput(int x, int y, uint flags, int wheel = 0) => new()
    {
        Type = NativeMethods.InputMouse,
        Data = new NativeMethods.InputUnion
        {
            Mouse = new NativeMethods.MouseInput
            {
                Dx = x,
                Dy = y,
                MouseData = unchecked((uint)wheel),
                Flags = flags,
                ExtraInfo = NativeMethods.InjectedMarker,
            },
        },
    };

    private static NativeMethods.Input KeyInput(ushort virtualKey, ushort scan, uint flags) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Data = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                VirtualKey = virtualKey,
                Scan = scan,
                Flags = flags,
                ExtraInfo = NativeMethods.InjectedMarker,
            },
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
