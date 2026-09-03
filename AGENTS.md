# Orientações para agentes de IA

## Antes de atuar

1. Leia `README.md` e `docs/README.md`.
2. Inspecione o código e os documentos reais antes de sugerir mudanças.
3. Consulte o YABook para planejamento, artefatos GitHub e mutações.
4. Preserve decisões aprovadas e identifique hipóteses como hipóteses.
5. Não invente comandos, APIs, resultados de testes ou suporte de plataforma.

## Postura padrão: estudo

Este é um projeto de aprendizagem prática. Nícolas deve escrever o código e
compreender as decisões tomadas.

- Explique primeiro o problema e o conceito necessário.
- Divida a implementação em passos pequenos, verificáveis e ligados à issue.
- Prefira perguntas que façam o estudante raciocinar quando ele tiver contexto
  suficiente para responder.
- Use exemplos reduzidos; não entregue a funcionalidade inteira pronta sem uma
  mudança explícita para o modo `work` ou `prod`.
- Revise código com causa, impacto, correção e forma de testar.
- Ensine C#, .NET, React e TypeScript conforme forem exigidos pelo projeto, sem
  criar uma trilha paralela desnecessária.

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

- Use o GitHub Project `https://github.com/orgs/ya-labs/projects/4`.
- Atribua novas issues a `nicolasmacardoso`, salvo decisão diferente.
- `Size` é campo do Project, de `1` a `5`, nunca label.
- Use milestones `v0.1` a `v0.7` e `v1.0`; não crie epic.
- Uma issue deve produzir um resultado verificável e pequeno o bastante para
  uma branch e um Pull Request.
- Branch: `numero-descricao-curta`.
- Commit: Conventional Commits em português.
- Pull Request: título objetivo, vínculo com a issue e evidência de testes.
- Antes de criar issue, branch, commit ou PR, valide o contrato vigente do
  YABook e procure artefatos equivalentes.

## Qualidade

- Teste transformações de coordenadas, estados, autorização e falhas de forma
  determinística antes dos testes com o League real.
- Use o simulador para desenvolver captura e input com segurança.
- Não considere integração com iPhone validada por emulador: Web Push, PWA,
  Face ID e mídia exigem aparelho real.
- Atualize a documentação quando uma decisão ou comportamento estável mudar.
