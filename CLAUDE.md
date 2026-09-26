# Instruções para o Claude Code

As regras para agentes deste repositório estão em `AGENTS.md`:

@AGENTS.md

## Comandos verificados

```powershell
dotnet build LoLRemote.slnx
dotnet test tests/LoLRemote.Simulator.Core.Tests
dotnet run --project src/LoLRemote.Simulator -- --fast
```

Rodar build e testes antes de dar uma mudança como concluída. Fluxo de trabalho
direto na `main`, por checkpoints: veja "Rastreabilidade" em `AGENTS.md`. O simulador WPF
abre janelas: a validação visual é feita por Nícolas.
