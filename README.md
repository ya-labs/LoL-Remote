# LoL Remote

Controle remoto especializado no League Client para momentos em que a pessoa
precisa se afastar do computador por alguns minutos sem perder o Ready Check ou
a Champion Select.

O LoL Remote transmite somente a janela real do League Client para o celular e
converte interações explícitas da pessoa em input no Windows. O produto não é
um bot: ele não escolhe campeão, bane, altera runas ou joga automaticamente.

## Estado atual

A v0.1 está quase concluída. Já funcionam, contra um simulador do League
Client:

- captura exclusiva da janela e vídeo H.264 para o iPhone por WebRTC, pela
  Tailscale ([ADR 0001](docs/adr/0001-captura-de-janela.md) e
  [ADR 0002](docs/adr/0002-transporte-de-video-webrtc-h264.md));
- controle por toque com validação e bloqueio durante a partida
  ([agente](docs/agente.md)).

Falta testar com o League Client real, o que depende de revisar antes as
políticas da Riot sobre a API local.

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

Veja [`docs/agente.md`](docs/agente.md) (agente e roteiro de teste com o
iPhone) e [`docs/simulador.md`](docs/simulador.md). Não documente comandos
futuros como se já funcionassem.

## Aprendizagem e colaboração

Este projeto adota o modo de colaboração `work` do YABook, por decisão de
Nícolas em 25/09/2026 (antes: `study`). A IA pode implementar funcionalidades
completas e deve:

- explicar as decisões técnicas relevantes junto da entrega;
- manter mudanças pequenas e registradas no changelog;
- declarar o que foi e o que não foi validado;
- deixar Nícolas revisar e aprovar antes de qualquer mutação Git.

O objetivo de aprendizagem continua: explicações acompanham o código entregue.

## Organização do trabalho

Desde 25/09/2026 o desenvolvimento acontece direto na branch `main`, sem issues,
branches de funcionalidade ou Pull Requests (veja a
[decisão D2](docs/decisoes.md)). A rastreabilidade fica no próprio repositório:

- [`CHANGELOG.md`](CHANGELOG.md): o que mudou, em linguagem simples;
- [`docs/decisoes.md`](docs/decisoes.md): decisões tomadas, com data e motivo;
- [`docs/roadmap.md`](docs/roadmap.md): checklist do que já foi entregue;
- `docs/adr/`: decisões técnicas maiores, quando surgirem;
- histórico de commits em Conventional Commits, em português;
- tags `v0.1` a `v1.0` marcando versões testadas e aprovadas.

Papéis: a IA desenvolve, documenta e sugere a mensagem de commit ao fim de cada
checkpoint; Nícolas testa, decide e faz o commit e o push. O CI do GitHub
Actions compila e testa cada push na `main`.

Issues continuam disponíveis para registrar bugs ou pedidos, mas são opcionais.
O repositório é `ya-labs/LoL-Remote`, e o
[YABook](https://github.com/ya-labs/Handbook) segue como referência de método,
documentação e execução segura.

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
