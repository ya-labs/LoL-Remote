namespace LoLRemote.Agent.Core.Geometry;

/// <summary>Retângulo em pixels inteiros; bordas direita e inferior exclusivas.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    /// <summary>Borda direita (exclusiva).</summary>
    public int Right => X + Width;

    /// <summary>Borda inferior (exclusiva).</summary>
    public int Bottom => Y + Height;

    /// <summary>Indica se o retângulo tem área.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Indica se o ponto está dentro.</summary>
    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
}

/// <summary>Ponto normalizado sobre o vídeo recebido: 0..1 nos dois eixos.</summary>
public readonly record struct NormalizedPoint(double X, double Y)
{
    /// <summary>Indica se as coordenadas são números finitos entre 0 e 1.</summary>
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && X is >= 0 and <= 1 && Y is >= 0 and <= 1;
}

/// <summary>Ponto em pixels físicos relativo à área cliente da janela alvo.</summary>
public readonly record struct ClientPoint(int X, int Y);
