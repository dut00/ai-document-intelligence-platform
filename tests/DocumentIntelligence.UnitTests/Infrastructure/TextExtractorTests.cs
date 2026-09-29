using System.Text;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Infrastructure.TextExtraction;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentIntelligence.UnitTests.Infrastructure;

public sealed class TextExtractorTests
{
    private readonly PdfTextExtractor _pdf = new();
    private readonly PlainTextExtractor _text = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void Each_extractor_handles_its_own_content_type()
    {
        _pdf.CanExtract(ContentType.Pdf).ShouldBeTrue();
        _pdf.CanExtract(ContentType.Txt).ShouldBeFalse();
        _text.CanExtract(ContentType.Txt).ShouldBeTrue();
        _text.CanExtract(ContentType.Pdf).ShouldBeFalse();
    }

    [Fact]
    public async Task Pdf_text_layer_is_extracted_from_every_page()
    {
        var pdf = BuildPdf(["Invoice 7/2026", "Total: 99.90 EUR"], ["Due date: 2026-12-23"]);

        var text = await _pdf.ExtractTextAsync(new MemoryStream(pdf), CancellationToken);

        text.ShouldContain("Invoice 7/2026");
        text.ShouldContain("Total: 99.90 EUR");
        text.ShouldContain("Due date: 2026-12-23");
    }

    [Fact]
    public async Task Pdf_without_text_layer_yields_no_text()
    {
        var pdf = BuildPdf([]);

        var text = await _pdf.ExtractTextAsync(new MemoryStream(pdf), CancellationToken);

        text.ShouldBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Damaged_pdf_is_unprocessable()
    {
        var damaged = Encoding.ASCII.GetBytes("%PDF-1.7\nthis is not really a PDF");

        await Should.ThrowAsync<UnprocessableDocumentException>(
            () => _pdf.ExtractTextAsync(new MemoryStream(damaged), CancellationToken));
    }

    [Fact]
    public async Task Text_file_is_read_as_utf8()
    {
        var text = await _text.ExtractTextAsync(new MemoryStream(Encoding.UTF8.GetBytes("Zażółć gęślą jaźń")), CancellationToken);

        text.ShouldBe("Zażółć gęślą jaźń");
    }

    [Fact]
    public async Task Text_file_with_a_byte_order_mark_uses_its_encoding()
    {
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Umowa najmu")).ToArray();

        var text = await _text.ExtractTextAsync(new MemoryStream(utf16), CancellationToken);

        text.ShouldBe("Umowa najmu");
    }

    private static byte[] BuildPdf(params string[][] pages)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        if (pages.Length == 0)
        {
            builder.AddPage(PageSize.A4).DrawRectangle(new PdfPoint(50, 50), 200, 100, 1, fill: true);
        }

        foreach (var lines in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            for (var index = 0; index < lines.Length; index++)
            {
                page.AddText(lines[index], 12, new PdfPoint(50, 780 - (index * 20)), font);
            }
        }

        return builder.Build();
    }
}
