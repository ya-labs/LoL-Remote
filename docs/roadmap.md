# Roadmap até a v1.0

O roadmap registra resultados de produto e marca o que já foi entregue
(`[x]`). Detalhes de cada entrega ficam no [`CHANGELOG.md`](../CHANGELOG.md).

## Fundação

- [x] documentação e governança do repositório;
- [x] modo de trabalho e rastreabilidade no repositório (decisões D1 e D2);
- [x] ambiente de desenvolvimento, CI e critérios de qualidade.

## v0.1 — Prova de conceito

- [x] simulador seguro do fluxo do League;
- [x] descoberta e captura exclusiva de uma janela ([ADR 0001](adr/0001-captura-de-janela.md));
- [x] spike WebRTC H.264 no iPhone ([ADR 0002](adr/0002-transporte-de-video-webrtc-h264.md));
- [x] toque normalizado e clique remoto controlado ([agente](agente.md));
- [x] medição inicial de latência e ADR de transporte.

## v0.2 — Controle utilizável

- [ ] teclado, scroll e gestos necessários;
- [ ] zoom e orientação mobile;
- [ ] reconexão;
- [ ] múltiplos monitores e mudanças de tamanho/DPI.

## v0.3 — League Awareness

- [ ] estados read-only da LCU;
- [ ] Ready Check e Champion Select;
- [ ] tentativa de alertas de turno;
- [ ] Web Push com iPhone bloqueado;
- [ ] bloqueio no início da partida.

## v0.4 — Acesso remoto

- [ ] onboarding do Tailscale e Tailscale Serve;
- [ ] Grants e isolamento entre usuários;
- [ ] funcionamento em Wi-Fi e 4G/5G;
- [ ] diagnóstico de conexão direta e DERP.

## v0.5 — Experiência

- [ ] pareamento por QR;
- [ ] Face ID por nova sessão;
- [ ] modo remoto de 30 minutos, configurável entre 5 e 60;
- [ ] abertura da notificação diretamente na sessão;
- [ ] indicadores de conexão e feedback de toque.

## v0.6 — Segurança

- [ ] revogação e expiração;
- [ ] rate limiting e proteção contra replay;
- [ ] kill switch pelo tray e atalho definido;
- [ ] revisão de ameaças e logs;
- [ ] validação das políticas da Riot antes do beta.

Segurança básica não espera esta versão: validação de alvo, autorização e
bloqueio de gameplay começam na v0.1. A v0.6 consolida e audita o conjunto.

## v0.7 — Otimização

- [ ] resolução, FPS e bitrate adaptativos;
- [ ] descarte de frames antigos;
- [ ] otimização de captura e encode;
- [ ] métricas locais de latência e estabilidade.

## v1.0 — Beta fechado

- [ ] estabilização e correções do fluxo completo;
- [ ] testes reais no iPhone, em rede local e móvel;
- [ ] documentação para testadores;
- [ ] pacote ZIP x64 e checksum SHA-256;
- [ ] aviso de atualização com instalação manual;
- [ ] critérios de desempenho e segurança atendidos.

## Ordem do primeiro bloco

1. [x] Inicializar documentação e governança.
2. [x] Criar simulador do fluxo do League (WPF).
3. [x] Validar captura de janela ocluída e em outro monitor (escalas
   diferentes pendentes para a v0.2).
4. [x] Validar WebRTC H.264 entre .NET e Safari/iPhone por Tailscale.
5. [x] Definir OpenAPI, schemas e erros ([contratos](contratos.md)).
6. [x] Implementar transformação de toque e `SendInput` no simulador.
7. [x] Medir latência e registrar a decisão de transporte (vídeo; a latência
   do toque será medida junto com o input).
8. [x] Somente então testar captura e clique no League Client real
   ([ADR 0003](adr/0003-input-com-sendinput.md)); falta confirmar o bloqueio no
   início de uma partida real.
