namespace SmartX.PoE.Api.Models;

public enum NodeStatus
{
    Online,
    Offline
}

public sealed class MeshNode
{
    public MeshNode(string id, string mac, string name, string subnet, bool isGateway = false)
    {
        Id = id;
        Mac = mac;
        Name = name;
        Subnet = subnet;
        IsGateway = isGateway;
    }

    public string Id { get; }
    public string Mac { get; }
    public string Name { get; }
    public string Subnet { get; }
    public bool IsGateway { get; }
    public NodeStatus Status { get; set; } = NodeStatus.Online;
    public bool IsOnline => Status == NodeStatus.Online;
}

public sealed record MeshEdge(string From, string To, double Cost);

public sealed record RoutePath(IReadOnlyList<string> Hops, double TotalCost);
