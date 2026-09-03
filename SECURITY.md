# Segurança

## Escopo

O LoL Remote controla input em uma aplicação local e, portanto, trata captura,
autenticação, pareamento, transporte e injeção de input como superfícies
críticas desde a primeira versão.

## Propriedades obrigatórias

- Capturar e controlar somente a janela validada do League Client.
- Nunca usar o desktop inteiro como fallback.
- Exigir dispositivo pareado e autenticação antes de abrir sessão.
- Validar coordenadas, sequência, expiração, alvo e estado para cada comando.
- Bloquear input durante gameplay ou quando o estado seguro não puder ser
  confirmado.
- Encerrar imediatamente por kill switch local.
- Manter o serviço fora da internet pública.
- Proteger segredos locais com DPAPI e manter credenciais efêmeras em memória.
- Não registrar tokens, texto digitado, coordenadas ou conteúdo capturado.

## Rede e confiança

Tailscale fornece uma rede privada criptografada, mas não substitui autorização
da aplicação. A tailnet deve negar acesso cruzado entre participantes, e o
agente ainda deve exigir pareamento e WebAuthn.

## Relato responsável

Durante o beta fechado, não publique detalhes exploráveis em issue pública.
Envie o relato diretamente ao mantenedor `nicolasmacardoso`, contendo impacto,
condições de reprodução e versão afetada, sem incluir credenciais reais.

Um canal privado definitivo deverá ser publicado antes de ampliar o beta.

## Limitações conhecidas da fundação

- Ainda não existe implementação para auditoria.
- A League Client API não é oficialmente suportada para terceiros.
- Binários do beta não terão assinatura paga; checksums serão obrigatórios.
- Sem serviço externo, não há alerta proativo quando o próprio PC fica offline.
