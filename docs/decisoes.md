# Registro de decisões

Toda decisão que muda o produto, o método ou a arquitetura entra aqui, da mais
recente para a mais antiga. Decisões técnicas com várias alternativas também
ganham um ADR em `docs/adr/` quando surgirem. As decisões iniciais de produto e
arquitetura continuam descritas em [`visao-do-produto.md`](visao-do-produto.md)
e [`arquitetura.md`](arquitetura.md).

Status possíveis: **vigente**, **substituída** (com link para a nova) ou
**hipótese** (aguardando validação).

---

## D6 — Transporte de vídeo com WebRTC H.264

- **Data:** 26/09/2026
- **Decidido por:** IA, com base no spike executado por Nícolas
- **Status:** vigente para a v0.1
- **Contexto:** o item 4 da v0.1 pedia validar WebRTC H.264 entre .NET e o
  Safari do iPhone pela Tailscale.
- **Decisão:** SIPSorcery + FFmpeg (H.264), conforme o
  [ADR 0002](adr/0002-transporte-de-video-webrtc-h264.md). p95 de 44 ms no
  Wi-Fi e 62 ms no 4G/5G em conexão direta.
- **Consequência:** licenças (SIPSorcery e FFmpeg com libx264) precisam de
  decisão de Nícolas antes do beta; comportamento com perdas e via DERP ainda
  precisa de teste.

## D5 — Captura de janela com Windows.Graphics.Capture

- **Data:** 26/09/2026
- **Decidido por:** IA, com base no spike executado por Nícolas
- **Status:** vigente
- **Contexto:** o item 3 da v0.1 pedia validar a captura exclusiva da janela.
- **Decisão:** usar Windows.Graphics.Capture sobre o `HWND`, nunca sobre o
  monitor, com as regras do [ADR 0001](adr/0001-captura-de-janela.md).
- **Consequência:** escalas diferentes entre monitores ficam para a v0.2; o
  próximo passo é o spike de WebRTC.

## D4 — CI no GitHub Actions e Claude Code no Windows

- **Data:** 25/09/2026
- **Decidido por:** Nícolas
- **Status:** vigente
- **Contexto:** a IA trabalha num ambiente Linux isolado e não consegue
  compilar nem executar o projeto Windows.
- **Decisão:** usar as duas opções. O GitHub Actions (`windows-latest`)
  compila e testa cada push na `main`, e o Claude Code roda no terminal do
  Windows para executar comandos locais.
- **Consequência:** testes visuais do simulador e, no futuro, do iPhone
  continuam manuais, feitos por Nícolas.

## D3 — Simulador como primeiro incremento de código

- **Data:** 25/09/2026
- **Decidido por:** Nícolas, com recomendação da IA
- **Status:** vigente
- **Contexto:** o roadmap pede um alvo seguro antes de testar captura e input
  no League Client real.
- **Decisão:** simulador WPF com núcleo separado e testável. Tela de
  referência 1280x720 com faixas pretas ao redimensionar, título próprio
  (`LoL Remote Simulator`) e janela de controle separada.
- **Consequência:** captura e input serão desenvolvidos primeiro contra o
  simulador. Detalhes em [`simulador.md`](simulador.md).

## D2 — Desenvolvimento direto na `main`

- **Data:** 25/09/2026
- **Decidido por:** Nícolas
- **Status:** vigente
- **Contexto:** com a IA desenvolvendo e Nícolas testando sem revisar código,
  issues, branches e PRs viram burocracia sem revisão real.
- **Decisão:** commits direto na `main`, um checkpoint por vez. A IA sugere a
  mensagem de commit e Nícolas faz o commit antes do próximo checkpoint.
  Issues ficam opcionais, só para bugs ou pedidos.
- **Consequência:** a rastreabilidade que ficaria no GitHub passa a morar no
  repositório: este registro, o `CHANGELOG.md`, o checklist do roadmap, os ADRs,
  as mensagens de commit e as tags de versão. O GitHub Project #4, os milestones
  e as labels deixam de ser obrigatórios.

## D1 — Modo de colaboração `work`

- **Data:** 25/09/2026
- **Decidido por:** Nícolas
- **Status:** vigente (substitui o modo `study` definido na fundação)
- **Contexto:** Nícolas prefere focar em testar e decidir, sem escrever o
  código.
- **Decisão:** a IA implementa as funcionalidades completas e explica as
  decisões em linguagem acessível.
- **Consequência:** Nícolas é responsável pelos testes manuais, pelas
  decisões de produto e pelos commits.
