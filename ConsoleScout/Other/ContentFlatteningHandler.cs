using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ConsoleScout.Other;

/// <summary>
/// Some OpenAI-compatible providers return message.content as an array of chunks
/// ({"type":"text"}, {"type":"reference"}, ...) rather than a string - that is how
/// Citations &amp; References are delivered. The OpenAI SDK only knows
/// text/refusal/image_url/input_audio/file and throws ArgumentOutOfRangeException, so the array is
/// flattened into a string first. Responses with nothing to flatten pass through untouched.
/// </summary>
internal sealed class ContentFlatteningHandler() : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);

        // Streaming (text/event-stream) and binary responses are left alone entirely.
        if (response.Content.Headers.ContentType?.MediaType != "application/json")
        {
            return response;
        }

        // Read as bytes: JsonDocument parses UTF-8 directly, with no decoding into a string.
        var payload = await response.Content.ReadAsByteArrayAsync(ct);
        if (!TryFlatten(payload, out var flattened))
        {
            return response; // HttpContent has already buffered the body, so the SDK can read it again.
        }

        response.Content.Dispose();
        response.Content = new ByteArrayContent(flattened);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        return response;
    }

    /// <summary>False means there is nothing to rewrite, and the caller returns the original response as is.</summary>
    private static bool TryFlatten(byte[] payload, out byte[] flattened)
    {
        flattened = [];

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            if (!NeedsFlattening(document.RootElement))
            {
                return false;
            }

            var buffer = new ArrayBufferWriter<byte>(payload.Length);
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteRoot(document.RootElement, writer);
            }

            flattened = buffer.WrittenSpan.ToArray();
            return true;
        }
    }

    /// <summary>Whether any choice carries content as an array. Cheaper than rebuilding the JSON blindly.</summary>
    private static bool NeedsFlattening(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.ValueKind == JsonValueKind.Object
                && choice.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.Object
                && message.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteRoot(JsonElement root, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals("choices") && property.Value.ValueKind == JsonValueKind.Array)
            {
                writer.WriteStartArray("choices");
                foreach (var choice in property.Value.EnumerateArray())
                {
                    WriteChoice(choice, writer);
                }

                writer.WriteEndArray();
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteChoice(JsonElement choice, Utf8JsonWriter writer)
    {
        if (choice.ValueKind != JsonValueKind.Object)
        {
            choice.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        foreach (var property in choice.EnumerateObject())
        {
            if (property.NameEquals("message") && property.Value.ValueKind == JsonValueKind.Object)
            {
                WriteMessage(property.Value, writer);
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteMessage(JsonElement message, Utf8JsonWriter writer)
    {
        writer.WriteStartObject("message");
        foreach (var property in message.EnumerateObject())
        {
            if (property.NameEquals("content") && property.Value.ValueKind == JsonValueKind.Array)
            {
                var flattened = FlattenContent(property.Value);
                writer.WriteString("content", flattened);
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static string FlattenContent(JsonElement content)
    {
        var builder = new StringBuilder();
        foreach (var chunk in content.EnumerateArray())
        {
            if (chunk.ValueKind == JsonValueKind.String)
            {
                var value = chunk.GetString();
                builder.Append(value);
                continue;
            }

            if (chunk.ValueKind != JsonValueKind.Object
                || !chunk.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            // ValueEquals compares against a UTF-8 literal without materializing the string.
            if (type.ValueEquals("text"))
            {
                AppendText(chunk, builder);
            }

            // Everything else is skipped: reference chunks because answers carry no citation markers, and
            // other types (thinking, image_url, ...) because not crashing matters more than not losing data.
        }

        return builder.ToString();
    }

    private static void AppendText(JsonElement chunk, StringBuilder builder)
    {
        if (chunk.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            var value = text.GetString();
            builder.Append(value);
        }
    }
}
