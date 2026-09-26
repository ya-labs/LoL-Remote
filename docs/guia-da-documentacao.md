# Guia da documentação

## Objetivo

Registrar somente conhecimento que ajuda outra pessoa a entender, executar,
testar ou evoluir o LoL Remote.

## O que vai em cada lugar

- `README.md`: objetivo, estado atual, stack e caminho inicial.
- `docs/visao-do-produto.md`: problema, público, escopo e critérios do produto.
- `docs/arquitetura.md`: componentes, limites e decisões técnicas vigentes.
- `docs/roadmap.md`: resultados esperados por versão, com checklist do que
  já foi entregue.
- `docs/decisoes.md`: toda decisão tomada, com data, motivo e consequência.
- ADR futuro: decisão arquitetural relevante, alternativas e consequências.
- `CHANGELOG.md`: o que foi entregue e como foi validado, por versão.
- Commit: mudança pontual, em Conventional Commits.
- Issue: opcional, para registrar bug ou pedido.

## Regras de escrita

- Escreva em português do Brasil e UTF-8.
- Diferencie decisão, hipótese, risco e resultado observado.
- Não documente comandos ou recursos antes de funcionarem.
- Prefira links a duplicação de conteúdo.
- Atualize documentação junto da mudança que a tornou necessária.
- Não use documentos estáveis como checklist de andamento diário.

## ADRs

Crie `docs/adr/` somente quando existir a primeira decisão que precise desse
registro. Use numeração sequencial e inclua contexto, decisão, alternativas,
consequências e status.

Decisões já previstas para ADR incluem transporte WebRTC, codificação H.264,
estratégia de input e limites do uso read-only da LCU. Elas só devem ser
registradas como aceitas após os respectivos spikes.
