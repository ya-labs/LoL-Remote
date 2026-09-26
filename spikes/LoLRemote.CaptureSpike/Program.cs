using System.Text;
using LoLRemote.CaptureSpike;

Console.OutputEncoding = Encoding.UTF8;
return await GuidedSpike.RunAsync(args).ConfigureAwait(false);
