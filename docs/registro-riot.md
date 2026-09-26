# Registro na Riot

## Por que registrar

- Os [Termos de Serviço da Riot](https://www.riotgames.com/en/terms-of-service)
  (seção 7.1, item 11) proíbem programas de terceiros **não autorizados** que
  interajam com os serviços da Riot.
- A [política geral de desenvolvedores](https://developer.riotgames.com/policies/general)
  exige que todo produto seja registrado e auditado no Developer Portal, e que
  quem usa a League Client API registre o produto.
- A [documentação do League](https://developer.riotgames.com/docs/lol) diz que a
  League Client API **não tem suporte oficial** para terceiros e pede que o
  desenvolvedor informe quais endpoints usa e como.

Pesquisa feita em 26/09/2026. Não é aconselhamento jurídico: o registro
reduz o risco, mas só uma resposta da Riot confirma o que é aceito.

## Como enviar (Nícolas)

1. Entre em <https://developer.riotgames.com> com a sua conta Riot.
2. Escolha registrar um produto (o tipo de uso pessoal basta; não precisamos de
   chave de produção nem da API web da Riot).
3. Preencha com o texto abaixo, em inglês. Os nomes dos campos podem variar.
4. Guarde a confirmação e registre a resposta da Riot em `docs/decisoes.md`.

## Texto para o formulário

**Product name:** LoL Remote

**Product URL:** https://github.com/ya-labs/LoL-Remote

**Description:**

> LoL Remote is a personal, non-commercial remote viewer for the League
> Client, used by at most six friends, each only with their own PC and iPhone
> over a private Tailscale network (never exposed to the public internet).
>
> It streams only the League Client window (never the game and never the
> desktop) to the player's phone, and turns the player's explicit taps into
> mouse clicks on the client. The goal is to answer a Ready Check or pick in
> Champion Select while briefly away from the PC.
>
> It is not a bot: it never accepts, declines, bans, picks or plays on its own.
> Every action comes from a tap by the player. Remote input is blocked while a
> match is loading or in progress, when the phase is unknown, or when someone
> is using the PC locally.
>
> League Client API usage (read-only): `GET /lol-gameflow/v1/gameflow-phase`,
> polled every 500 ms on 127.0.0.1, used only to decide when remote input must
> be blocked. No other endpoints and no write calls. No game memory access, no
> injection into the game process, no overlays, no data collection or sharing.
>
> We would appreciate confirmation that this use is acceptable, or guidance on
> what to change.

## Estado

- 26/09/2026: produto registrado por Nícolas como **Personal API Key** (uso
  pessoal ou comunidade pequena e privada), com a descrição acima. A Riot
  emitiu uma chave pessoal (20 requisições/s, 100 a cada 2 min).
- A chave vale só para a API web da Riot, que o LoL Remote **não usa**. Ela não
  deve ser colada em conversas nem gravada no repositório.
- Nenhuma resposta da Riot sobre o uso da League Client API até agora.
