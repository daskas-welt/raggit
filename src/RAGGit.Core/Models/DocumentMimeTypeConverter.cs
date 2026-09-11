using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAGGit.Core.Models;

/// <summary>
/// JSON converter that maps <see cref="DocumentMimeType"/> to the full MIME
/// content-type strings required by contracts/api.yaml.
/// </summary>
public sealed class DocumentMimeTypeConverter : JsonConverter<DocumentMimeType>
{
    public override DocumentMimeType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString();
        return value switch
        {
            "application/pdf" => DocumentMimeType.Pdf,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                DocumentMimeType.Docx,
            "text/plain" => DocumentMimeType.Txt,
            "text/markdown" => DocumentMimeType.Md,
            _ => throw new JsonException($"Unsupported MIME type: {value}"),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        DocumentMimeType value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStringValue(value.GetContentType());
    }
}
