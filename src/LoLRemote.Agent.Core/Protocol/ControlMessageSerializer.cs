using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoLRemote.Agent.Core.Protocol;

/// <summary>Enum serializado em kebab-case minúsculo (ex.: "out-of-order").</summary>
/// <typeparam name="TEnum">Tipo do enum.</typeparam>
public sealed class KebabCaseEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Cria o conversor sem aceitar valores numéricos.</summary>
    public KebabCaseEnumConverter()
        : base(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false)
    {
    }
}

/// <summary>Leitura e escrita das mensagens de controle, com limites de tamanho.</summary>
public static class ControlMessageSerializer
{
    /// <summary>Tamanho máximo de uma mensagem, em caracteres.</summary>
    public const int MaxMessageLength = 4096;

    private static readonly JsonSerializerOptions Options = new()
    {
        AllowOutOfOrderMetadataProperties = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 4,
    };

    /// <summary>Serializa uma mensagem.</summary>
    public static string Serialize(ControlMessage message) =>
        JsonSerializer.Serialize(message, Options);

    /// <summary>Lê uma mensagem; retorna false para JSON inválido, tipo desconhecido ou campo extra.</summary>
    public static bool TryParse(string json, out ControlMessage? message)
    {
        message = null;
        if (string.IsNullOrEmpty(json) || json.Length > MaxMessageLength)
        {
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize<ControlMessage>(json, Options);
            return message is not null;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
