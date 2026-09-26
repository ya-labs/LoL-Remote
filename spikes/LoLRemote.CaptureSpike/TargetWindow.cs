using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LoLRemote.CaptureSpike;

/// <summary>Janela de nível superior que corresponde ao alvo.</summary>
internal sealed record WindowCandidate(nint Handle, uint ProcessId, string ProcessName);

/// <summary>Estado de uma janela em um instante.</summary>
internal sealed record WindowSnapshot(
    bool Exists,
    bool Minimized,
    NativeMethods.Rect Bounds,
    uint Dpi,
    string Monitor,
    bool PrimaryMonitor)
{
    public int ScalePercent => (int)Math.Round(Dpi * 100 / 96.0);

    public string Describe() => !Exists
        ? "janela não existe mais"
        : $"{(Minimized ? "minimizada" : "normal")}, {Bounds.Width}x{Bounds.Height} px, escala {ScalePercent}%, monitor {Monitor}{(PrimaryMonitor ? " (principal)" : string.Empty)}";
}

/// <summary>
/// Localiza e descreve a janela alvo. A regra é falhar fechado: só aceita
/// exatamente uma janela visível com o título e o processo esperados.
/// </summary>
internal static unsafe class TargetWindow
{
    public static List<WindowCandidate> Find(string exactTitle, string processName)
    {
        var matches = new List<WindowCandidate>();
        foreach (var hwnd in EnumerateTopLevel())
        {
            if (!NativeMethods.IsWindowVisible(hwnd) || IsCloaked(hwnd) || GetTitle(hwnd) != exactTitle)
            {
                continue;
            }

            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            var name = GetProcessName(pid);
            if (string.Equals(name, processName, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new WindowCandidate(hwnd, pid, name!));
            }
        }

        return matches;
    }

    /// <summary>Janelas visíveis cujo título contém o texto, para ajudar a achar o alvo.</summary>
    public static List<string> ListContaining(string text)
    {
        var result = new List<string>();
        foreach (var hwnd in EnumerateTopLevel())
        {
            var title = GetTitle(hwnd);
            if (!NativeMethods.IsWindowVisible(hwnd) || !title.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            result.Add($"\"{title}\" (processo {GetProcessName(pid) ?? "?"})");
        }

        return result;
    }

    public static WindowSnapshot Snapshot(nint hwnd)
    {
        if (!NativeMethods.IsWindow(hwnd))
        {
            return new WindowSnapshot(false, false, default, 0, "-", false);
        }

        NativeMethods.Rect bounds;
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DwmwaExtendedFrameBounds, &bounds, sizeof(NativeMethods.Rect)) != 0)
        {
            _ = NativeMethods.GetWindowRect(hwnd, out bounds);
        }

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MonitorInfoEx { Size = (uint)sizeof(NativeMethods.MonitorInfoEx) };
        var monitorName = "?";
        var primary = false;
        if (NativeMethods.GetMonitorInfoW(monitor, ref info))
        {
            monitorName = new string(info.Device);
            primary = (info.Flags & NativeMethods.MonitorInfoPrimary) != 0;
        }

        return new WindowSnapshot(
            true,
            NativeMethods.IsIconic(hwnd),
            bounds,
            NativeMethods.GetDpiForWindow(hwnd),
            monitorName,
            primary);
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
        var handles = (List<nint>)GCHandle.FromIntPtr(state).Target!;
        handles.Add(hwnd);
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
