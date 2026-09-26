# Contratos

Fontes de verdade da comunicação entre o celular (PWA) e o agente:

- HTTP: [`contracts/openapi.yaml`](../contracts/openapi.yaml) (OpenAPI 3.1).
- Tempo real: [`contracts/schemas/control-message.schema.json`](../contracts/schemas/control-message.schema.json)
  (JSON Schema 2020-12).
- Exemplos: [`contracts/examples/control/`](../contracts/examples/control/), com
  exemplos válidos e, em `invalid/`, exemplos que precisam ser recusados.

Implementação de referência: `src/LoLRemote.Agent.Core`. Os testes em
`tests/LoLRemote.Agent.Core.Tests` leem os contratos e falham se o código
divergir (exemplos, motivos de recusa e fases do jogo).

## Fluxo de uma sessão

1. O celular sincroniza o relógio com várias chamadas a `GET /api/v1/time`.
2. O celular cria a oferta WebRTC (vídeo somente recepção e canal de dados
   `control`) e envia para `POST /api/v1/sessions`; o agente responde com o SDP.
3. Com a conexão aberta, o agente envia `state` e passa a enviar o vídeo.
4. Cada toque vira um `input.tap`; o agente responde sempre com `input.ack`.
5. A cada mudança de fase ou de bloqueio, o agente envia um novo `state`.

## Mensagens de controle (versão 1)

| Tipo | Sentido | Campos |
|---|---|---|
| `input.tap` | celular → agente | `seq`, `sentAt`, `x`, `y` (0..1 sobre o vídeo) |
| `input.ack` | agente → celular | `seq`, `status` (`accepted`/`rejected`), `reason` quando recusado |
| `state` | agente → celular | `phase` (nomes da LCU), `inputAllowed`, `reason` quando bloqueado |

Mensagens com campo extra, campo faltando, tipo desconhecido, número como texto
ou acima de 4096 caracteres são descartadas.

## Validação de um toque, na ordem

1. Versão igual a 1.
2. `seq` maior que a última recebida na sessão. A sequência é consumida mesmo
   se o comando for recusado depois, para impedir reenvio.
3. Idade até 1000 ms e no máximo 250 ms no futuro (relógio do agente).
4. No máximo 20 comandos por segundo.
5. Bloqueio do sistema (falha fechada, nesta ordem): modo remoto inativo, fase
   desconhecida, fase que não permite input, janela indisponível, janela
   minimizada, captura sem frame recente, atividade local no PC.
7. No Windows, a janela é trazida para o primeiro plano e o ponto da tela é
   conferido; se outra janela estiver na frente, o toque é recusado com
   `target-obscured`.
6. Conversão do ponto: toque na faixa preta ou na barra de título/bordas é
   recusado, não aproximado.

Fases que permitem input: `None`, `Lobby`, `Matchmaking`, `ReadyCheck`,
`ChampSelect` e `EndOfGame`. Todas as outras, inclusive fases novas que a Riot
venha a criar, bloqueiam.

## Conversão do toque em clique

O vídeo tem 1280x720; o frame capturado é encaixado com faixas pretas
(`VideoLayout`). O toque normalizado é convertido para pixel do frame, depois
para pixel da área cliente descontando barra de título e bordas
(`CoordinateMapper`). No Windows, esse ponto será convertido para coordenada de
tela e para a escala 0..65535 do `SendInput`.

## Pendente

- Autenticação: token de sessão após pareamento e WebAuthn (v0.5).
- Mensagens de teclado, scroll e gestos (v0.2).
- O spike de WebRTC continua com as rotas provisórias `/offer`, `/time` e
  `/stats`; o agente (`src/LoLRemote.Agent`) já usa as rotas do contrato.
