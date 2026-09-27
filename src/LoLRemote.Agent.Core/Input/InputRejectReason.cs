namespace LoLRemote.Agent.Core.Input;

/// <summary>
/// Motivos de recusa de um comando de input. Os nomes em kebab-case são os
/// valores do campo "reason" em contracts/schemas/control-message.schema.json.
/// </summary>
public enum InputRejectReason
{
    /// <summary>Versão do protocolo não suportada.</summary>
    UnsupportedVersion,

    /// <summary>Sequência repetida ou menor que a última recebida (possível replay).</summary>
    OutOfOrder,

    /// <summary>Comando antigo demais.</summary>
    Expired,

    /// <summary>Comando com horário no futuro além da tolerância.</summary>
    FromFuture,

    /// <summary>Muitos comandos em pouco tempo.</summary>
    RateLimited,

    /// <summary>Coordenadas fora de 0..1 ou não numéricas.</summary>
    InvalidCoordinates,

    /// <summary>Rolagem zero ou maior que o limite.</summary>
    InvalidScroll,

    /// <summary>Texto vazio, longo demais ou com caracteres de controle.</summary>
    InvalidText,

    /// <summary>Tecla fora da lista permitida.</summary>
    InvalidKey,

    /// <summary>Toque na faixa preta do vídeo.</summary>
    Letterbox,

    /// <summary>Toque na barra de título ou bordas da janela.</summary>
    OutsideClientArea,

    /// <summary>Janela alvo não encontrada ou não validada.</summary>
    TargetUnavailable,

    /// <summary>Janela alvo minimizada.</summary>
    TargetMinimized,

    /// <summary>Janela alvo não pôde ficar em primeiro plano ou outra janela cobre o ponto.</summary>
    TargetObscured,

    /// <summary>Sem frame recente da janela: a pessoa não está vendo o estado atual.</summary>
    CaptureStale,

    /// <summary>Fase do jogo desconhecida.</summary>
    PhaseUnknown,

    /// <summary>Fase do jogo não permite input (partida em andamento, carregando, etc.).</summary>
    PhaseBlocked,

    /// <summary>Atividade local recente no PC: input remoto pausado.</summary>
    LocalActivity,

    /// <summary>Modo remoto não está ativo ou expirou.</summary>
    RemoteModeInactive,
}
