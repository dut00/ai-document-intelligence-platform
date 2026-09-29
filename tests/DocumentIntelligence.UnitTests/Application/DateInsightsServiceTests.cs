using DocumentIntelligence.Application.Abstractions.Calendar;
using DocumentIntelligence.Application.Documents.Processing;
using DocumentIntelligence.Domain.Documents.Analysis;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DocumentIntelligence.UnitTests.Application;

public sealed class DateInsightsServiceTests
{
    private readonly IPublicHolidayProvider _holidays = Substitute.For<IPublicHolidayProvider>();
    private readonly DateInsightsService _service;

    public DateInsightsServiceTests()
    {
        _holidays.GetPublicHolidaysAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Holidays((int)call[0]));
        _service = new DateInsightsService(_holidays, NullLogger<DateInsightsService>.Instance);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Business_day_is_neither_weekend_nor_holiday()
    {
        var check = await CheckAsync(new DateOnly(2026, 11, 3)); // Tuesday

        check.ShouldBe(new CalendarCheck(isWeekend: false, holidayName: null, new DateOnly(2026, 11, 4)));
        check.IsBusinessDay.ShouldBeTrue();
    }

    [Fact]
    public async Task Saturday_is_a_weekend_and_the_next_business_day_is_monday()
    {
        var check = await CheckAsync(new DateOnly(2026, 11, 14));

        check.ShouldBe(new CalendarCheck(isWeekend: true, holidayName: null, new DateOnly(2026, 11, 16)));
    }

    [Fact]
    public async Task Public_holiday_is_named()
    {
        var check = await CheckAsync(new DateOnly(2026, 11, 11)); // Wednesday

        check.ShouldBe(new CalendarCheck(isWeekend: false, "National Independence Day", new DateOnly(2026, 11, 12)));
        check.IsBusinessDay.ShouldBeFalse();
    }

    [Fact]
    public async Task Next_business_day_skips_a_weekend_followed_by_a_holiday()
    {
        // Friday; Saturday and Sunday are the weekend and Monday 7 December is a holiday in the stub.
        var check = await CheckAsync(new DateOnly(2026, 12, 4));

        check.NextBusinessDay.ShouldBe(new DateOnly(2026, 12, 8));
    }

    [Fact]
    public async Task Next_business_day_may_fall_into_the_following_year()
    {
        // Thursday 31 December; 1 January is a holiday and 2-3 January 2027 are the weekend.
        var check = await CheckAsync(new DateOnly(2026, 12, 31));

        check.NextBusinessDay.ShouldBe(new DateOnly(2027, 1, 4));
        await _holidays.Received(1).GetPublicHolidaysAsync(2027, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_dates_means_no_calendar_request()
    {
        var dates = await _service.AddCalendarChecksAsync([], CancellationToken);

        dates.ShouldBeEmpty();
        await _holidays.DidNotReceiveWithAnyArgs().GetPublicHolidaysAsync(default, CancellationToken);
    }

    [Fact]
    public async Task Holidays_are_requested_once_per_year()
    {
        await _service.AddCalendarChecksAsync(
            [Date(new DateOnly(2026, 3, 2)), Date(new DateOnly(2026, 5, 5)), Date(new DateOnly(2027, 3, 1))],
            CancellationToken);

        await _holidays.Received(1).GetPublicHolidaysAsync(2026, Arg.Any<CancellationToken>());
        await _holidays.Received(1).GetPublicHolidaysAsync(2027, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unavailable_calendar_leaves_the_dates_unchecked()
    {
        _holidays.GetPublicHolidaysAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));
        IReadOnlyList<ImportantDate> dates = [Date(new DateOnly(2026, 11, 11))];

        var result = await _service.AddCalendarChecksAsync(dates, CancellationToken);

        result.ShouldBe(dates);
        result[0].CalendarCheck.ShouldBeNull();
    }

    private async Task<CalendarCheck> CheckAsync(DateOnly date)
    {
        var dates = await _service.AddCalendarChecksAsync([Date(date)], CancellationToken);

        return dates.ShouldHaveSingleItem().CalendarCheck.ShouldNotBeNull();
    }

    private static ImportantDate Date(DateOnly date) => new(date, ImportantDateType.PaymentDeadline, "Payment due");

    private static IReadOnlyList<PublicHoliday> Holidays(int year) =>
    [
        new PublicHoliday(new DateOnly(year, 1, 1), "New Year's Day"),
        new PublicHoliday(new DateOnly(year, 11, 11), "National Independence Day"),
        new PublicHoliday(new DateOnly(year, 12, 7), "Test Holiday"),
    ];
}
