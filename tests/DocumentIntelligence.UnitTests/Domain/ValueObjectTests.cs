using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Analysis;

namespace DocumentIntelligence.UnitTests.Domain;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData(@"C:\Users\me\report.PDF", "report.PDF")]
    [InlineData("/home/me/notes.txt", "notes.txt")]
    public void FileName_keeps_only_the_last_path_segment(string input, string expected)
    {
        new FileName(input).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("folder/")]
    public void FileName_rejects_empty_names(string input)
    {
        Should.Throw<DomainException>(() => new FileName(input));
    }

    [Fact]
    public void FileName_extension_is_lower_case()
    {
        new FileName("REPORT.PDF").Extension.ShouldBe(".pdf");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(FileSize.MaxBytes + 1)]
    public void FileSize_rejects_values_outside_the_allowed_range(long bytes)
    {
        Should.Throw<DomainException>(() => new FileSize(bytes));
    }

    [Fact]
    public void FileSize_accepts_the_maximum()
    {
        new FileSize(FileSize.MaxBytes).Bytes.ShouldBe(FileSize.MaxBytes);
    }

    [Theory]
    [InlineData("application/pdf", ".pdf")]
    [InlineData("APPLICATION/PDF", ".pdf")]
    [InlineData("text/plain; charset=utf-8", ".txt")]
    public void ContentType_recognizes_supported_mime_types(string mimeType, string extension)
    {
        ContentType.TryFromMimeType(mimeType, out var contentType).ShouldBeTrue();
        contentType.Extension.ShouldBe(extension);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("")]
    [InlineData(null)]
    public void ContentType_rejects_unsupported_mime_types(string? mimeType)
    {
        ContentType.TryFromMimeType(mimeType, out _).ShouldBeFalse();
    }

    [Fact]
    public void Money_normalizes_currency_code()
    {
        new Money(10m, " eur ").ShouldBe(new Money(10m, "EUR"));
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("12$")]
    public void Money_rejects_invalid_currency_codes(string currency)
    {
        Should.Throw<DomainException>(() => new Money(1m, currency));
    }

    [Fact]
    public void CalendarCheck_is_business_day_only_when_not_weekend_and_not_holiday()
    {
        var nextDay = new DateOnly(2026, 11, 12);

        new CalendarCheck(false, null, nextDay).IsBusinessDay.ShouldBeTrue();
        new CalendarCheck(true, null, nextDay).IsBusinessDay.ShouldBeFalse();
        new CalendarCheck(false, "National Independence Day", nextDay).IsBusinessDay.ShouldBeFalse();
    }

    [Fact]
    public void ImportantDate_rejects_next_business_day_not_after_the_date()
    {
        var date = new DateOnly(2026, 11, 11);

        Should.Throw<DomainException>(() => new ImportantDate(
            date, ImportantDateType.PaymentDeadline, "Invoice due", new CalendarCheck(false, "Holiday", date)));
    }
}
