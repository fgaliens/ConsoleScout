using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleScout.Configuration;

/// <summary>Source-generated metadata: reflection-based serialization is unavailable under PublishAot.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(SecretsFile))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
