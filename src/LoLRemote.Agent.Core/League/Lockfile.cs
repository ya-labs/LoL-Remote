using System.Globalization;

namespace LoLRemote.Agent.Core.League;

/// <summary>
/// Conteúdo do lockfile do League Client (e do simulador), no formato
/// <c>nome:pid:porta:senha:protocolo</c>. A senha só existe em memória.
/// </summary>
/// <param name="Name">Nome do processo que escreveu o arquivo.</param>
/// <param name="ProcessId">PID do processo.</param>
/// <param name="Port">Porta local da API.</param>
/// <param name="Password">Senha da autenticação Basic (usuário "riot").</param>
/// <param name="Protocol">"http" ou "https".</param>
public sealed record Lockfile(string Name, int ProcessId, int Port, string Password, string Protocol)
{
    /// <summary>Usuário da autenticação Basic da LCU.</summary>
    public const string User = "riot";

    /// <summary>Endereço base da API local.</summary>
    public Uri BaseAddress => new($"{Protocol}://127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}/");

    /// <summary>Não expõe a senha em logs acidentais.</summary>
    public override string ToString() => $"Lockfile {{ Name = {Name}, ProcessId = {ProcessId}, Port = {Port}, Protocol = {Protocol} }}";

    /// <summary>Lê o conteúdo do lockfile; qualquer formato inesperado é recusado.</summary>
    public static bool TryParse(string? content, out Lockfile? lockfile)
    {
        lockfile = null;
        if (string.IsNullOrWhiteSpace(content) || content.Length > 512)
        {
            return false;
        }

        var parts = content.Trim().Split(':');
        if (parts.Length != 5
            || parts[0].Length == 0
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535
            || parts[3].Length == 0
            || parts[4] is not ("http" or "https"))
        {
            return false;
        }

        lockfile = new Lockfile(parts[0], pid, port, parts[3], parts[4]);
        return true;
    }
}
