# Simulador do League Client

## Objetivo

Oferecer um alvo seguro e determinístico para desenvolver descoberta de janela,
captura, conversão de coordenadas e input remoto sem tocar no League Client
real. O simulador não usa a LCU nem qualquer dado da Riot.

## Estrutura

- `src/LoLRemote.Simulator.Core`: biblioteca `net10.0` sem dependência de
  Windows. Contém a máquina de estados (`SimulatorEngine`), o layout em
  coordenadas de referência (`SimLayout`) e as durações (`SimulatorTimings`).
- `src/LoLRemote.Simulator`: aplicativo WPF `net10.0-windows` que desenha o
  layout e encaminha cliques ao motor.
- `tests/LoLRemote.Simulator.Core.Tests`: testes xUnit do núcleo, executáveis
  em qualquer sistema operacional.

## Fluxo simulado

```text
Lobby --encontrar--> Matchmaking --tempo--> ReadyCheck
  ^                      |                   |  aceitar + tempo
  |<--------cancelar-----'                   v
  |<--recusar / sem resposta------------ ChampSelect: Ban
  |                                          | confirmar ou tempo
  |<--tempo esgotado (dodge)------------ ChampSelect: Pick
  |                                          | confirmar
  |                                      ChampSelect: Finalization
  |                                          | tempo
  '<--"Encerrar partida" (controle)----- InProgress
```

Os nomes das fases acompanham o gameflow do League Client para facilitar o
mapeamento futuro do monitor de estado.

Durações padrão: fila 8 s, Ready Check 12 s, banimento 30 s, escolha 30 s e
finalização 10 s. O argumento `--fast` usa 2, 8, 12, 12 e 4 s.

## Decisões de projeto

- **Espaço de referência 1280x720.** A janela escala esse espaço com
  `Viewbox Stretch=Uniform`. Ao redimensionar para outra proporção surgem faixas
  pretas (letterbox), reproduzindo o problema de conversão que o agente terá no
  League Client.
- **DPI por monitor (PerMonitorV2).** Declarado em `app.manifest` para testar
  janelas em monitores com escalas diferentes.
- **Título próprio.** A janela alvo se chama `LoL Remote Simulator`. O agente
  deverá ser configurado explicitamente para esse alvo; o simulador não imita o
  título ou a classe de janela do League Client.
- **Janela de controle separada.** Estado, cronômetro, violações e histórico de
  cliques ficam em `LoL Remote Simulator - Controle`, fora da área capturada.
  Isso também exercita a escolha do `HWND` correto quando o processo tem mais de
  uma janela.
- **Violações de gameplay.** Qualquer clique recebido em `InProgress` é contado
  como violação e não altera o estado. Em testes integrados, o contador deve
  permanecer em zero.
- **Histórico só em memória.** O simulador mostra as últimas 50 coordenadas
  recebidas para depuração, sem gravar em disco. A proibição de registrar
  coordenadas continua valendo para o agente.
- **Tempo injetável.** O motor recebe um `TimeProvider`; os testes avançam o
  relógio manualmente, sem espera real.

## Como executar

Requer Windows 10/11 com o SDK do .NET 10.

```powershell
dotnet test tests/LoLRemote.Simulator.Core.Tests
dotnet run --project src/LoLRemote.Simulator -- --fast
```

Status de validação:

- Núcleo: `dotnet test` no Windows com 42 testes xUnit passando
  (25/09/2026).
- Aplicativo WPF: executado no Windows por Nícolas em 25/09/2026 com
  `--fast`. Fluxo completo validado manualmente (sala, fila, Ready Check,
  banimento, escolha e partida), com cliques em `InProgress` contados como
  violação. Os avisos CA1852 do primeiro build foram
  corrigidos e o projeto voltou a herdar `TreatWarningsAsErrors=true`.

## Próximos passos

- Validar o build e a execução do WPF no Windows 11.
- Usar o simulador no spike de captura com janela ocluída e em outro monitor.
- Usar o simulador para testar a transformação de toque e o `SendInput`.
