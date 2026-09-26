namespace LoLRemote.Agent.Core.Geometry;

/// <summary>
/// Posição do frame capturado dentro do vídeo transmitido. O frame é escalado
/// mantendo a proporção e centralizado; o que sobra vira faixa preta.
/// A regra é a mesma usada pelo compositor de vídeo.
/// </summary>
public sealed record VideoLayout
{
    private VideoLayout(int outputWidth, int outputHeight, int frameWidth, int frameHeight, double scale, PixelRect content)
    {
        OutputWidth = outputWidth;
        OutputHeight = outputHeight;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        Scale = scale;
        Content = content;
    }

    /// <summary>Largura do vídeo transmitido.</summary>
    public int OutputWidth { get; }

    /// <summary>Altura do vídeo transmitido.</summary>
    public int OutputHeight { get; }

    /// <summary>Largura do frame capturado.</summary>
    public int FrameWidth { get; }

    /// <summary>Altura do frame capturado.</summary>
    public int FrameHeight { get; }

    /// <summary>Fator de escala do frame para o vídeo.</summary>
    public double Scale { get; }

    /// <summary>Área do vídeo ocupada pelo frame (fora dela é faixa preta).</summary>
    public PixelRect Content { get; }

    /// <summary>Calcula o encaixe de um frame no vídeo de saída.</summary>
    public static VideoLayout Fit(int frameWidth, int frameHeight, int outputWidth, int outputHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

        var scale = Math.Min((double)outputWidth / frameWidth, (double)outputHeight / frameHeight);
        var width = Math.Clamp((int)(frameWidth * scale), 1, outputWidth);
        var height = Math.Clamp((int)(frameHeight * scale), 1, outputHeight);
        var content = new PixelRect((outputWidth - width) / 2, (outputHeight - height) / 2, width, height);
        return new VideoLayout(outputWidth, outputHeight, frameWidth, frameHeight, scale, content);
    }
}
