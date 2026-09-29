using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

public static class TestPdf
{
    public static byte[] WithText(params string[] lines)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        var top = 780;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(50, top), font);
            top -= 20;
        }

        return builder.Build();
    }

    /// <summary>
    /// Like a scanned document: something is drawn on the page, but there is no text layer.
    /// </summary>
    public static byte[] WithoutText()
    {
        using var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4).DrawRectangle(new PdfPoint(50, 50), 200, 100, 1, fill: true);

        return builder.Build();
    }
}
