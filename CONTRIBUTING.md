# Como contribuir

## Princípio

O projeto é desenvolvido como aprendizagem prática. Contribuições devem manter
o código compreensível, revisável e ligado a um resultado concreto.

## Fluxo

1. Escolha ou crie uma issue aprovada no milestone correto.
2. Confirme aceite, dependências, labels, responsável e `Size` no Project #4.
3. Crie uma branch ligada à issue no formato `numero-descricao-curta`.
4. Faça mudanças pequenas e inclua testes proporcionais ao risco.
5. Atualize documentação estável quando necessário.
6. Abra Pull Request com vínculo à issue e instruções de teste.

Use Conventional Commits em português, por exemplo:

```text
feat: adiciona simulador de janela do League
test: valida conversão de coordenadas normalizadas
docs: registra decisão do transporte de vídeo
```

## Qualidade mínima

- Código formatado e análise estática sem erros.
- Testes unitários e de integração relevantes passando.
- Nenhum segredo ou dado sensível no diff.
- `git diff --check` sem problemas.
- Evidência manual para comportamentos dependentes de Windows ou iPhone.

## Aprendizagem

Antes de copiar uma solução, explique com suas palavras o problema, a escolha e
como testá-la. A IA pode orientar, revisar e depurar, mas o objetivo é que o
autor compreenda e escreva a implementação.
