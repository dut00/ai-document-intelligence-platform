using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Application.Documents.Processing;

namespace DocumentIntelligence.UnitTests.Application;

public sealed class AnalysisResultValidatorTests
{
    private readonly AnalysisResultValidator _validator = new();

    [Fact]
    public void Complete_analysis_is_valid()
    {
        _validator.Validate(ValidResult()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Empty_lists_are_valid()
    {
        var result = ValidResult() with { Entities = [], ImportantDates = [], FinancialInformation = [], PotentialRisks = [] };

        _validator.Validate(result).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("1")]
    [InlineData("Company")]
    public void Entity_type_must_be_an_exact_enum_name(string type)
    {
        var result = ValidResult() with { Entities = [new AnalyzedEntity(type, "Acme")] };

        _validator.Validate(result).Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Entities[0].Type");
    }

    [Theory]
    [InlineData("11.11.2026")]
    [InlineData("2026-02-30")]
    [InlineData("2026-11-11T00:00:00")]
    public void Date_must_be_a_valid_iso_date(string date)
    {
        var result = ValidResult() with { ImportantDates = [new AnalyzedDate(date, "PaymentDeadline", "Payment due")] };

        _validator.Validate(result).Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("ImportantDates[0].Date");
    }

    [Theory]
    [InlineData("pln")]
    [InlineData("zł")]
    [InlineData("EURO")]
    public void Currency_must_be_an_iso_code(string currency)
    {
        var result = ValidResult() with { FinancialInformation = [new AnalyzedAmount("Total", 10m, currency)] };

        _validator.Validate(result).Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("FinancialInformation[0].Currency");
    }

    [Fact]
    public void Missing_values_are_reported_individually()
    {
        var result = new AnalysisResult("", "", null!, null!, null!, null!);

        var errors = _validator.Validate(result).Errors.Select(error => error.PropertyName);

        errors.ShouldBe(
            ["DocumentType", "Summary", "Entities", "ImportantDates", "FinancialInformation", "PotentialRisks"],
            ignoreOrder: true);
    }

    [Fact]
    public void Too_many_items_are_rejected()
    {
        var risks = Enumerable.Range(0, AnalysisResultValidator.MaxItems + 1)
            .Select(index => new AnalyzedRisk("Low", $"Risk {index}"))
            .ToList();

        var errors = _validator.Validate(ValidResult() with { PotentialRisks = risks }).Errors;

        errors.ShouldHaveSingleItem().PropertyName.ShouldBe("PotentialRisks");
    }

    private static AnalysisResult ValidResult() => new(
        "Invoice",
        "An invoice for consulting services.",
        [new AnalyzedEntity("Organization", "Acme")],
        [new AnalyzedDate("2026-11-11", "PaymentDeadline", "Invoice payment due date")],
        [new AnalyzedAmount("Total due", 1200.50m, "PLN")],
        [new AnalyzedRisk("Medium", "Late payment penalty of 0.5% per day")]);
}
