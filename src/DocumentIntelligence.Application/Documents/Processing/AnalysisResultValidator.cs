using System.Globalization;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Domain.Documents.Analysis;
using FluentValidation;

namespace DocumentIntelligence.Application.Documents.Processing;

/// <summary>
/// Guards the domain against whatever the AI returns. Messages are written for the model:
/// an invalid response is sent back to it once, together with these errors.
/// </summary>
public sealed class AnalysisResultValidator : AbstractValidator<AnalysisResult>
{
    public const int MaxDocumentTypeLength = 100;
    public const int MaxSummaryLength = 2000;
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 500;
    public const int MaxItems = 50;

    public AnalysisResultValidator()
    {
        RuleFor(result => result.DocumentType).NotEmpty().MaximumLength(MaxDocumentTypeLength);
        RuleFor(result => result.Summary).NotEmpty().MaximumLength(MaxSummaryLength);

        RuleFor(result => result.Entities).NotNull().Must(HaveAtMostMaxItems).WithMessage(TooManyItems);
        RuleForEach(result => result.Entities).NotNull().ChildRules(entity =>
        {
            entity.RuleFor(item => item.Type).Must(BeNameOf<EntityType>).WithMessage(NotOneOf<EntityType>());
            entity.RuleFor(item => item.Name).NotEmpty().MaximumLength(MaxNameLength);
        });

        RuleFor(result => result.ImportantDates).NotNull().Must(HaveAtMostMaxItems).WithMessage(TooManyItems);
        RuleForEach(result => result.ImportantDates).NotNull().ChildRules(date =>
        {
            date.RuleFor(item => item.Date).Must(BeIsoDate).WithMessage($"'{{PropertyValue}}' is not a date in the {AnalysisResult.DateFormat} format.");
            date.RuleFor(item => item.Type).Must(BeNameOf<ImportantDateType>).WithMessage(NotOneOf<ImportantDateType>());
            date.RuleFor(item => item.Description).NotEmpty().MaximumLength(MaxDescriptionLength);
        });

        RuleFor(result => result.FinancialInformation).NotNull().Must(HaveAtMostMaxItems).WithMessage(TooManyItems);
        RuleForEach(result => result.FinancialInformation).NotNull().ChildRules(amount =>
        {
            amount.RuleFor(item => item.Description).NotEmpty().MaximumLength(MaxDescriptionLength);
            amount.RuleFor(item => item.Currency)
                .Must(BeCurrencyCode)
                .WithMessage("'{PropertyValue}' is not a three-letter ISO 4217 currency code.");
        });

        RuleFor(result => result.PotentialRisks).NotNull().Must(HaveAtMostMaxItems).WithMessage(TooManyItems);
        RuleForEach(result => result.PotentialRisks).NotNull().ChildRules(risk =>
        {
            risk.RuleFor(item => item.Severity).Must(BeNameOf<RiskSeverity>).WithMessage(NotOneOf<RiskSeverity>());
            risk.RuleFor(item => item.Description).NotEmpty().MaximumLength(MaxDescriptionLength);
        });
    }

    private static string TooManyItems => $"'{{PropertyName}}' must not contain more than {MaxItems} items.";

    private static bool HaveAtMostMaxItems<T>(IReadOnlyList<T>? items) => items is null || items.Count <= MaxItems;

    // Exact names only: Enum.TryParse would also accept numbers and different casing.
    private static bool BeNameOf<TEnum>(string? value)
        where TEnum : struct, Enum =>
        value is not null && Enum.GetNames<TEnum>().Contains(value, StringComparer.Ordinal);

    private static string NotOneOf<TEnum>()
        where TEnum : struct, Enum =>
        $"'{{PropertyValue}}' is not one of: {string.Join(", ", Enum.GetNames<TEnum>())}.";

    private static bool BeIsoDate(string? value) =>
        DateOnly.TryParseExact(value, AnalysisResult.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool BeCurrencyCode(string? value) =>
        value is { Length: 3 } && value.All(char.IsAsciiLetterUpper);
}
