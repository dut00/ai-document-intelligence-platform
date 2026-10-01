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
    private const int MaxCharacters = 1_000;

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

        var text = await _pdf.ExtractTextAsync(new MemoryStream(pdf), MaxCharacters, CancellationToken);

        text.ShouldContain("Invoice 7/2026");
        text.ShouldContain("Total: 99.90 EUR");
        text.ShouldContain("Due date: 2026-12-23");
    }

    [Fact]
    public async Task Pdf_without_text_layer_yields_no_text()
    {
        var pdf = BuildPdf([]);

        var text = await _pdf.ExtractTextAsync(new MemoryStream(pdf), MaxCharacters, CancellationToken);

        text.ShouldBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Damaged_pdf_is_unprocessable()
    {
        var damaged = Encoding.ASCII.GetBytes("%PDF-1.7\nthis is not really a PDF");

        await Should.ThrowAsync<UnprocessableDocumentException>(
            () => _pdf.ExtractTextAsync(new MemoryStream(damaged), MaxCharacters, CancellationToken));
    }

    [Fact]
    public async Task Text_file_is_read_as_utf8()
    {
        var text = await _text.ExtractTextAsync(new MemoryStream(Encoding.UTF8.GetBytes("Zażółć gęślą jaźń")), MaxCharacters, CancellationToken);

        text.ShouldBe("Zażółć gęślą jaźń");
    }

    [Fact]
    public async Task Text_file_with_a_byte_order_mark_uses_its_encoding()
    {
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Umowa najmu")).ToArray();

        var text = await _text.ExtractTextAsync(new MemoryStream(utf16), MaxCharacters, CancellationToken);

        text.ShouldBe("Umowa najmu");
    }

    [Fact]
    public async Task Pdf_reading_stops_once_the_text_budget_is_exceeded()
    {
        var pages = Enumerable.Range(1, 5).Select(page => new[] { $"Page {page}: {new string('x', 40)}" }).ToArray();

        var text = await _pdf.ExtractTextAsync(new MemoryStream(BuildPdf(pages)), maxCharacters: 60, CancellationToken);

        text.ShouldContain("Page 2");
        text.ShouldNotContain("Page 3");
    }

    [Fact]
    public async Task Pdf_reading_stops_after_the_page_limit()
    {
        var pages = Enumerable.Range(1, PdfTextExtractor.MaxPages + 1).Select(page => new[] { $"P{page}." }).ToArray();

        var text = await _pdf.ExtractTextAsync(new MemoryStream(BuildPdf(pages)), maxCharacters: 100_000, CancellationToken);

        text.ShouldContain($"P{PdfTextExtractor.MaxPages}.");
        text.ShouldNotContain($"P{PdfTextExtractor.MaxPages + 1}.");
    }

    [Fact]
    public async Task Leading_whitespace_does_not_use_up_the_text_budget()
    {
        var content = Encoding.UTF8.GetBytes(new string(' ', 500) + new string('\n', 500) + "Invoice 7/2026");

        var text = await _text.ExtractTextAsync(new MemoryStream(content), maxCharacters: 100, CancellationToken);

        text.ShouldBe("Invoice 7/2026");
    }

    [Fact]
    public async Task Leading_whitespace_is_skipped_on_a_stream_that_returns_short_reads()
    {
        var content = Encoding.UTF8.GetBytes(new string(' ', 70_000) + "Invoice 7/2026 " + new string('x', 200));

        var text = await _text.ExtractTextAsync(new TrickleStream(content), maxCharacters: 100, CancellationToken);

        text.ShouldStartWith("Invoice 7/2026");
        text.Length.ShouldBe(101);
    }

    [Fact]
    public async Task Leading_blank_pdf_pages_do_not_use_up_the_text_budget()
    {
        var pages = Enumerable.Repeat(Array.Empty<string>(), 3).Append(["Invoice 7/2026"]).ToArray();

        var text = await _pdf.ExtractTextAsync(new MemoryStream(BuildPdf(pages)), maxCharacters: 5, CancellationToken);

        text.ShouldContain("Invoice 7/2026");
    }

    [Fact]
    public async Task Pdf_is_not_parsed_while_every_parser_is_busy()
    {
        using var parsers = new SemaphoreSlim(0, 1);
        var extractor = new PdfTextExtractor(parsers);
        var pdf = BuildPdf(["Invoice 7/2026"]);

        // A transient error: the message is retried once a parser is free.
        await Should.ThrowAsync<InvalidOperationException>(
            () => extractor.ExtractTextAsync(new MemoryStream(pdf), MaxCharacters, CancellationToken));

        parsers.Release();
        (await extractor.ExtractTextAsync(new MemoryStream(pdf), MaxCharacters, CancellationToken)).ShouldContain("Invoice 7/2026");
        parsers.CurrentCount.ShouldBe(1);
    }

    [Fact]
    public async Task Text_file_is_read_up_to_one_character_past_the_budget()
    {
        var text = await _text.ExtractTextAsync(new MemoryStream(Encoding.UTF8.GetBytes(new string('a', 50))), maxCharacters: 10, CancellationToken);

        text.Length.ShouldBe(11);
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
