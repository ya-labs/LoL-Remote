using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using LoLRemote.Agent.Platform;
using WinRT;

namespace LoLRemote.Agent.Capture;

/// <summary>
/// Criação do dispositivo Direct3D e do item de captura de uma janela.
/// Mesma implementação validada no spike de captura (ADR 0001).
/// </summary>
internal static unsafe class CaptureInterop
{
    private static readonly Guid IidGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid IidGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IidDxgiDevice = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

    public static IDirect3DDevice CreateDevice()
    {
        var hr = NativeMethods.D3D11CreateDevice(
            0, NativeMethods.D3DDriverTypeHardware, 0, NativeMethods.D3D11CreateDeviceBgraSupport,
            0, 0, NativeMethods.D3D11SdkVersion, out var d3dDevice, out _, out var context);
        if (hr < 0)
        {
            // Sem GPU utilizável: tenta o renderizador de software do Windows.
            Marshal.ThrowExceptionForHR(NativeMethods.D3D11CreateDevice(
                0, NativeMethods.D3DDriverTypeWarp, 0, NativeMethods.D3D11CreateDeviceBgraSupport,
                0, 0, NativeMethods.D3D11SdkVersion, out d3dDevice, out _, out context));
        }

        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, in IidDxgiDevice, out var dxgiDevice));
            try
            {
                Marshal.ThrowExceptionForHR(NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable));
                try
                {
                    return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
                }
                finally
                {
                    _ = Marshal.Release(inspectable);
                }
            }
            finally
            {
                _ = Marshal.Release(dxgiDevice);
            }
        }
        finally
        {
            _ = Marshal.Release(context);
            _ = Marshal.Release(d3dDevice);
        }
    }

    public static GraphicsCaptureItem CreateItemForWindow(nint hwnd)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        nint hstring;
        fixed (char* chars = className)
        {
            Marshal.ThrowExceptionForHR(NativeMethods.WindowsCreateString(chars, (uint)className.Length, out hstring));
        }

        nint interop;
        try
        {
            Marshal.ThrowExceptionForHR(NativeMethods.RoGetActivationFactory(hstring, in IidGraphicsCaptureItemInterop, out interop));
        }
        finally
        {
            _ = NativeMethods.WindowsDeleteString(hstring);
        }

        try
        {
            // IGraphicsCaptureItemInterop: slots 0-2 são IUnknown; CreateForWindow é o slot 3.
            var vtable = *(nint**)interop;
            var createForWindow = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)vtable[3];
            var iid = IidGraphicsCaptureItem;
            nint itemPointer;
            Marshal.ThrowExceptionForHR(createForWindow(interop, hwnd, &iid, &itemPointer));
            try
            {
                return GraphicsCaptureItem.FromAbi(itemPointer);
            }
            finally
            {
                _ = Marshal.Release(itemPointer);
            }
        }
        finally
        {
            _ = Marshal.Release(interop);
        }
    }
}
