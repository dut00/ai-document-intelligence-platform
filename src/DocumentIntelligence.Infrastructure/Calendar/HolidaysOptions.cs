using System.ComponentModel.DataAnnotations;

namespace DocumentIntelligence.Infrastructure.Calendar;

public sealed class HolidaysOptions
{
    public const string SectionName = "Holidays";

    /// <summary>
    /// Base address of the Nager.Date API.
    /// </summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("https://date.nager.at/");

    /// <summary>
    /// ISO 3166-1 alpha-2 code of the country whose holidays apply, e.g. "PL".
    /// </summary>
    [Required]
    [RegularExpression("^[A-Z]{2}$")]
    public string CountryCode { get; set; } = "PL";

    /// <summary>
    /// Holidays rarely change, so a year's list is kept in memory this long.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);
}
