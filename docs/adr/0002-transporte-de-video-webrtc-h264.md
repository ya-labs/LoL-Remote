# ADR 0002 — Transporte de vídeo com WebRTC H.264 (SIPSorcery + FFmpeg)

- **Status:** aceita para a v0.1, com pendências listadas abaixo
- **Data:** 26/09/2026
- **Evidência:** [spike de WebRTC](../spike-webrtc.md), rodadas 1 e 2 de
  26/09/2026 (Windows 11, iPhone com Safari 26.6.1, Tailscale).

## Contexto

O agente precisa enviar a janela capturada ([ADR 0001](0001-captura-de-janela.md))
ao Safari do iPhone com baixa latência, pela rede Tailscale. A meta da v1.0 é
p95 de até 250 ms em conexão direta e 600 ms via DERP.

## Decisão

1. WebRTC com [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery)
   10.0.16 no agente .NET, com o navegador fazendo a oferta SDP e o agente
   respondendo por HTTP.
2. Codificação H.264 (perfil baseline, sem B-frames) por
   SIPSorceryMedia.FFmpeg 10.0.16 com FFmpeg 8.1 Shared.
3. Saída de 1280x720 com faixas pretas, 15 FPS, sempre com o frame mais
   recente (frames antigos são descartados).
4. Quadro-chave a cada 1 s e sempre que o iPhone pedir (PLI/FIR). A resposta
   SDP acrescenta `a=rtcp-fb:<pt> nack pli`, que o SIPSorcery não anuncia.
5. Mídia UDP presa à interface Tailscale; o HTTP escuta só em loopback e é
   publicado pelo Tailscale Serve.
6. No Safari, pedir o menor buffer de recepção (`jitterBufferTarget = 0`) quando
   suportado.

## Resultados observados

| Rodada | Rede | Caminho Tailscale | Latência p50 | Latência p95 | Perdas |
|---|---|---|---|---|---|
| 1 | Wi-Fi | não medido | 23–61 ms | até 110 ms | nenhuma |
| 1 | 4G/5G | não medido | 64–1.948 ms | até 2.722 ms | travadas, 100 quadros descartados |
| 2 | Wi-Fi | direto | 32–36 ms | até 44 ms | nenhuma |
| 2 | 4G/5G | direto | 52–54 ms | até 62 ms | nenhuma |

A latência medida vai do início da codificação até a exibição no iPhone
(codificação, rede, decodificação e exibição). Decodificação no iPhone:
~6 ms por quadro. Codificação no PC: ~10 ms por quadro.

## Alternativas consideradas

- **libdatachannel:** reserva caso o SIPSorcery falhasse; não foi necessária.
- **VP8:** suportado pelo Safari, mas sem garantia de decodificação por
  hardware no iPhone; H.264 funcionou.
- **Streaming por WebSocket/MJPEG:** mais simples, mas sem controle de
  congestionamento nem recuperação de perdas do WebRTC.

## Pendências

- **Perdas de pacote:** a rodada 2 não teve perdas, então o pedido de
  quadro-chave (PLI) ainda não foi exercitado. A melhora frente à rodada 1 pode
  ter vindo também de uma rede melhor.
- **Caminho via DERP:** as duas redes da rodada 2 conectaram direto. Testar
  relay na v0.4 (diagnóstico de conexão direta e DERP).
- **Taxa adaptativa:** o SIPSorcery não estima banda; resolução, FPS e bitrate
  adaptativos ficam para a v0.7.
- **Licenças, antes do beta:** SIPSorcery usa BSD 3-Clause com cláusula
  adicional não padrão; a build "full" do FFmpeg inclui libx264 (GPL). Avaliar
  uma build LGPL com `h264_mf` ou codificadores de GPU para distribuição.
- **Conversão de cor e cópia GPU→CPU:** a captura é copiada para a CPU a cada
  frame. Otimizar na v0.7 se o custo de CPU aparecer.

## Consequências

O agente terá um módulo de transporte com essa pilha atrás de uma interface.
Os contratos HTTP e de sinalização (OpenAPI e JSON Schema) são o próximo passo
da v0.1, seguidos do input remoto no simulador.
