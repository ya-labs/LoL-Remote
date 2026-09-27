# ADR 0003 — Input remoto com SendInput e verificação de primeiro plano

- **Status:** aceita
- **Data:** 27/09/2026
- **Evidência:** teste no simulador (26/09/2026) e no League Client real
  (27/09/2026), ambos pelo iPhone via Tailscale. Ver [agente](../agente.md).

## Contexto

O toque no celular precisa virar um clique no ponto certo do cliente, sem
afetar outras janelas. A arquitetura previa input direcionado e `SendInput`
como alternativa. Havia o risco de o Vanguard descartar input sintético.

## Decisão

1. Converter o toque (0..1 sobre o vídeo) em pixel da área cliente com as
   regras do Agent.Core; toques em faixa preta ou barra de título são
   recusados.
2. Antes de clicar: validar a janela (mesmo HWND e PID), restaurar se estiver
   escondida, trazer para o primeiro plano e conferir com `WindowFromPoint` que
   o ponto pertence à janela alvo. Se não pertencer, recusar
   (`target-obscured`).
3. Clicar com `SendInput` (mover + botão esquerdo pressiona + solta) em
   coordenadas absolutas da área de trabalho virtual, marcando os eventos para
   que a atividade local ignore o próprio agente.
4. Não usar mensagens de janela (`PostMessage`) nem drivers de dispositivo
   virtual.

## Resultados observados

- Simulador: cliques corretos em todas as telas; 0 violações durante a
  partida.
- League Client real, com o Vanguard ativo: 20 toques executados na sala, na
  fila, no Ready Check (recusa da partida) e na seleção de campeões, todos com
  efeito no cliente. O Vanguard não descartou os cliques no cliente.

## Alternativas consideradas

- **PostMessage/WM_LBUTTONDOWN:** não depende de primeiro plano, mas o cliente
  (Chromium) não trata esses eventos de forma confiável.
- **Driver de mouse virtual (HID):** contornaria bloqueios de input
  sintético, mas é complexo, exige instalação com privilégio e não foi
  necessário.

## Consequências

- O clique rouba o primeiro plano e move o cursor do PC; é aceitável porque o
  controle só é liberado sem atividade local recente.
- O bloqueio durante a partida foi confirmado no cliente real em 27/09/2026.
