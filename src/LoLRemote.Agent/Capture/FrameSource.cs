using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace LoLRemote.Agent.Capture;

/// <summary>Último frame capturado, em BGRA.</summary>
internal sealed record CapturedFrame(byte[] Bgra, int Width, int Height, long CapturedAtMs);

/// <summary>
/// Captura contínua da janela alvo. Guarda somente o frame mais recente:
/// frames antigos são descartados, porque latência importa mais que fidelidade.
/// </summary>
internal sealed class FrameSource : IDisposable
{
    private const DirectXPixelFormat PixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
    private const int BufferCount = 2;

    private readonly IDirect3DDevice _device;
    private readonly GraphicsCaptureItem _item;
    private readonly Direct3D11CaptureFramePool _pool;
    private readonly GraphicsCaptureSession _session;
    private SizeInt32 _poolSize;
    private CapturedFrame? _latest;
    private long _frames;
    private volatile bool _closed;

    public FrameSource(nint hwnd)
    {
        _device = CaptureInterop.CreateDevice();
        try
        {
            _item = CaptureInterop.CreateItemForWindow(hwnd);
        }
        catch
        {
            _device.Dispose();
            throw;
        }

        _item.Closed += (_, _) => _closed = true;
        _poolSize = _item.Size;
        _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, PixelFormat, BufferCount, _poolSize);
        _pool.FrameArrived += OnFrameArrived;
        _session = _pool.CreateCaptureSession(_item);
        _session.IsCursorCaptureEnabled = false;
        try
        {
            _session.IsBorderRequired = false;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidCastException)
        {
            // Borda amarela continua visível; não impede o teste.
        }

        _session.StartCapture();
    }

    public CapturedFrame? Latest => Volatile.Read(ref _latest);

    public long FramesCaptured => Interlocked.Read(ref _frames);

    public bool Closed => _closed;

    public void Dispose()
    {
        _pool.FrameArrived -= OnFrameArrived;
        _session.Dispose();
        _pool.Dispose();
        _device.Dispose();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame is null)
        {
            return;
        }

        var size = frame.ContentSize;
        if (size.Width != _poolSize.Width || size.Height != _poolSize.Height)
        {
            _poolSize = size;
            sender.Recreate(_device, PixelFormat, BufferCount, size);
            return;
        }

        try
        {
            using var bitmap = SoftwareBitmap
                .CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Premultiplied)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyToBuffer(bytes.AsBuffer());
            Volatile.Write(ref _latest, new CapturedFrame(bytes, bitmap.PixelWidth, bitmap.PixelHeight, ServerClock.NowMs));
            Interlocked.Increment(ref _frames);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            // Frame perdido; o próximo substitui.
        }
    }
}
