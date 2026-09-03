# Visão do produto

## Problema

Uma pessoa pode entrar na fila do League of Legends e precisar se afastar do
computador por poucos minutos. Um Ready Check ou a Champion Select pode ocorrer
nesse intervalo, enquanto soluções genéricas de acesso remoto acrescentam
atrito, transmitem o desktop inteiro e não são focadas nesse fluxo curto.

## Proposta

O LoL Remote permite visualizar exclusivamente a janela real do League Client
no iPhone e transportar toques, teclado e scroll para o computador. Toda ação
relevante nasce de uma interação explícita da pessoa.

## Público da v1.0

Beta fechado, pessoal e não comercial, com até seis usuários conhecidos. Cada
participante controla o próprio PC e iPhone dentro de uma tailnet administrada
por Nícolas.

## Princípios

1. A pessoa permanece no controle.
2. Somente o League Client pode ser capturado e receber input.
3. Latência tem prioridade sobre fidelidade visual.
4. Vídeo é iniciado sob demanda; estado leve é preferível quando possível.
5. Segurança básica existe desde a primeira prova funcional.
6. O sistema falha fechado diante de incerteza.
7. A evolução ocorre por versões pequenas e mensuráveis.

## Critério de sucesso do núcleo

Com PC e iPhone conectados pela rede privada, visualizar a janela do League
Client, tocar em um elemento e reproduzir corretamente o clique correspondente
sem expor ou controlar outras aplicações.

## Critério de sucesso da v1.0

- Uso confiável em Wi-Fi e rede móvel.
- 720p com 15 a 30 FPS adaptativos.
- Latência p95 de vídeo de até 250 ms em conexão direta e 600 ms via DERP.
- Notificação de Ready Check em até dois segundos.
- Notificação recebida com PWA fechada e iPhone bloqueado.
- Face ID exigido ao iniciar nova sessão remota.
- Input bloqueado antes do gameplay.
- Instalação e diagnóstico compreensíveis por testadores do beta.

## Fora de escopo da v1.0

- jogar, decidir ou executar ações automáticas;
- controlar o desktop ou outros programas;
- app nativo e suporte oficial a Android;
- áudio remoto;
- infraestrutura própria, VPS ou coturn;
- alerta proativo quando o próprio PC estiver offline;
- atualização automática e distribuição comercial.

## Restrições externas

A League Client API é local e não oficialmente suportada para terceiros. O uso
planejado é exclusivamente read-only para estado e eventos, isolado do input.
Antes do beta, as políticas atuais da Riot deverão ser revisadas e o uso deverá
ser registrado pelos canais indicados pela Riot.

Se alertas de turno não forem compatíveis ou permitidos, a v1.0 poderá degradar
para Ready Check e mudanças de fase. Isso não autoriza detecção visual voltada a
automatizar ações.
