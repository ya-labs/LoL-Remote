# Spike de captura de janela

## Pergunta

A captura com `Windows.Graphics.Capture` entrega somente a janela alvo, de forma
confiável, nas situações que o agente vai enfrentar?

Critérios da v0.1 (roadmap, item 3):

1. Captura continua com a janela coberta por outra.
2. Captura acompanha mudança de tamanho.
3. Captura funciona em outro monitor, inclusive com escala diferente.
4. Janela minimizada não gera imagem enganosa.
5. Janela fechada encerra a captura (falha fechada), sem cair para o monitor.
6. A imagem tem o tamanho da janela e nada além dela.

## Como o spike funciona

`spikes/LoLRemote.CaptureSpike` é um programa de console descartável:

- **Localização com falha fechada:** aceita somente uma janela visível com
  título exato `LoL Remote Simulator` e processo `LoLRemote.Simulator`. Zero ou
  mais de uma correspondência encerra o programa sem capturar nada.
- **Captura:** cria um `GraphicsCaptureItem` para o `HWND` da janela, nunca para
  um monitor, e usa um frame pool de 2 buffers BGRA recriado quando o tamanho
  muda. O cursor fica fora da captura, e o spike tenta remover a borda amarela.
- **Roteiro guiado:** em cada etapa a pessoa mexe no simulador, pressiona Enter,
  e o spike mede os frames por 5 segundos, salva uma amostra PNG e compara o
  tamanho da amostra com o tamanho real da janela (tolerância de 2 px).
- **Saída:** `spike-output/captura-<data>/relatorio.md` e as amostras PNG. A
  pasta `spike-output/` fica fora do Git.

O programa usa DPI por monitor, então tamanhos e posições são pixels físicos.

## Cliente real (somente captura)

Para validar a captura no League Client sem ler a API e sem clicar (decisão
D7), com o cliente aberto:

```powershell
dotnet run --project spikes/LoLRemote.CaptureSpike -- --title "League of Legends" --process LeagueClientUx
```

Se a janela não for encontrada, o programa lista as janelas visíveis com
"League" no título e o processo de cada uma, para ajustar os argumentos.
Pule a etapa "fechada" (P) se não quiser fechar o cliente.

### Resultado no cliente real (26/09/2026)

Janela "League of Legends" do processo `LeagueClientUx`, 1280x720, escala 100%.

| Etapa | Resultado |
|---|---|
| Visível | Aprovado: ~55 frames/s (o cliente anima o tempo todo), só a janela |
| Coberta | Aprovado: a amostra mostra o cliente, não a janela por cima |
| Redimensionada | Não testada: o cliente não redimensiona pela borda (só pelas configurações dele) |
| Outro monitor | Aprovado (os dois monitores em 100%) |
| Minimizada | Diferente do simulador: o botão de minimizar do cliente não deixa a janela no estado "minimizada" do Windows; a captura continua (~16 frames/s) com a imagem do cliente |
| Fechada | Não testada (cliente mantido aberto); a etapa acusou corretamente que a janela ainda existia |

Observações para o agente:

- O cliente não tem barra de título do Windows: o frame capturado é igual à
  área cliente.
- Como o cliente minimizado continua desenhando, o agente precisa descobrir
  como ele está escondido (fora da tela, oculto ou apenas atrás) e restaurar a
  janela antes de clicar; hoje o clique seria recusado com `target-obscured`.

## Como executar

Com o simulador aberto, em outro terminal na raiz do repositório:

```powershell
dotnet run --project src/LoLRemote.Simulator -- --fast
dotnet run --project spikes/LoLRemote.CaptureSpike
```

## Status

Executado por Nícolas em 26/09/2026 (Windows 10.0.26200, .NET 10.0.7, dois
monitores em 100%). Decisão registrada no
[ADR 0001](adr/0001-captura-de-janela.md).

## Resultado

| Critério | Resultado |
|---|---|
| 1. Janela coberta | Aprovado: amostra mostra só o simulador |
| 2. Mudança de tamanho | Aprovado: 18 recriações do pool, amostra 1165x692 igual à janela |
| 3. Outro monitor | Aprovado para mesma escala; escala diferente não testada |
| 4. Minimizada | Aprovado: nenhum frame |
| 5. Fechada | Aprovado: nenhum frame e nenhum fallback; evento `Closed` não disparou |
| 6. Só a janela | Aprovado: amostras do tamanho exato da janela, incluindo barra de título |

Ajustes feitos no spike após a execução (não afetam os resultados acima):

- o tamanho inicial aparecia como 0x0 porque era lido depois de a janela
  fechar;
- a detecção de redimensionamento passou a contar desde a etapa anterior, pois
  a pessoa redimensiona antes de apertar Enter;
- a etapa "fechada" passou a julgar pela ausência da janela e de frames, e só
  informar se o evento `Closed` disparou.
