using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace LoLRemote.CaptureSpike;

/// <summary>Etapa do roteiro guiado.</summary>
internal sealed record SpikeStep(string Id, string Title, string Instruction);

/// <summary>Medições de uma etapa.</summary>
internal sealed record StepResult(
    SpikeStep Step,
    bool Skipped,
    WindowSnapshot? Window,
    double Fps,
    int PoolRecreations,
    string? SampleFile,
    int SampleWidth,
    int SampleHeight,
    double BlackRatio,
    bool CaptureClosed,
    string Verdict);

/// <summary>
/// Roteiro guiado: a pessoa mexe no simulador conforme as instruções e o spike
/// mede se a captura continua correta em cada situação.
/// </summary>
internal static class GuidedSpike
{
    // Alvo padrão: simulador. Para o cliente real (só captura, sem cliques):
    // --title "League of Legends" --process LeagueClientUx
    private static string TargetTitle = "LoL Remote Simulator";
    private static string TargetProcess = "LoLRemote.Simulator";
    private const int SizeTolerance = 2;
    private static readonly TimeSpan MeasureDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(2);

    private static readonly SpikeStep[] Steps =
    [
        new("visivel", "Janela visível", "Deixe o simulador visível na tela."),
        new("coberta", "Janela coberta", "Coloque outra janela (ex.: o navegador) TOTALMENTE por cima do simulador."),
        new("redimensionada", "Janela redimensionada", "Traga o simulador de volta e mude o tamanho dele arrastando a borda (deixe bem mais largo ou mais alto)."),
        new("outro-monitor", "Outro monitor", "Arraste o simulador para o outro monitor. Se tiver só um monitor, digite P e Enter para pular."),
        new("minimizada", "Janela minimizada", "Minimize o simulador (botão _ da janela)."),
        new("restaurada", "Janela restaurada", "Restaure o simulador clicando nele na barra de tarefas."),
        new("fechada", "Janela fechada", "Feche o simulador (o X da janela principal)."),
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--title":
                    TargetTitle = args[i + 1];
                    break;
                case "--process":
                    TargetProcess = args[i + 1];
                    break;
                default:
                    Console.WriteLine("Uso: [--title <título exato>] [--process <nome do processo>]");
                    return 1;
            }
        }

        Console.WriteLine("LoL Remote - spike de captura de janela");
        Console.WriteLine(new string('=', 40));
        Console.WriteLine();

        if (!CaptureSession.IsSupported())
        {
            Console.WriteLine("ERRO: este Windows não suporta Windows.Graphics.Capture.");
            return 2;
        }

        var candidates = TargetWindow.Find(TargetTitle, TargetProcess);
        if (candidates.Count != 1)
        {
            Console.WriteLine(candidates.Count == 0
                ? $"ERRO: janela \"{TargetTitle}\" do processo {TargetProcess} não encontrada."
                : $"ERRO: {candidates.Count} janelas \"{TargetTitle}\" abertas. Deixe só uma.");
            Console.WriteLine("Simulador: dotnet run --project src/LoLRemote.Simulator -- --fast");
            foreach (var hint in TargetWindow.ListContaining("League").Concat(TargetWindow.ListContaining("LoL")).Distinct())
            {
                Console.WriteLine($"  janela visível: {hint}");
            }
            return 3;
        }

        var target = candidates[0];
        var outputDir = Path.Combine(
            Environment.CurrentDirectory,
            "spike-output",
            "captura-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"Simulador encontrado: processo {target.ProcessName} (PID {target.ProcessId}).");
        Console.WriteLine($"Estado inicial: {TargetWindow.Snapshot(target.Handle).Describe()}");

        using var session = CaptureSession.Start(target.Handle);
        Console.WriteLine($"Captura iniciada. Borda amarela removida: {(session.BorderDisabled ? "sim" : "não")}.");
        Console.WriteLine();
        Console.WriteLine("Em cada etapa: faça o que foi pedido e depois volte aqui e pressione Enter.");
        Console.WriteLine("O spike mede por 5 segundos e salva uma imagem de amostra.");

        var results = new List<StepResult>();
        var seenRecreations = 0;
        for (var i = 0; i < Steps.Length; i++)
        {
            var step = Steps[i];
            Console.WriteLine();
            Console.WriteLine($"[{i + 1}/{Steps.Length}] {step.Title}");
            Console.WriteLine($"  {step.Instruction}");
            Console.Write("  Pressione Enter quando estiver pronto (P + Enter para pular): ");
            var answer = Console.ReadLine();
            if (string.Equals(answer?.Trim(), "p", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new StepResult(step, true, null, 0, 0, null, 0, 0, 0, session.Closed, "PULADA"));
                Console.WriteLine("  Etapa pulada.");
                continue;
            }

            // Mudanças de tamanho contam desde a etapa anterior: a pessoa
            // redimensiona antes de apertar Enter, não durante a medição.
            var result = await MeasureAsync(session, target.Handle, step, i + 1, outputDir, seenRecreations, results).ConfigureAwait(false);
            seenRecreations = session.PoolRecreations;
            results.Add(result);
            Console.WriteLine($"  {result.Fps:0.0} frames/s | janela: {result.Window?.Describe()}");
            Console.WriteLine($"  Resultado: {result.Verdict}");
        }

        var reportPath = Path.Combine(outputDir, "relatorio.md");
        await File.WriteAllLinesAsync(reportPath, BuildReport(target, session, results)).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine("Concluído. Relatório e imagens em:");
        Console.WriteLine($"  {outputDir}");
        Console.WriteLine("Avise o Claude que terminou: ele lê o relatório direto da pasta.");
        return 0;
    }

    private static async Task<StepResult> MeasureAsync(
        CaptureSession session,
        nint hwnd,
        SpikeStep step,
        int number,
        string outputDir,
        int recreationsBefore,
        List<StepResult> previous)
    {
        var framesBefore = session.Frames;
        Console.Write("  Medindo");
        for (var s = 0; s < MeasureDuration.TotalSeconds; s++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            Console.Write('.');
        }

        Console.WriteLine();
        var fps = (session.Frames - framesBefore) / MeasureDuration.TotalSeconds;
        var recreations = session.PoolRecreations - recreationsBefore;
        var window = TargetWindow.Snapshot(hwnd);

        string? sampleFile = null;
        int width = 0, height = 0;
        var blackRatio = double.NaN;
        using (var sample = await session.TakeSampleAsync(SampleTimeout).ConfigureAwait(false))
        {
            if (sample is not null)
            {
                width = sample.PixelWidth;
                height = sample.PixelHeight;
                blackRatio = BlackRatio(sample);
                sampleFile = $"{number:00}-{step.Id}.png";
                await SavePngAsync(sample, Path.Combine(outputDir, sampleFile)).ConfigureAwait(false);
            }
        }

        var verdict = Judge(step, window, fps, recreations, sampleFile is not null, width, height, blackRatio, session.Closed, previous);
        return new StepResult(step, false, window, fps, recreations, sampleFile, width, height, blackRatio, session.Closed, verdict);
    }

    private static string Judge(
        SpikeStep step,
        WindowSnapshot window,
        double fps,
        int recreations,
        bool hasSample,
        int width,
        int height,
        double blackRatio,
        bool closed,
        List<StepResult> previous)
    {
        var sizeMatches = window.Exists
            && Math.Abs(width - window.Bounds.Width) <= SizeTolerance
            && Math.Abs(height - window.Bounds.Height) <= SizeTolerance;
        var imageOk = fps > 0 && hasSample && sizeMatches;
        var sizeNote = hasSample && !sizeMatches
            ? $" (amostra {width}x{height} difere da janela {window.Bounds.Width}x{window.Bounds.Height})"
            : string.Empty;

        switch (step.Id)
        {
            case "minimizada":
                if (!hasSample)
                {
                    return "OK: nenhuma imagem enquanto minimizada. O agente deve tratar isso como 'sem imagem' e bloquear input.";
                }

                return $"ATENÇÃO: imagens recebidas com a janela minimizada ({blackRatio:P0} de pixels pretos). Ver amostra.";

            case "fechada":
                if (window.Exists || fps > 0)
                {
                    return "FALHA: a janela ainda existe ou frames continuam chegando.";
                }

                return "OK: janela não existe e nenhum frame chega, sem cair para o monitor. " +
                    $"Evento Closed do Windows disparou: {(closed ? "sim" : "não")}.";

            case "redimensionada":
                if (!imageOk)
                {
                    return "FALHA: sem imagem correta após redimensionar" + sizeNote + ".";
                }

                return recreations > 0
                    ? $"OK: a captura acompanhou o novo tamanho ({width}x{height})."
                    : $"ATENÇÃO: imagem OK ({width}x{height}), mas nenhuma mudança de tamanho foi detectada. A janela foi redimensionada?";

            case "outro-monitor":
                var baseline = previous.FirstOrDefault(r => !r.Skipped && r.Window is { Exists: true })?.Window;
                if (!imageOk)
                {
                    return "FALHA: sem imagem correta no outro monitor" + sizeNote + ".";
                }

                if (baseline is not null && baseline.Monitor == window.Monitor)
                {
                    return $"ATENÇÃO: imagem OK, mas a janela continua no mesmo monitor ({window.Monitor}).";
                }

                return $"OK: imagem correta no monitor {window.Monitor} com escala {window.ScalePercent}%" +
                    (baseline is not null ? $" (antes: {baseline.Monitor}, {baseline.ScalePercent}%)." : ".");

            default:
                return imageOk
                    ? $"OK: imagem recebida só da janela ({width}x{height})."
                    : "FALHA: " + (fps <= 0 ? "nenhum frame recebido" : !hasSample ? "sem amostra" : "tamanho incorreto") + sizeNote + ".";
        }
    }

    private static IEnumerable<string> BuildReport(WindowCandidate target, CaptureSession session, List<StepResult> results)
    {
        yield return "# Relatório do spike de captura";
        yield return string.Empty;
        yield return $"- Data: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}";
        yield return $"- Sistema: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
        yield return $"- .NET: {RuntimeInformation.FrameworkDescription}";
        yield return $"- Alvo: título \"{TargetTitle}\", processo {target.ProcessName} (PID {target.ProcessId})";
        yield return $"- Tamanho inicial da captura: {session.InitialSize.Width}x{session.InitialSize.Height}";
        yield return $"- Borda amarela removida: {(session.BorderDisabled ? "sim" : "não")}";
        yield return $"- Cursor incluído na captura: não";
        yield return $"- Frames totais: {session.Frames}; recriações do pool: {session.PoolRecreations}";
        yield return string.Empty;
        yield return "O simulador atualiza um relógio a cada 100 ms, então cerca de 10 frames/s é o esperado.";
        yield return string.Empty;
        yield return "| # | Etapa | Frames/s | Janela | Amostra | Pixels pretos | Resultado |";
        yield return "|---|---|---|---|---|---|---|";
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            if (r.Skipped)
            {
                yield return $"| {i + 1} | {r.Step.Title} | - | - | - | - | PULADA |";
                continue;
            }

            var sample = r.SampleFile is null ? "nenhuma" : $"[{r.SampleWidth}x{r.SampleHeight}]({r.SampleFile})";
            var black = double.IsNaN(r.BlackRatio) ? "-" : r.BlackRatio.ToString("P0", CultureInfo.CurrentCulture);
            yield return $"| {i + 1} | {r.Step.Title} | {r.Fps.ToString("0.0", CultureInfo.CurrentCulture)} | {r.Window?.Describe()} | {sample} | {black} | {r.Verdict} |";
        }

        yield return string.Empty;
        var failures = results.Count(r => r.Verdict.StartsWith("FALHA", StringComparison.Ordinal));
        var warnings = results.Count(r => r.Verdict.StartsWith("ATENÇÃO", StringComparison.Ordinal));
        yield return $"Resumo: {failures} falha(s), {warnings} atenção(ões), {results.Count(r => r.Skipped)} etapa(s) pulada(s).";
    }

    private static double BlackRatio(SoftwareBitmap bitmap)
    {
        try
        {
            var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyToBuffer(bytes.AsBuffer());
            long black = 0;
            for (var i = 0; i + 2 < bytes.Length; i += 4)
            {
                if (bytes[i] < 4 && bytes[i + 1] < 4 && bytes[i + 2] < 4)
                {
                    black++;
                }
            }

            return bytes.Length == 0 ? double.NaN : black / (bytes.Length / 4.0);
        }
        catch (Exception ex) when (ex is ArgumentException or COMException)
        {
            return double.NaN;
        }
    }

    private static async Task SavePngAsync(SoftwareBitmap bitmap, string path)
    {
        var file = File.Create(path);
        await using (file.ConfigureAwait(false))
        {
            using var stream = file.AsRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream).AsTask().ConfigureAwait(false);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync().AsTask().ConfigureAwait(false);
        }
    }
}
