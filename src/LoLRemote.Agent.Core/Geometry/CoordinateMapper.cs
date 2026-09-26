namespace LoLRemote.Agent.Core.Geometry;

/// <summary>Motivo de um toque não poder ser convertido em clique.</summary>
public enum MappingFailure
{
    /// <summary>Conversão possível.</summary>
    None,

    /// <summary>Coordenadas fora de 0..1 ou não numéricas.</summary>
    InvalidCoordinates,

    /// <summary>Toque na faixa preta, fora da imagem da janela.</summary>
    Letterbox,

    /// <summary>Toque na barra de título ou nas bordas da janela.</summary>
    OutsideClientArea,
}

/// <summary>
/// Converte um toque no vídeo em pixel da área cliente da janela alvo.
/// O frame capturado inclui barra de título e bordas (ADR 0001); toques
/// nessas regiões são recusados em vez de aproximados.
/// </summary>
public static class CoordinateMapper
{
    /// <summary>Converte o toque ou informa por que não é possível.</summary>
    /// <param name="touch">Toque normalizado sobre o vídeo.</param>
    /// <param name="layout">Encaixe do frame no vídeo.</param>
    /// <param name="clientArea">Área cliente dentro do frame capturado, em pixels do frame.</param>
    /// <param name="point">Pixel da área cliente, quando a conversão é possível.</param>
    public static MappingFailure TryMap(NormalizedPoint touch, VideoLayout layout, PixelRect clientArea, out ClientPoint point)
    {
        ArgumentNullException.ThrowIfNull(layout);
        point = default;

        if (!touch.IsValid)
        {
            return MappingFailure.InvalidCoordinates;
        }

        var videoX = touch.X * layout.OutputWidth;
        var videoY = touch.Y * layout.OutputHeight;
        if (!layout.Content.Contains(videoX, videoY))
        {
            return MappingFailure.Letterbox;
        }

        var frameX = (videoX - layout.Content.X) / layout.Scale;
        var frameY = (videoY - layout.Content.Y) / layout.Scale;
        if (clientArea.IsEmpty || !clientArea.Contains(frameX, frameY))
        {
            return MappingFailure.OutsideClientArea;
        }

        point = new ClientPoint(
            Math.Min(clientArea.Width - 1, (int)(frameX - clientArea.X)),
            Math.Min(clientArea.Height - 1, (int)(frameY - clientArea.Y)));
        return MappingFailure.None;
    }

    /// <summary>
    /// Posição da área cliente dentro do frame capturado. O frame corresponde aos
    /// limites visíveis da janela (DWM); a área cliente fica dentro deles.
    /// </summary>
    /// <param name="frameOnScreen">Limites da janela na tela, iguais ao frame capturado.</param>
    /// <param name="clientOnScreen">Área cliente na tela.</param>
    public static PixelRect ClientAreaInFrame(PixelRect frameOnScreen, PixelRect clientOnScreen)
    {
        var left = Math.Max(clientOnScreen.X, frameOnScreen.X);
        var top = Math.Max(clientOnScreen.Y, frameOnScreen.Y);
        var right = Math.Min(clientOnScreen.Right, frameOnScreen.Right);
        var bottom = Math.Min(clientOnScreen.Bottom, frameOnScreen.Bottom);
        return right <= left || bottom <= top
            ? default
            : new PixelRect(left - frameOnScreen.X, top - frameOnScreen.Y, right - left, bottom - top);
    }

    /// <summary>
    /// Converte um pixel da tela (coordenadas físicas da área de trabalho virtual)
    /// para a escala 0..65535 usada pelo SendInput com MOUSEEVENTF_VIRTUALDESK.
    /// </summary>
    /// <param name="screenX">X em pixels físicos.</param>
    /// <param name="screenY">Y em pixels físicos.</param>
    /// <param name="virtualDesktop">Área de trabalho virtual (todos os monitores).</param>
    public static (int X, int Y) ToAbsoluteInput(int screenX, int screenY, PixelRect virtualDesktop)
    {
        if (virtualDesktop.Width < 2 || virtualDesktop.Height < 2 || !virtualDesktop.Contains(screenX, screenY))
        {
            throw new ArgumentOutOfRangeException(nameof(virtualDesktop), "Ponto fora da área de trabalho virtual.");
        }

        var x = (int)Math.Round((screenX - virtualDesktop.X) * 65535.0 / (virtualDesktop.Width - 1));
        var y = (int)Math.Round((screenY - virtualDesktop.Y) * 65535.0 / (virtualDesktop.Height - 1));
        return (x, y);
    }
}
