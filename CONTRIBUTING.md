# Como contribuir

## Princípio

A IA desenvolve e documenta; Nícolas testa, decide e faz os commits. O código
deve continuar compreensível, testado e ligado a um resultado concreto.

## Fluxo

1. Trabalhe direto na `main`, um checkpoint por vez (decisão D2 em
   [`docs/decisoes.md`](docs/decisoes.md)).
2. Faça mudanças pequenas, com testes proporcionais ao risco.
3. Atualize `CHANGELOG.md`, o checklist de `docs/roadmap.md` e, se houver
   decisão, `docs/decisoes.md`.
4. Rode build e testes (veja [`docs/simulador.md`](docs/simulador.md)).
5. Faça o commit antes de iniciar o próximo checkpoint e envie com `git push`.
6. Confirme que o CI do GitHub Actions passou.

Use Conventional Commits em português, por exemplo:

```text
feat: adiciona simulador de janela do League
test: valida conversão de coordenadas normalizadas
docs: registra decisão do transporte de vídeo
```

Versões aprovadas recebem uma tag (`v0.1`, `v0.2`, ...).

## Qualidade mínima

- Build sem avisos (`TreatWarningsAsErrors` está ativo).
- Testes unitários e de integração relevantes passando.
- Nenhum segredo ou dado sensível no diff.
- `git diff --check` sem problemas.
- Evidência manual para comportamentos dependentes de Windows ou iPhone,
  registrada no changelog ou no documento da área.
