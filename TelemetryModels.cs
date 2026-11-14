namespace SmartX.PoE.Api.Models;

public sealed record TelemetryReading(
    string SensorId,
    DateTimeOffset Timestamp,
    string Metric,
    double Value,
    string Unit);

public readonly record struct TelemetryKey(long UtcTicks, long Sequence) : IComparable<TelemetryKey>
{
    public int CompareTo(TelemetryKey other)
    {
        int byTime = UtcTicks.CompareTo(other.UtcTicks);
        return byTime != 0 ? byTime : Sequence.CompareTo(other.Sequence);
    }
}

public sealed record TelemetryQueryResult(
    string SensorId,
    IReadOnlyList<TelemetryReading> Readings,
    int TotalMatches,
    int TotalStored,
    int TreeHeight,
    double ElapsedMs);
