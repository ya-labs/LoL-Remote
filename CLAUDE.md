# Instruções para o Claude Code

As regras para agentes deste repositório estão em `AGENTS.md`:

@AGENTS.md

## Comandos verificados

```powershell
dotnet build LoLRemote.slnx
dotnet test tests/LoLRemote.Simulator.Core.Tests
dotnet run --project src/LoLRemote.Simulator -- --fast
dotnet run --project spikes/LoLRemote.CaptureSpike
dotnet run --project spikes/LoLRemote.WebRtcSpike
```

O spike de WebRTC precisa do FFmpeg 8.1 Shared, do Tailscale e do iPhone de
Nícolas; veja `docs/spike-webrtc.md`.

O spike de captura é interativo: precisa do simulador aberto e de Nícolas
seguindo as instruções no console.

Rodar build e testes antes de dar uma mudança como concluída. Fluxo de trabalho
direto na `main`, por checkpoints: veja "Rastreabilidade" em `AGENTS.md`. O simulador WPF
abre janelas: a validação visual é feita por Nícolas.
