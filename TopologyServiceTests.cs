using SmartX.PoE.Api.Models;
using SmartX.PoE.Api.Services;
using Xunit;

namespace SmartX.PoE.Tests;

public class TopologyServiceTests
{
    private static TopologyService SeededService()
    {
        var topology = new TopologyService();
        DemoDataSeeder.Seed(topology, new TelemetryStore());
        return topology;
    }

    [Fact]
    public void DemoMesh_HasTheExpectedMst()
    {
        TopologyDto topo = SeededService().GetTopology();

        Assert.Equal(12, topo.Summary.Total);
        Assert.Equal(11, topo.Summary.MstEdges);          
        Assert.Equal(18.5, topo.Summary.MstCost, 6);
        Assert.Equal(11, topo.Links.Count(l => l.InMst));
        Assert.Equal(0, topo.Summary.Isolated);
    }

    [Fact]
    public void Route_CanBeLookedUpByIdOrMac_WithTheSameResult()
    {
        TopologyService topology = SeededService();

        RouteDto byId = topology.GetRoute("N-04");
        RouteDto byMac = topology.GetRoute("24-6f-28-a1-00-04");

        Assert.Equal(byId.Id, byMac.Id);
        Assert.Equal(new[] { "N-04", "N-03", "N-01", "GW-01" }, byId.MstRoute!.Hops);
        Assert.Equal(5.0, byId.MstRoute.TotalCost, 6);
        Assert.Equal(byId.MstRoute.Hops, byMac.MstRoute!.Hops);
    }

    [Fact]
    public void FailingANode_ReroutesItsDependants()
    {
        TopologyService topology = SeededService();
        topology.SetStatus("N-05", NodeStatus.Offline);

        FailureReportDto report = topology.GetFailures();

        FailureDto failure = Assert.Single(report.Failures);
        Assert.Equal("N-05", failure.Id);
        Assert.Equal(new[] { "GW-01", "N-06", "N-07" }, failure.RerouteVia);
        Assert.Equal(6, failure.Affected.Count);
        Assert.All(failure.Affected, a => Assert.False(a.Isolated));

        AffectedNodeDto n07 = failure.Affected.Single(a => a.Id == "N-07");
        Assert.Equal("N-03", n07.NextHop);
        Assert.Equal(new[] { "N-07", "N-03", "N-01", "GW-01" }, n07.NewRoute!);
        Assert.Empty(report.Isolated);
    }

    [Fact]
    public void RestoringANode_ClearsTheFailureReport()
    {
        TopologyService topology = SeededService();
        topology.SetStatus("N-05", NodeStatus.Offline);
        topology.SetStatus("N-05", NodeStatus.Online);

        Assert.Empty(topology.GetFailures().Failures);
        Assert.Equal(18.5, topology.GetTopology().Summary.MstCost, 6);
    }

    [Fact]
    public void TheGateway_CannotBeTakenOffline()
    {
        Assert.Throws<InvalidOperationException>(() => SeededService().SetStatus("GW-01", NodeStatus.Offline));
    }

    [Fact]
    public void Reachability_ForASubnet_ListsConnectedAndDownMembers()
    {
        TopologyService topology = SeededService();
        topology.SetStatus("N-05", NodeStatus.Offline);

        ReachabilityDto result = topology.GetReachability(null, "zone-b");

        Assert.Equal(new[] { "N-06", "N-07", "N-08" }, result.Reachable.Select(n => n.Id).OrderBy(x => x));
        Assert.Equal("N-05", Assert.Single(result.Unreachable).Id);
    }

    [Fact]
    public void ANodeWithNoRemainingPath_IsReportedAsIsolated()
    {
        var topology = new TopologyService();
        topology.AddNode(new MeshNode("GW", "AA:00:00:00:00:01", "Gateway", "core", isGateway: true));
        topology.AddNode(new MeshNode("A", "AA:00:00:00:00:02", "A", "z"));
        topology.AddNode(new MeshNode("B", "AA:00:00:00:00:03", "B", "z"));
        topology.AddLink("GW", "A", 1);
        topology.AddLink("A", "B", 1);

        topology.SetStatus("A", NodeStatus.Offline);

        FailureReportDto report = topology.GetFailures();
        Assert.Equal("B", Assert.Single(report.Isolated).Id);
        Assert.True(Assert.Single(Assert.Single(report.Failures).Affected).Isolated);

        RouteDto route = topology.GetRoute("B");
        Assert.False(route.Reachable);
        Assert.Null(route.MstRoute);
    }

    [Fact]
    public void Route_ExplainsWhenTheMstIsNotTheCheapestSingleRoute()
    {
        var topology = new TopologyService();
        topology.AddNode(new MeshNode("G", "AA:00:00:00:00:01", "Gateway", "core", isGateway: true));
        topology.AddNode(new MeshNode("A", "AA:00:00:00:00:02", "A", "z"));
        topology.AddNode(new MeshNode("B", "AA:00:00:00:00:03", "B", "z"));
        topology.AddLink("G", "A", 5);
        topology.AddLink("G", "B", 3);
        topology.AddLink("B", "A", 3);

        RouteDto route = topology.GetRoute("A");

        Assert.Equal(6.0, route.MstRoute!.TotalCost, 6);
        Assert.Equal(5.0, route.CheapestRoute!.TotalCost, 6);
        Assert.Contains("MST minimises", route.Message);
    }

    [Fact]
    public void TopologyCache_IsRefreshedAfterAChange()
    {
        TopologyService topology = SeededService();
        double before = topology.GetTopology().Summary.MstCost;

        topology.SetStatus("N-05", NodeStatus.Offline);
        double after = topology.GetTopology().Summary.MstCost;

        Assert.Equal(18.5, before, 6);
        Assert.Equal(18.0, after, 6);
    }
}
