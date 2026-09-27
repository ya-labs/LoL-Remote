# Changelog

Mudanças do LoL Remote em linguagem simples. Cada versão aprovada recebe uma
tag no Git. O formato segue a ideia do [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## Não lançado

### Adicionado

- Rolagem arrastando o dedo sobre o vídeo e teclado: caixa "Digitar no PC" e
  botões Enter, Apagar, Esc e Tab. Mesmas validações e bloqueios do toque; texto
  nunca registrado.
- Contrato: mensagens `input.scroll`, `input.text` e `input.key`, com exemplos
  válidos, inválidos e recusados.

### Alterado

- O toque passa a ser enviado ao soltar o dedo, para diferenciar de arrasto.
- Depois de cada clique ou rolagem, o cursor do PC sai de cima do cliente, para
  não abrir o vídeo de habilidades ao passar sobre um campeão.

### Validado

- 27/09/2026, League Client real: rolagem da lista de campeões, busca por
  texto, tecla Apagar e chat pelo iPhone.

## 0.1.0 — Prova de conceito (27/09/2026)

Primeira versão funcional: pelo iPhone, ver a janela do League Client e tocar
para clicar nela, com o controle bloqueado durante a partida.

### Adicionado

- Agente funciona com o League Client real (`--target league`): lê a fase pela
  API local (somente leitura, HTTPS conferido pelo certificado da Riot),
  restaura o cliente minimizado antes de clicar e mostra diagnóstico quando um
  toque é recusado por janela na frente.
- ADR 0003: input remoto com SendInput e verificação de primeiro plano.

- Agente (`src/LoLRemote.Agent`): primeira versão que transforma toques no
  iPhone em cliques no simulador. Envia vídeo, recebe toques pelo canal de
  controle, valida cada um, traz a janela para a frente, confere que nada a
  cobre e só então clica. Bloqueia durante a partida, com a janela minimizada,
  sem imagem recente, com alguém usando o PC e após 30 minutos. Como rodar em
  [`docs/agente.md`](docs/agente.md).
- Página do celular com toque, marcação verde/vermelha de cada toque, motivo
  da recusa e faixa de estado (liberado/bloqueado).
- Simulador informa a fase do jogo como o League Client (lockfile + API local
  com senha).

- Contratos da comunicação celular ↔ PC: API HTTP (`contracts/openapi.yaml`),
  mensagens de controle (`contracts/schemas/control-message.schema.json`) e
  exemplos válidos e inválidos. Explicação em [`docs/contratos.md`](docs/contratos.md).
- Núcleo do agente (`src/LoLRemote.Agent.Core`): converte o toque no vídeo em
  ponto da janela (descontando faixas pretas e barra de título), bloqueia input
  fora das telas do cliente e recusa comandos repetidos, antigos, em excesso ou
  com formato errado. Testes automáticos conferem o código contra os contratos.

- Spike de WebRTC (`spikes/LoLRemote.WebRtcSpike`): transmite a janela do
  simulador para o Safari do iPhone em H.264 e mede a latência automaticamente
  com um código de barras de horário no vídeo. Detalhes em
  [`docs/spike-webrtc.md`](docs/spike-webrtc.md).

- Spike de captura de janela (`spikes/LoLRemote.CaptureSpike`): encontra só a
  janela do simulador, captura com a API de captura do Windows e guia um teste
  em 7 etapas (visível, coberta, redimensionada, outro monitor, minimizada,
  restaurada, fechada), gerando relatório e imagens. Detalhes em
  [`docs/spike-captura.md`](docs/spike-captura.md).
- Relógio de pulsação no simulador, atualizado a cada 100 ms.
- ADR 0001: captura de janela com Windows.Graphics.Capture.
- ADR 0002: transporte de vídeo com WebRTC H.264 (SIPSorcery + FFmpeg).

- Simulador do fluxo do League Client: janela que imita sala, fila, Ready
  Check, seleção de campeões e partida, para testar captura e cliques remotos
  sem usar o jogo real. Conta como "violação" qualquer clique durante a
  partida. Detalhes em [`docs/simulador.md`](docs/simulador.md).
- Núcleo do simulador separado do visual, com 42 testes automáticos.
- Estrutura da solution .NET 10 com build que trata avisos como erro.
- CI no GitHub Actions: compila e testa no Windows a cada push na `main`.
- `CLAUDE.md` para o Claude Code seguir as regras do projeto no Windows.
- Registro de decisões ([`docs/decisoes.md`](docs/decisoes.md)) e este
  changelog.

### Alterado

- Pausa do controle remoto após uso local do PC reduzida de 10 s para 5 s (D9).
- Modo de colaboração de `study` para `work`: a IA desenvolve, Nícolas testa
  e decide (D1).
- Desenvolvimento direto na `main`, sem issues, branches e PRs obrigatórios;
  roadmap passa a ser checklist (D2).

### Corrigido

- Agente travava ao encerrar com Ctrl+C.
- Depois de uma partida o agente perdia a janela do cliente, que é recriada;
  agora ele a reencontra sozinho.
- Leitura do lockfile do cliente real falhava porque o cliente mantém o arquivo
  aberto.

- Texto do botão "ENCONTRAR PARTIDA" cortado no simulador.

### Validado

- 27/09/2026, League Client real pelo iPhone: toques funcionando na sala, na
  fila, no Ready Check e na seleção de campeões, com o Vanguard ativo; controle
  bloqueado durante a partida.

- 26/09/2026, League Client real: captura só da janela do cliente, inclusive
  coberta e em outro monitor, sem ler a API e sem clicar.

- 26/09/2026, iPhone via Tailscale: controle do simulador pelo toque, com
  bloqueio durante a partida (0 violações), pausa por uso local do PC e recusa
  com janela minimizada. Vídeo com p95 de 40 a 54 ms.

- 26/09/2026, iPhone (Safari 26.6.1) via Tailscale direto: vídeo H.264
  1280x720 a 15 FPS com latência p95 de 44 ms no Wi-Fi e 62 ms no 4G/5G.

- 26/09/2026, Windows: spike de captura aprovado nos 6 critérios (janela
  coberta, redimensionada, outro monitor, minimizada, fechada e somente a
  janela). Escalas diferentes entre monitores ainda não testadas.

- 25/09/2026, Windows: `dotnet test` com 42 testes passando; simulador
  executado com `--fast` e fluxo completo percorrido manualmente.
