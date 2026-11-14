using SmartX.PoE.Api.Models;
using SmartX.PoE.Api.Services;
using Xunit;

namespace SmartX.PoE.Tests;

public class TelemetryStoreTests
{
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 11, 13, hour, minute, 0, TimeSpan.Zero);

    private static TelemetryReading Reading(string sensor, DateTimeOffset time, string metric, double value) =>
        new(sensor, time, metric, value, "u");

    [Fact]
    public void Query_ReturnsOnlyTheRequestedWindow_Inclusive()
    {
        var store = new TelemetryStore();
        store.Add(Reading("N-01", At(13, 59), "moisture", 1));
        store.Add(Reading("N-01", At(14), "moisture", 2));
        store.Add(Reading("N-01", At(15), "moisture", 3));
        store.Add(Reading("N-01", At(16), "moisture", 4));
        store.Add(Reading("N-01", At(16, 1), "moisture", 5));

        TelemetryQueryResult result = store.Query("N-01", At(14), At(16));

        Assert.Equal(new[] { 2.0, 3.0, 4.0 }, result.Readings.Select(r => r.Value));
        Assert.Equal(3, result.TotalMatches);
        Assert.Equal(5, result.TotalStored);
    }

    [Fact]
    public void Query_FiltersByMetric()
    {
        var store = new TelemetryStore();
        store.Add(Reading("N-01", At(14), "moisture", 40));
        store.Add(Reading("N-01", At(14, 5), "temperature", 21));
        store.Add(Reading("N-01", At(14, 10), "moisture", 41));

        TelemetryQueryResult result = store.Query("N-01", At(13), At(15), "MOISTURE");

        Assert.All(result.Readings, r => Assert.Equal("moisture", r.Metric));
        Assert.Equal(2, result.Readings.Count);
    }

    [Fact]
    public void Add_KeepsReadingsThatShareATimestamp()
    {
        var store = new TelemetryStore();
        store.Add(Reading("N-01", At(14), "moisture", 40));
        store.Add(Reading("N-01", At(14), "temperature", 21));

        Assert.Equal(2, store.Query("N-01", At(14), At(14)).Readings.Count);
    }

    [Fact]
    public void Query_KeepsSensorsSeparate()
    {
        var store = new TelemetryStore();
        store.Add(Reading("N-01", At(14), "moisture", 1));
        store.Add(Reading("N-02", At(14), "moisture", 2));

        TelemetryQueryResult result = store.Query("N-02", At(13), At(15));

        Assert.Equal(2.0, Assert.Single(result.Readings).Value);
    }

    [Fact]
    public void Query_WhenMoreMatchesThanTheLimit_ReturnsTheMostRecent()
    {
        var store = new TelemetryStore();
        for (int i = 0; i < 10; i++) store.Add(Reading("N-01", At(10).AddMinutes(i), "moisture", i));

        TelemetryQueryResult result = store.Query("N-01", At(9), At(12), limit: 3);

        Assert.Equal(10, result.TotalMatches);
        Assert.Equal(new[] { 7.0, 8.0, 9.0 }, result.Readings.Select(r => r.Value));
    }

    [Fact]
    public void Query_UnknownSensor_IsEmpty()
    {
        var result = new TelemetryStore().Query("N-99", At(0), At(23));

        Assert.Empty(result.Readings);
        Assert.Equal(0, result.TotalStored);
    }

    [Fact]
    public void Summarise_ReportsEarliestLatestAndCount()
    {
        var store = new TelemetryStore();
        store.Add(Reading("N-01", At(12), "moisture", 1));
        store.Add(Reading("N-01", At(9), "moisture", 2));
        store.Add(Reading("N-01", At(15), "moisture", 3));

        TelemetrySummary summary = store.Summarise("N-01");

        Assert.Equal(3, summary.Count);
        Assert.Equal(At(9), summary.Earliest);
        Assert.Equal(At(15), summary.Latest);
    }

    [Fact]
    public void LargeHistory_StaysBalanced()
    {
       
        var store = new TelemetryStore();
        for (int i = 0; i < 20_000; i++) store.Add(Reading("N-01", At(0).AddSeconds(i), "moisture", i));

        TelemetryQueryResult result = store.Query("N-01", At(0).AddSeconds(100), At(0).AddSeconds(199));

        Assert.Equal(100, result.TotalMatches);
        Assert.True(result.TreeHeight <= 2 * Math.Ceiling(Math.Log2(20_001)));
    }
}
