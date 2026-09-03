# LoL Remote

Controle remoto especializado no League Client para momentos em que a pessoa
precisa se afastar do computador por alguns minutos sem perder o Ready Check ou
a Champion Select.

O LoL Remote transmite somente a janela real do League Client para o celular e
converte interações explícitas da pessoa em input no Windows. O produto não é
um bot: ele não escolhe campeão, bane, altera runas ou joga automaticamente.

## Estado atual

O projeto está na fundação de produto, arquitetura e aprendizagem. Ainda não há
código executável. O primeiro incremento será uma prova de conceito de captura,
transmissão e clique remoto em rede privada.

## Objetivo da v1.0

Entregar um beta fechado, pessoal e não comercial para até seis usuários, com:

- agente interativo para Windows 11 24H2 ou superior;
- PWA otimizada inicialmente para iPhone com iOS 18.4 ou superior;
- conexão privada por Tailscale, direta quando possível e via DERP como fallback;
- captura exclusiva da janela do League Client;
- controle manual por toque, teclado e scroll;
- notificações de Ready Check e mudanças de fase;
- bloqueio de input quando a partida entrar em andamento;
- autenticação por pareamento e Face ID/WebAuthn;
- diagnóstico local de conexão e latência.

Consulte a [visão do produto](docs/visao-do-produto.md), a
[arquitetura inicial](docs/arquitetura.md) e o [roadmap](docs/roadmap.md).

## Stack planejada

- Desktop: C#, .NET 10, WPF, ASP.NET Core e APIs nativas do Windows.
- Web móvel: React, TypeScript, Vite e PWA.
- Mídia: WebRTC com H.264, condicionado a spike técnico.
- Rede privada: Tailscale e Tailscale Serve.
- Persistência local: SQLite, com segredos protegidos por DPAPI.
- Contratos: OpenAPI para HTTP e JSON Schema para mensagens em tempo real.

As bibliotecas concretas de WebRTC e codificação só serão adotadas após prova
de compatibilidade, desempenho e licença.

## Como executar

Ainda não há aplicação executável. Quando a primeira implementação existir,
esta seção deverá conter os pré-requisitos, comandos e limitações verificados.
Não documente comandos futuros como se já funcionassem.

## Aprendizagem e colaboração

Este projeto adota o modo de colaboração `study` do YABook. Nícolas escreverá o
código para aprender C#, .NET, React e as tecnologias envolvidas. A IA deve:

- explicar os fundamentos conforme surgirem no trabalho real;
- propor passos pequenos e exercícios úteis;
- revisar e depurar o código escrito pelo estudante;
- evitar entregar implementações completas sem mudança explícita para `work` ou
  `prod`;
- relacionar cada conceito ao problema concreto do LoL Remote.

Não existe uma trilha teórica separada obrigatória: o estudo acompanha as
necessidades de cada issue.

## Organização do trabalho

- Repositório: `ya-labs/LoL-Remote`.
- GitHub Project: [YA LABS Project #4](https://github.com/orgs/ya-labs/projects/4).
- Responsável padrão: [`nicolasmacardoso`](https://github.com/nicolasmacardoso).
- Milestones: `v0.1` a `v0.7` e `v1.0`, sem epic.
- `Size`: campo do Project com valores de `1` a `5`; não é label.
- Fluxo: uma issue por branch e Pull Request, com rastreabilidade entre os
  artefatos.

Labels planejadas:

- tipo: `type: feature`, `type: bug`, `type: spike`, `type: docs`, `type: chore`;
- área: `area: desktop`, `area: mobile`, `area: capture`, `area: transport`,
  `area: input`, `area: league-state`, `area: notifications`, `area: security`,
  `area: diagnostics`, `area: distribution`;
- plataforma: `platform: windows`, `platform: ios`, `platform: tailscale`;
- prioridade: `priority: p0` a `priority: p3`;
- risco: `risk: high`, `risk: medium`, `risk: low`.

O projeto usa o [YABook](https://github.com/ya-labs/Handbook) para planejamento,
execução segura, documentação e artefatos GitHub.

## Documentação

O índice e as regras de manutenção estão em [`docs/README.md`](docs/README.md).
Antes de contribuir, consulte também [`CONTRIBUTING.md`](CONTRIBUTING.md),
[`SECURITY.md`](SECURITY.md) e [`AGENTS.md`](AGENTS.md).

## Limites importantes

- Nunca capturar o desktop inteiro como fallback.
- Nunca controlar gameplay.
- Nunca transformar eventos do League em decisões automáticas.
- Falhar fechado quando o alvo, a sessão ou o estado de segurança forem
  incertos.
- Revisar as políticas atuais da Riot antes do beta e antes de aprofundar o uso
  da League Client API, que não possui suporte oficial para terceiros.
