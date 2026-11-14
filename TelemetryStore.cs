using System.Diagnostics;
using SmartX.PoE.Api.DataStructures;
using SmartX.PoE.Api.Models;

namespace SmartX.PoE.Api.Services;

public sealed class TelemetryStore
{
    private readonly Dictionary<string, RedBlackTree<TelemetryKey, TelemetryReading>> _trees =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _gate = new();
    private long _sequence;

    public int TotalReadings
    {
        get
        {
            lock (_gate) return _trees.Values.Sum(t => t.Count);
        }
    }

    public void Add(TelemetryReading reading)
    {
        lock (_gate)
        {
            if (!_trees.TryGetValue(reading.SensorId, out var tree))
            {
                tree = new RedBlackTree<TelemetryKey, TelemetryReading>();
                _trees[reading.SensorId] = tree;
            }

            var key = new TelemetryKey(reading.Timestamp.UtcTicks, ++_sequence);
            tree.Insert(key, reading);
        }
    }

    public TelemetryQueryResult Query(
        string sensorId,
        DateTimeOffset start,
        DateTimeOffset end,
        string? metric = null,
        int limit = 500)
    {
        long started = Stopwatch.GetTimestamp();

        lock (_gate)
        {
            if (!_trees.TryGetValue(sensorId, out var tree))
            {
                return new TelemetryQueryResult(sensorId, Array.Empty<TelemetryReading>(), 0, 0, 0,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }

            var low = new TelemetryKey(start.UtcTicks, long.MinValue);
            var high = new TelemetryKey(end.UtcTicks, long.MaxValue);

            List<TelemetryReading> inWindow = tree.Range(low, high);

            if (!string.IsNullOrWhiteSpace(metric))
            {
                inWindow = inWindow
                    .Where(r => string.Equals(r.Metric, metric, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            int totalMatches = inWindow.Count;
            IReadOnlyList<TelemetryReading> page = totalMatches > limit
                ? inWindow.GetRange(totalMatches - limit, limit)
                : inWindow;

            return new TelemetryQueryResult(
                sensorId,
                page,
                totalMatches,
                tree.Count,
                tree.Height(),
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    public TelemetrySummary Summarise(string sensorId)
    {
        lock (_gate)
        {
            if (!_trees.TryGetValue(sensorId, out var tree) || tree.IsEmpty)
                return new TelemetrySummary(sensorId, 0, null, null, 0);

            tree.TryGetMin(out _, out TelemetryReading? first);
            tree.TryGetMax(out _, out TelemetryReading? last);
            return new TelemetrySummary(sensorId, tree.Count, first!.Timestamp, last!.Timestamp, tree.Height());
        }
    }
}
