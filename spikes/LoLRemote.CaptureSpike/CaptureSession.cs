using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace LoLRemote.CaptureSpike;

/// <summary>
/// Captura de uma única janela com Windows.Graphics.Capture. Nunca captura o
/// monitor: se a janela deixar de existir, a sessão apenas para de receber frames.
/// </summary>
internal sealed class CaptureSession : IDisposable
{
    private const DirectXPixelFormat PixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
    private const int BufferCount = 2;

    private readonly Lock _gate = new();
    private readonly IDirect3DDevice _device;
    private readonly GraphicsCaptureItem _item;
    private readonly Direct3D11CaptureFramePool _pool;
    private readonly GraphicsCaptureSession _session;
    private readonly SizeInt32 _initialSize;
    private SizeInt32 _poolSize;
    private SizeInt32 _lastContentSize;
    private TaskCompletionSource<SoftwareBitmap?>? _sampleRequest;
    private long _frames;
    private int _poolRecreations;
    private volatile bool _closed;

    private CaptureSession(IDirect3DDevice device, GraphicsCaptureItem item)
    {
        _device = device;
        _item = item;
        _initialSize = item.Size;
        _poolSize = item.Size;
        _lastContentSize = item.Size;
        _item.Closed += (_, _) => _closed = true;

        _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, PixelFormat, BufferCount, _poolSize);
        _pool.FrameArrived += OnFrameArrived;
        _session = _pool.CreateCaptureSession(_item);
        _session.IsCursorCaptureEnabled = false;
        BorderDisabled = TryDisableBorder(_session);
        _session.StartCapture();
    }

    /// <summary>Tamanho da janela quando a captura começou.</summary>
    public SizeInt32 InitialSize => _initialSize;

    /// <summary>Se o Windows aceitou remover a borda amarela de captura.</summary>
    public bool BorderDisabled { get; }

    /// <summary>Total de frames recebidos.</summary>
    public long Frames => Interlocked.Read(ref _frames);

    /// <summary>Quantas vezes o pool foi recriado por mudança de tamanho.</summary>
    public int PoolRecreations => Volatile.Read(ref _poolRecreations);

    /// <summary>Indica que o Windows encerrou a captura (janela fechada).</summary>
    public bool Closed => _closed;

    /// <summary>Tamanho do conteúdo do último frame.</summary>
    public SizeInt32 LastContentSize
    {
        get
        {
            lock (_gate)
            {
                return _lastContentSize;
            }
        }
    }

    public static bool IsSupported() => GraphicsCaptureSession.IsSupported();

    public static CaptureSession Start(nint hwnd)
    {
        var device = CaptureInterop.CreateDevice();
        try
        {
            return new CaptureSession(device, CaptureInterop.CreateItemForWindow(hwnd));
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    /// <summary>Copia o próximo frame recebido, ou retorna null se nenhum chegar a tempo.</summary>
    public async Task<SoftwareBitmap?> TakeSampleAsync(TimeSpan timeout)
    {
        var request = new TaskCompletionSource<SoftwareBitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _sampleRequest = request;
        }

        var finished = await Task.WhenAny(request.Task, Task.Delay(timeout)).ConfigureAwait(false);
        if (finished != request.Task)
        {
            lock (_gate)
            {
                if (_sampleRequest == request)
                {
                    _sampleRequest = null;
                }
            }

            return null;
        }

        return await request.Task.ConfigureAwait(false);
    }

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

        Interlocked.Increment(ref _frames);
        var size = frame.ContentSize;
        TaskCompletionSource<SoftwareBitmap?>? request = null;
        lock (_gate)
        {
            _lastContentSize = size;
            if (size.Width == _poolSize.Width && size.Height == _poolSize.Height)
            {
                request = _sampleRequest;
                _sampleRequest = null;
            }
        }

        if (size.Width != _poolSize.Width || size.Height != _poolSize.Height)
        {
            // A janela mudou de tamanho: recria os buffers para o novo tamanho.
            // A amostra pendente fica para o próximo frame, já no tamanho certo.
            _poolSize = size;
            Interlocked.Increment(ref _poolRecreations);
            sender.Recreate(_device, PixelFormat, BufferCount, size);
            return;
        }

        if (request is null)
        {
            return;
        }

        try
        {
            var bitmap = SoftwareBitmap
                .CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Premultiplied)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            request.TrySetResult(bitmap);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
        {
            request.TrySetException(ex);
        }
    }

    private static bool TryDisableBorder(GraphicsCaptureSession session)
    {
        try
        {
            session.IsBorderRequired = false;
            return !session.IsBorderRequired;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidCastException)
        {
            return false;
        }
    }
}
