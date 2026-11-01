using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleScout.Contracts;

/// <summary>Source-generated, so the AOT-compiled CLI and the agent serialize the wire format identically.</summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FetchRequest))]
[JsonSerializable(typeof(FetchResponse))]
[JsonSerializable(typeof(FetchBatchRequest))]
[JsonSerializable(typeof(FetchBatchResponse))]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(AppendHistoryRequest))]
[JsonSerializable(typeof(AppendHistoryResponse))]
[JsonSerializable(typeof(HistoryThreadRequest))]
[JsonSerializable(typeof(HistoryThreadResponse))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(AgentError))]
[JsonSerializable(typeof(AgentEndpoint))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
