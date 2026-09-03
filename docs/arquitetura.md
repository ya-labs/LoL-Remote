# Arquitetura inicial

## Estado

Direção aprovada para orientar spikes e planejamento. Bibliotecas de mídia e
detalhes de integração permanecem hipóteses até validação experimental.

## Componentes

```text
iPhone / PWA
  | HTTPS privado, WebRTC e Web Push
  v
Tailscale / Tailscale Serve
  |
  v
Agente .NET na sessão interativa do Windows
  |-- autenticação e sessões
  |-- captura da janela
  |-- transporte de mídia
  |-- input controlado
  |-- estado read-only do League
  |-- notificações
  `-- diagnóstico local
          |
          v
      League Client
```

Não existe backend central na v1.0.

## Agente desktop

Aplicativo C#/.NET 10 com interface WPF/tray e servidor ASP.NET Core em
loopback. Ele deve executar na sessão interativa; um Windows Service não pode
ser a unidade principal porque captura e input dependem do desktop da pessoa.

Interfaces mínimas:

- captura da janela validada;
- transporte de mídia;
- controlador de input;
- monitor de estado do League;
- notificações;
- autenticação e sessões;
- diagnóstico.

## PWA

Aplicação React/TypeScript instalada na Tela de Início do iPhone. Exibe estado,
vídeo e controles, registra WebAuthn e Web Push e fornece diagnóstico compreensível.

## Rede

O agente escuta somente em loopback. Tailscale Serve publica HTTPS privado. A
tailnet deve usar Grants com acesso de cada identidade aos próprios dispositivos
e somente às portas necessárias para HTTPS e WebRTC.

Tailscale tenta conexão direta e usa DERP quando necessário. VPS, peer relay
próprio e coturn só poderão ser reconsiderados após medições demonstrarem uma
necessidade que justifique custo e operação.

## Captura e input

- Descobrir e validar o `HWND` do League Client.
- Capturar com `Windows.Graphics.Capture`.
- Nunca usar captura do monitor como fallback.
- Converter coordenadas normalizadas considerando proporção, letterbox, DPI e
  tamanho atual da janela.
- Confirmar janela, processo, estado e sequência antes de injetar input.
- Preferir input direcionado; usar `SendInput` controlado como fallback.
- Pausar diante de atividade local concorrente.
- Bloquear input quando a janela fechar, minimizar, mudar, o Windows bloquear,
  o modo remoto expirar ou a partida iniciar.

## Mídia

O primeiro spike validará WebRTC H.264 entre .NET e Safari no iPhone. A primeira
candidata é SIPSorcery com pipeline compatível de H.264. Licença, distribuição,
CPU, aceleração e dependências são critérios do spike.

Se essa candidata falhar, avaliar um adaptador isolado sobre libdatachannel. Se
as duas alternativas falharem nos critérios mínimos, interromper a v0.1 e
replanejar em vez de esconder o problema com uma arquitetura improvisada.

## Autenticação e dados

- QR com segredo aleatório de 256 bits, uso único e expiração de cinco minutos.
- WebAuthn/Face ID para iniciar cada nova sessão.
- SQLite apenas para metadados locais.
- DPAPI para chaves e segredos persistidos.
- Tokens da LCU somente em memória.
- Sem telemetria externa ou logs de texto digitado e coordenadas.

## Contratos

OpenAPI será a fonte do HTTP. JSON Schema versionará sinalização e comandos em
tempo real. Eventos de controle carregarão versão, sequência e timestamp, e
receberão confirmação explícita.

Os contratos concretos serão definidos depois dos spikes de captura e WebRTC,
antes da implementação integrada da v0.1.
