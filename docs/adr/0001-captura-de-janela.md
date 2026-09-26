# ADR 0001 — Captura de janela com Windows.Graphics.Capture

- **Status:** aceita
- **Data:** 26/09/2026
- **Evidência:** [spike de captura](../spike-captura.md), execução de
  26/09/2026 no Windows 10.0.26200 (Windows 11), .NET 10.0.7.

## Contexto

O agente precisa transmitir somente a janela do League Client, sem jamais
expor o resto da tela, e continuar funcionando quando a pessoa cobre, move ou
redimensiona a janela.

## Decisão

1. Capturar com `Windows.Graphics.Capture`, criando o item de captura a partir
   do `HWND` da janela (`IGraphicsCaptureItemInterop.CreateForWindow`). Nunca
   criar item de captura de monitor, nem como fallback.
2. Localizar o alvo exigindo exatamente uma janela visível com título e
   processo esperados; zero ou várias correspondências não iniciam a captura.
3. Usar frame pool free-threaded com 2 buffers BGRA e recriá-lo quando o
   tamanho do conteúdo mudar.
4. Desligar a captura do cursor e remover a borda amarela de captura.
5. Tratar "nenhum frame recente" como "sem imagem" e bloquear input nesse
   estado (janela minimizada ou fechada).
6. Detectar o fim da janela por verificação ativa (`IsWindow` e ausência de
   frames), sem depender só do evento `GraphicsCaptureItem.Closed`.
7. A imagem capturada inclui a barra de título e as bordas da janela. A
   conversão de toque para clique deve considerar o deslocamento da área
   cliente dentro do frame (ou recortar essa área antes de transmitir).

## Resultados observados

| Situação | Resultado |
|---|---|
| Janela visível | ~9 frames/s (relógio de 100 ms), amostra do tamanho exato da janela |
| Coberta por outra janela | Continua capturando só o simulador |
| Redimensionada | Frame pool recriado; amostra no novo tamanho |
| Outro monitor (ambos 100%) | Continua capturando, tamanho correto |
| Minimizada | Nenhum frame |
| Fechada | Nenhum frame; o evento `Closed` não disparou no console |
| Borda amarela | Removida sem pedido de permissão (app não empacotado) |

## Alternativas consideradas

- **Captura do monitor recortada na janela:** descartada; expõe o que estiver
  por cima e viola a regra de nunca capturar o desktop.
- **`PrintWindow`/GDI:** não avaliada; menos eficiente e com problemas
  conhecidos em janelas aceleradas por GPU, como o League Client.

## Pendências

- Monitores com escalas diferentes (ex.: 100% e 150%) não foram testados; os
  dois monitores do teste estavam em 100%. Coberto pela v0.2.
- Verificar se `Closed` dispara no agente WPF, que tem loop de mensagens.
- Validar no League Client real (item 8 da ordem do primeiro bloco).

## Consequências

A captura fica isolada atrás de uma interface no agente. O próximo passo é o
spike de WebRTC H.264, que vai transportar esses frames.
