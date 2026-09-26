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

O spike grava a saída na pasta de onde foi executado. Rode a partir da raiz do
repositório para usar `spike-output/` da raiz.

## Rodada 1 (26/09/2026)

Ambiente: Windows 11, iPhone com iOS/Safari 26.6.1, Tailscale nos dois. Vídeo
H.264 1280x720 a 15 FPS, cerca de 100 kbit/s (tela quase estática).

| Rede | Latência p50 | Latência p95 | RTT | Observações |
|---|---|---|---|---|
| Wi-Fi | 23 a 61 ms | 27 a 110 ms | ~5 ms | Estável, sem quadros descartados |
| 4G/5G | 64 a 1.948 ms | até 2.722 ms | 36 a 5.765 ms | Travadas de vários segundos, 100 quadros descartados, 1 queda de conexão |

Conclusões:

- WebRTC H.264 do .NET para o Safari funciona: o codec negociado foi H.264 e a
  decodificação levou ~6 ms por quadro.
- No Wi-Fi a meta (p95 até 250 ms) foi atendida com folga.
- No 4G/5G, entre as travadas a latência ficou em 64 a 93 ms, mas as travadas
  derrubaram o p95 para segundos. Não foi possível saber se o caminho era
  direto ou por relay (DERP).
- Causa provável das travadas longas: o SIPSorcery não anuncia `nack pli`, então
  o Safari não pede quadro-chave ao perder pacotes, e o quadro-chave periódico
  vinha só a cada ~2 s.

Ajustes para a rodada 2:

- anunciar `nack pli` na resposta SDP e gerar quadro-chave quando o iPhone pedir;
- quadro-chave periódico a cada 1 s;
- pedir ao Safari o menor buffer de recepção possível;
- registrar pacotes perdidos, congelamentos, buffer e se o Tailscale está em
  conexão direta ou por relay.

## Rodada 2 (26/09/2026)

Com os ajustes acima. O Tailscale informou conexão direta nas duas redes.

| Rede | Latência p50 | Latência p95 | RTT | Perdas | Buffer |
|---|---|---|---|---|---|
| Wi-Fi | 32 a 36 ms | 39 a 44 ms | 5 a 9 ms | nenhuma | 9 a 15 ms |
| 4G/5G | 52 a 54 ms | 60 a 62 ms | 38 a 101 ms | nenhuma | 16 a 18 ms |

Não houve pacote perdido nem pedido de quadro-chave, então o ajuste de PLI não
foi exercitado nesta rodada. O teste em 4G/5G durou cerca de 35 s.

## Status

Concluído para a v0.1. Decisão registrada no
[ADR 0002](adr/0002-transporte-de-video-webrtc-h264.md), com as pendências de
perdas, DERP, taxa adaptativa e licenças.
