# Changelog

Mudanças do LoL Remote em linguagem simples. Cada versão aprovada recebe uma
tag no Git. O formato segue a ideia do [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## Não lançado

### Adicionado

- Spike de captura de janela (`spikes/LoLRemote.CaptureSpike`): encontra só a
  janela do simulador, captura com a API de captura do Windows e guia um teste
  em 7 etapas (visível, coberta, redimensionada, outro monitor, minimizada,
  restaurada, fechada), gerando relatório e imagens. Detalhes em
  [`docs/spike-captura.md`](docs/spike-captura.md).
- Relógio de pulsação no simulador, atualizado a cada 100 ms.
- ADR 0001: captura de janela com Windows.Graphics.Capture.

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

- Modo de colaboração de `study` para `work`: a IA desenvolve, Nícolas testa
  e decide (D1).
- Desenvolvimento direto na `main`, sem issues, branches e PRs obrigatórios;
  roadmap passa a ser checklist (D2).

### Corrigido

- Texto do botão "ENCONTRAR PARTIDA" cortado no simulador.

### Validado

- 26/09/2026, Windows: spike de captura aprovado nos 6 critérios (janela
  coberta, redimensionada, outro monitor, minimizada, fechada e somente a
  janela). Escalas diferentes entre monitores ainda não testadas.

- 25/09/2026, Windows: `dotnet test` com 42 testes passando; simulador
  executado com `--fast` e fluxo completo percorrido manualmente.
