using System.Text.Json;
using FluentAssertions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class DocumentMimeTypeXlsxTests
{
    [Fact]
    public void DocumentMimeType_Contains_Xlsx_In_Correct_Order()
    {
        var values = Enum.GetValues<DocumentMimeType>();
        values.Should().Contain(DocumentMimeType.Xlsx);
        // Order must be Pdf, Docx, Xlsx, Txt, Md
        values[0].Should().Be(DocumentMimeType.Pdf);
        values[1].Should().Be(DocumentMimeType.Docx);
        values[2].Should().Be(DocumentMimeType.Xlsx);
        values[3].Should().Be(DocumentMimeType.Txt);
        values[4].Should().Be(DocumentMimeType.Md);
        values.Should().HaveCount(5);
    }

    [Fact]
    public void Xlsx_GetContentType_Returns_Spreadsheetml()
    {
        DocumentMimeType.Xlsx.GetContentType()
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public void DocumentMimeTypeConverter_RoundTrips_Xlsx()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DocumentMimeTypeConverter());

        var json = JsonSerializer.Serialize(DocumentMimeType.Xlsx, options);
        json.Should().Be("\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet\"");

        var deserialized = JsonSerializer.Deserialize<DocumentMimeType>(json, options);
        deserialized.Should().Be(DocumentMimeType.Xlsx);
    }

    [Fact]
    public void DocumentMimeTypeConverter_Throws_On_Unknown()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DocumentMimeTypeConverter());

        var act = () => JsonSerializer.Deserialize<DocumentMimeType>("\"unknown/type\"", options);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void MaxSpreadsheetCells_Is_100k()
    {
        DocumentValidation.MaxSpreadsheetCells.Should().Be(100_000);
    }

    [Fact]
    public void EnumDataType_ErrorMessage_Lists_Xlsx()
    {
        var attr = (System.ComponentModel.DataAnnotations.EnumDataTypeAttribute)typeof(Document)
            .GetProperty(nameof(Document.Mime))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.EnumDataTypeAttribute), false)[0];
        attr.ErrorMessage.Should().Contain("xlsx");
    }
}
