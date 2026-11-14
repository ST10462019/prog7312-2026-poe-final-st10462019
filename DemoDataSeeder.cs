using SmartX.PoE.Api.Models;

namespace SmartX.PoE.Api.Services;

public static class DemoDataSeeder
{
    public static void Seed(TopologyService topology, TelemetryStore telemetry)
    {
        topology.AddNode(new MeshNode("GW-01", "24:6F:28:A1:00:00", "Central Gateway", "core", isGateway: true));

        for (int i = 1; i <= 11; i++)
        {
            string subnet = i <= 4 ? "zone-a" : i <= 8 ? "zone-b" : "zone-c";
            topology.AddNode(new MeshNode($"N-{i:00}", $"24:6F:28:A1:00:{i:X2}", $"Sensor {i:00}", subnet));
        }

        (string, string, double)[] links =
        {
            ("GW-01", "N-01", 2.0), ("GW-01", "N-02", 3.0), ("GW-01", "N-05", 2.5),
            ("N-01", "N-02", 1.5), ("N-01", "N-03", 2.0), ("N-02", "N-04", 2.5), ("N-03", "N-04", 1.0),
            ("N-03", "N-07", 3.5), ("N-04", "N-08", 4.0),
            ("N-05", "N-06", 1.5), ("N-05", "N-07", 2.0), ("N-06", "N-08", 2.0), ("N-07", "N-08", 1.0),
            ("N-06", "N-09", 3.0), ("N-08", "N-10", 2.5),
            ("N-09", "N-10", 1.5), ("N-09", "N-11", 2.0), ("N-10", "N-11", 1.0)
        };

        foreach ((string a, string b, double cost) in links) topology.AddLink(a, b, cost);

        SeedTelemetry(telemetry);
    }

    private static void SeedTelemetry(TelemetryStore telemetry)
    {
        DateTimeOffset end = DateTimeOffset.UtcNow;
        DateTimeOffset start = end.AddDays(-2);
        TimeSpan step = TimeSpan.FromMinutes(5);

        for (int i = 1; i <= 11; i++)
        {
            string sensorId = $"N-{i:00}";
            var random = new Random(1000 + i); 
            double phase = i * 0.7;

            for (DateTimeOffset t = start; t <= end; t += step)
            {
                double dayFraction = (t - start).TotalHours / 24.0 * 2 * Math.PI;
                double moisture = 38 + 14 * Math.Sin(dayFraction + phase) + random.NextDouble() * 3 - 1.5;
                double temperature = 22 + 6 * Math.Sin(dayFraction + phase - 1.2) + random.NextDouble() * 1.2 - 0.6;

                telemetry.Add(new TelemetryReading(sensorId, t, "moisture", Math.Round(moisture, 1), "%"));
                telemetry.Add(new TelemetryReading(sensorId, t, "temperature", Math.Round(temperature, 1), "°C"));
            }
        }
    }
}
