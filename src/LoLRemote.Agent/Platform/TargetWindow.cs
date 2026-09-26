using System.Diagnostics;
using System.Runtime.InteropServices;
using LoLRemote.Agent.Core.Geometry;

namespace LoLRemote.Agent.Platform;

/// <summary>Critério de identificação da janela alvo.</summary>
/// <param name="Title">Título exato.</param>
/// <param name="ProcessName">Nome exato do processo.</param>
internal sealed record TargetSpec(string Title, string ProcessName)
{
    /// <summary>Único alvo aceito na v0.1.</summary>
    public static TargetSpec Simulator { get; } = new("LoL Remote Simulator", "LoLRemote.Simulator");
}

/// <summary>Estado da janela alvo em um instante.</summary>
/// <param name="Valid">A janela existe e ainda pertence ao processo validado.</param>
/// <param name="Minimized">Está minimizada.</param>
/// <param name="FrameOnScreen">Limites visíveis (DWM) na tela, iguais ao frame capturado.</param>
/// <param name="ClientOnScreen">Área cliente na tela.</param>
internal sealed record TargetState(bool Valid, bool Minimized, PixelRect FrameOnScreen, PixelRect ClientOnScreen)
{
    public PixelRect ClientAreaInFrame => CoordinateMapper.ClientAreaInFrame(FrameOnScreen, ClientOnScreen);
}

/// <summary>
/// Janela alvo validada. Falha fechada: só aceita exatamente uma janela visível
/// com título e processo esperados, e depois só a mesma janela e o mesmo PID.
/// </summary>
internal sealed unsafe class TargetWindow
{
    private TargetWindow(nint handle, uint processId, string processName)
    {
        Handle = handle;
        ProcessId = processId;
        ProcessName = processName;
    }

    public nint Handle { get; }

    public uint ProcessId { get; }

    public string ProcessName { get; }

    /// <summary>Procura o alvo; retorna null e o número de correspondências quando não é exatamente uma.</summary>
    public static TargetWindow? Find(TargetSpec spec, out int matches)
    {
        var found = new List<TargetWindow>();
        foreach (var hwnd in EnumerateTopLevel())
        {
            if (!NativeMethods.IsWindowVisible(hwnd) || IsCloaked(hwnd) || GetTitle(hwnd) != spec.Title)
            {
                continue;
            }

            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            var name = GetProcessName(pid);
            if (string.Equals(name, spec.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(new TargetWindow(hwnd, pid, name!));
            }
        }

        matches = found.Count;
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>Estado atual; inválido se a janela sumiu ou mudou de processo.</summary>
    public TargetState Describe()
    {
        if (!NativeMethods.IsWindow(Handle))
        {
            return new TargetState(false, false, default, default);
        }

        _ = NativeMethods.GetWindowThreadProcessId(Handle, out var pid);
        if (pid != ProcessId)
        {
            return new TargetState(false, false, default, default);
        }

        NativeMethods.Rect frame;
        if (NativeMethods.DwmGetWindowAttribute(Handle, NativeMethods.DwmwaExtendedFrameBounds, &frame, sizeof(NativeMethods.Rect)) != 0)
        {
            _ = NativeMethods.GetWindowRect(Handle, out frame);
        }

        var client = default(PixelRect);
        if (NativeMethods.GetClientRect(Handle, out var clientRect))
        {
            var origin = new NativeMethods.Point();
            if (NativeMethods.ClientToScreen(Handle, ref origin))
            {
                client = new PixelRect(origin.X, origin.Y, clientRect.Right - clientRect.Left, clientRect.Bottom - clientRect.Top);
            }
        }

        return new TargetState(
            true,
            NativeMethods.IsIconic(Handle),
            new PixelRect(frame.Left, frame.Top, frame.Right - frame.Left, frame.Bottom - frame.Top),
            client);
    }

    private static List<nint> EnumerateTopLevel()
    {
        var handles = new List<nint>();
        var gc = GCHandle.Alloc(handles);
        try
        {
            _ = NativeMethods.EnumWindows(&Collect, GCHandle.ToIntPtr(gc));
        }
        finally
        {
            gc.Free();
        }

        return handles;
    }

    [UnmanagedCallersOnly]
    private static int Collect(nint hwnd, nint state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add(hwnd);
        return 1;
    }

    private static string GetTitle(nint hwnd)
    {
        var length = NativeMethods.GetWindowTextLengthW(hwnd);
        if (length <= 0 || length > 512)
        {
            return string.Empty;
        }

        var buffer = stackalloc char[length + 1];
        var copied = NativeMethods.GetWindowTextW(hwnd, buffer, length + 1);
        return new string(buffer, 0, copied);
    }

    private static bool IsCloaked(nint hwnd)
    {
        int cloaked = 0;
        return NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DwmwaCloaked, &cloaked, sizeof(int)) == 0 && cloaked != 0;
    }

    private static string? GetProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
