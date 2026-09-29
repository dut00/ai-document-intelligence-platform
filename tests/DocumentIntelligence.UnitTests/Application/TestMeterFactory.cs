using System.Diagnostics.Metrics;

namespace DocumentIntelligence.UnitTests.Application;

/// <summary>
/// Creates meters scoped to this factory, so a <c>MetricCollector</c> sees only one test's measurements.
/// </summary>
public sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        options.Scope = this;
        var meter = new Meter(options);
        _meters.Add(meter);

        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
    }
}
