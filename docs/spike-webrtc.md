# Spike de WebRTC H.264

## Pergunta

Um processo .NET no Windows consegue enviar a janela capturada para o Safari do
iPhone por WebRTC com H.264, pela rede Tailscale, com latência compatível com a
meta da v1.0 (p95 até 250 ms em conexão direta e 600 ms via DERP)?

## Candidata avaliada

- [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) 10.0.16 para
  WebRTC (ICE, DTLS, SRTP, RTP).
- SIPSorceryMedia.FFmpeg 10.0.16 com FFmpeg 8.1 Shared para codificar H.264
  (libx264, perfil baseline, preset veryfast, tune zerolatency).

Pontos de licença a decidir antes do beta:

- SIPSorcery usa BSD 3-Clause com uma cláusula adicional que proíbe o uso por
  determinadas entidades; não é uma licença padrão aprovada pela OSI.
- SIPSorceryMedia.FFmpeg é LGPL 2.1. A build "full" do FFmpeg inclui libx264,
  que é GPL. Para distribuir aos testadores, avaliar uma build LGPL com o
  codificador do Windows (`h264_mf`) ou os de GPU (`h264_nvenc`, `h264_qsv`,
  `h264_amf`).

## Como o spike funciona

- Captura a janela do simulador com o código do spike de captura (mesma regra
  de falha fechada) e guarda só o frame mais recente.
- A 15 frames/s, redimensiona para 1280x720 com faixas pretas, carimba a hora
  do servidor num código de barras no canto inferior esquerdo, codifica em
  H.264 e envia pela conexão WebRTC.
- Servidor HTTP somente em `127.0.0.1:5080`; o iPhone acessa via Tailscale
  Serve (HTTPS dentro da tailnet, nunca na internet pública).
- A mídia (UDP) usa somente a interface Tailscale. Sem Tailscale, fica restrita
  ao próprio PC.
- A página no iPhone sincroniza o relógio com o servidor, lê o código de barras
  de cada frame exibido e calcula a latência de transporte (codificação, rede,
  decodificação e exibição). A captura em si não entra nessa medida.
- A cada 2 s a página envia estatísticas (latência p50/p95, FPS, codec, tipo de
  conexão, RTT) para `spike-output/webrtc-<data>/stats.jsonl`.

## Preparação (uma vez)

1. FFmpeg: `winget install "FFmpeg (Shared)" --version 8.1`.
2. Tailscale instalado e conectado no PC e no iPhone, na mesma conta.
3. No painel do Tailscale, em DNS: MagicDNS e HTTPS Certificates ativados.
4. No PC: `tailscale serve --bg 5080`. O comando mostra o endereço
   `https://<nome-do-pc>.<tailnet>.ts.net`.

## Como executar

```powershell
dotnet run --project src/LoLRemote.Simulator -- --fast
dotnet run --project spikes/LoLRemote.WebRtcSpike
```

No iPhone, abra o endereço do Tailscale Serve no Safari e toque em Conectar.

## Status

- Compilação: verificada contra stubs das APIs de SIPSorcery e WinRT com as
  regras de análise do projeto. **Ainda não compilada nem executada no
  Windows.**
- Resultado: pendente.
