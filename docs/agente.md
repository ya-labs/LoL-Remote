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

## Cliente real (`--target league`)

Em teste, conforme a decisão D7 (produto registrado na Riot).

- Janela: "League of Legends" do processo `LeagueClientUx`.
- Fase: lida do lockfile na pasta de instalação, escrito pelo `LeagueClient`
  (processo pai da janela). O agente confere esse parentesco, só aceita HTTPS
  com certificado emitido pela raiz da Riot e só chama
  `GET /lol-gameflow/v1/gameflow-phase`.
- Se o cliente estiver minimizado pelo botão dele, o agente restaura a janela
  antes de clicar. Quando um toque é recusado por janela na frente, o console
  mostra um diagnóstico (sem coordenadas).
- O carimbo de latência fica desligado por padrão, para não cobrir o chat.

Preparação (uma vez), na raiz do repositório:

```powershell
curl -o src/LoLRemote.Agent/riotgames.pem https://static.developer.riotgames.com/docs/lol/riotgames.pem
```

Com o cliente aberto e logado:

```powershell
dotnet run --project src/LoLRemote.Agent -- --target league
```

O agente confere a impressão digital do arquivo (SHA-256
`CA8C9D32…4A504EA3`, "LoL Game Engineering Certificate Authority", válido até
2043) e recusa qualquer outro. O arquivo é público e fica versionado.

Se o lockfile não estiver na pasta padrão, use `--lockfile <caminho>`.

Roteiro:

1. Sala: a faixa fica verde ("Lobby"). Toque em algum botão do cliente e veja
   se o clique acontece. **Este é o teste do anticheat**: se o toque aparecer
   como aceito mas nada acontecer no cliente, o Vanguard está descartando o
   clique.
2. Fila: entre na fila e, no Ready Check, toque em **Recusar** pelo celular.
   Recusar várias vezes seguidas gera tempo de espera crescente na fila; uma ou
   duas vezes não é problema.
3. Partida personalizada: na seleção de campeões, escolha e confirme pelo
   celular.
4. Quando a partida carregar, a faixa fica vermelha e toques são recusados.
5. Opcional: minimize o cliente pelo botão dele e toque em algo.

### Resultado no cliente real (27/09/2026)

- Fase lida corretamente: Lobby, Matchmaking, ReadyCheck e ChampSelect.
- 20 toques executados, todos com efeito no cliente: sala, fila, recusa no
  Ready Check e escolha na seleção de campeões. O Vanguard não bloqueou os
  cliques.
- Pausa por atividade local funcionou.
- Pendente: confirmar o bloqueio quando a partida começa (GameStart e
  InProgress).
- Corrigido depois do teste: o agente travava ao encerrar com Ctrl+C.

## Status

- Núcleo (`Agent.Core`): coberto por testes automáticos.
- Agente no Windows: validado por Nícolas em 26/09/2026 com o iPhone, pela
  Tailscale em conexão direta, seguindo o roteiro acima: cliques corretos em
  todas as telas, toques recusados durante a partida (violações do simulador
  em 0), pausa por atividade local e recusa com a janela minimizada. Vídeo com
  latência p50 de 30 a 43 ms e p95 de 40 a 54 ms, sem perdas nem congelamentos.
