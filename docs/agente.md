# Agente

`src/LoLRemote.Agent` é o programa que roda no PC: captura a janela alvo, envia
o vídeo para o celular e converte toques validados em cliques. Na v0.1 o único
alvo aceito é o simulador.

## Como funciona

```text
iPhone (Safari)                          PC (agente)
  toque ── input.tap ──► canal control ──► validação (Agent.Core)
                                            ├─ recusado ──► input.ack rejected
                                            └─ aceito ──► primeiro plano + conferência do ponto
                                                          └─► SendInput (clique) ──► input.ack accepted
  vídeo ◄── WebRTC H.264 ◄── compositor ◄── captura da janela
  faixa de estado ◄── state (a cada mudança)
```

- Fase do jogo: lida pelo lockfile e pela API local do simulador, que imita a
  LCU. O lockfile precisa ser do mesmo processo da janela alvo; sem leitura nos
  últimos 2 s, a fase vira `Unknown` e o controle bloqueia.
- Atividade local: se alguém mexer no mouse ou teclado do PC, o controle remoto
  pausa por 10 s. Cliques do próprio agente não contam.
- Modo remoto: ativo por 30 minutos desde o início do agente
  (`--remote-minutes 5..60`).
- Nada de coordenadas, texto ou senha é registrado. O console mostra só o
  número do toque e o resultado.
- Estatísticas do celular vão para `agent-output/` (fora do Git).

## Como executar

Pré-requisitos: FFmpeg 8.1 Shared e Tailscale Serve já configurados (ver
[spike de WebRTC](spike-webrtc.md)). Na raiz do repositório, em dois terminais:

```powershell
dotnet run --project src/LoLRemote.Simulator -- --fast
dotnet run --project src/LoLRemote.Agent
```

No iPhone, abra o endereço do Tailscale Serve e toque em Conectar.

## Roteiro de teste

1. Com a faixa verde ("Controle liberado · Lobby"), toque em ENCONTRAR PARTIDA.
2. No Ready Check, toque em ACEITAR!.
3. Na seleção, escolha um campeão, toque em BANIR, escolha outro e CONFIRMAR.
4. Em "Partida em andamento", a faixa fica vermelha. Toque na tela: o toque
   deve ser recusado ("partida em andamento") e o contador de violações do
   simulador deve continuar em 0.
5. Mexa no mouse do PC: a faixa mostra "alguém está usando o PC" por 10 s.
6. Minimize o simulador: toques recusados ("janela do jogo minimizada").

## Status

- Núcleo (`Agent.Core`): coberto por testes automáticos.
- Agente no Windows: validado por Nícolas em 26/09/2026 com o iPhone, pela
  Tailscale em conexão direta, seguindo o roteiro acima: cliques corretos em
  todas as telas, toques recusados durante a partida (violações do simulador
  em 0), pausa por atividade local e recusa com a janela minimizada. Vídeo com
  latência p50 de 30 a 43 ms e p95 de 40 a 54 ms, sem perdas nem congelamentos.
