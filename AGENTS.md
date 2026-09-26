# Orientações para agentes de IA

## Antes de atuar

1. Leia `README.md` e `docs/README.md`.
2. Inspecione o código e os documentos reais antes de sugerir mudanças.
3. Consulte o YABook para planejamento, artefatos GitHub e mutações.
4. Preserve decisões aprovadas e identifique hipóteses como hipóteses.
5. Não invente comandos, APIs, resultados de testes ou suporte de plataforma.

## Postura padrão: trabalho

Desde 25/09/2026 o projeto usa o modo `work` (antes: `study`). A IA pode
implementar funcionalidades completas, e Nícolas revisa e aprova.

- Implemente em checkpoints pequenos e verificáveis.
- Explique as decisões técnicas relevantes junto da entrega, em linguagem que
  ajude Nícolas a entender C#, .NET, React e TypeScript.
- Declare o que foi validado e como; nunca apresente como testado algo que não
  foi executado.
- Revise código com causa, impacto, correção e forma de testar.

O modo não reduz os guardrails do YABook nem autoriza mutações Git.

## Produto e segurança

- O LoL Remote transporta imagem e input explícito; não é um bot.
- Capture somente a janela validada do League Client.
- Não implemente ações semânticas como aceitar, banir ou escolher campeão.
- Bloqueie input durante gameplay e diante de estado inseguro ou desconhecido.
- Não exponha o serviço à internet pública; a v1.0 usa Tailscale.
- Nunca registre tokens, credenciais da LCU, teclas digitadas ou coordenadas de
  interação.
- Trate autenticação, pareamento e input remoto como superfícies críticas.

## Arquitetura

- Agente desktop: C# e .NET 10, processo interativo no Windows 11 24H2+.
- Cliente móvel: React e TypeScript em PWA, com iPhone como alvo oficial.
- Mantenha captura, mídia, input, estado do League, notificações,
  autenticação e diagnóstico desacoplados por interfaces simples.
- OpenAPI e JSON Schema são as fontes dos contratos públicos.
- Não adote a biblioteca final de WebRTC antes do spike aprovado.
- Simplicidade e clareza têm prioridade sobre abstrações especulativas.

## Rastreabilidade

Desenvolvimento direto na `main`, sem issues, branches ou PRs obrigatórios
(decisão D2 em `docs/decisoes.md`). Por isso o repositório precisa carregar o
contexto que antes ficaria no GitHub:

- Trabalhe em checkpoints: um resultado verificável por vez.
- Ao fim de cada checkpoint, atualize `CHANGELOG.md` (seção "Não lançado"), o
  checklist de `docs/roadmap.md` e, se houve decisão, `docs/decisoes.md`.
- Decisão técnica relevante com alternativas vira ADR em `docs/adr/`.
- Sugira a mensagem de commit (Conventional Commits em português) e peça o
  commit antes de iniciar o próximo checkpoint.
- Não faça commit, push, tag ou reescrita de histórico sem pedido explícito de
  Nícolas.
- Ao fechar uma versão aprovada por Nícolas, mova as entradas do changelog para
  a versão e sugira a tag correspondente.
- Nícolas não é técnico: explique decisões e peça escolhas em linguagem de
  produto, com recomendação clara.

## Qualidade

- Teste transformações de coordenadas, estados, autorização e falhas de forma
  determinística antes dos testes com o League real.
- Use o simulador para desenvolver captura e input com segurança.
- Não considere integração com iPhone validada por emulador: Web Push, PWA,
  Face ID e mídia exigem aparelho real.
- Atualize a documentação quando uma decisão ou comportamento estável mudar.
