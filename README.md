# Smart-X IoT Gateway

A real-time ingestion, command and mesh-routing gateway for IoT sensor networks (ESP32 nodes, smart plugs and environmental sensors). It is built with an **ASP.NET Core 10 Minimal API** and a dependency-free **single-page dashboard**, and it uses classic data structures (queues, heaps, dictionaries, sets, trees and graphs) to keep high-throughput telemetry fast, observable and self-healing.

---

## Contents

1. [Features](#features)
2. [Project structure](#project-structure)
3. [Prerequisites](#prerequisites)
4. [Build, run and test](#build-run-and-test)
5. [Using the application](#using-the-application)
6. [API reference](#api-reference)
7. [Data structures and algorithms](#data-structures-and-algorithms)
   - [Part 2 structures at a glance](#part-2-structures-at-a-glance)
   - [AVL tree: historical telemetry](#1-avl-tree-historical-telemetry)
   - [Weighted graph: the mesh network](#2-weighted-graph-the-mesh-network)
   - [Breadth-first search: reachability and failures](#3-breadth-first-search-reachability-and-failures)
   - [Prim's algorithm and the binary heap: the minimum spanning tree](#4-prims-algorithm-and-the-binary-heap-the-minimum-spanning-tree)
   - [Kruskal's algorithm and union-find: the cross-check](#5-kruskals-algorithm-and-union-find-the-cross-check)
   - [Self-healing](#6-self-healing-putting-it-together)
8. [The demo network](#the-demo-network)
9. [Testing](#testing)
10. [Troubleshooting](#troubleshooting)
11. [References](#references)

---

## Features

The application has three modules, all reachable from the main menu without losing state.

| Module | What it does |
|---|---|
| **Sensor Data Ingestion & Telemetry** | Register devices (MAC address, deployment location, category) and monitor every device attached to the gateway. |
| **Real-Time Command Stream & History** | Live device dashboard, FIFO and priority queues for incoming packets, manual override commands with one-click undo, search and filters, and predictive "suggested actions". |
| **Network Topology & Mesh Routing** | Models the physical mesh as a weighted graph. Traces the route of any node by MAC address or name, shows the minimum spanning tree (the most power-efficient routing tree), detects failed nodes with BFS and reroutes around them automatically. Provides a time-range query over each sensor's history using an AVL tree. |

Devices are simulated, but every reading reaches the API as a real HTTP `POST /api/telemetry`, exactly as an ESP32 would send it.

---

## Project structure

```
SmartX.PoE/
├── SmartX.PoE.slnx
├── README.md
├── src/SmartX.PoE.Api/
│   ├── Program.cs                    # dependency injection, static files, endpoint mapping
│   ├── Endpoints.cs                  # every HTTP route
│   ├── Models/Models.cs              # Device, TelemetryPacket, CommandRecord, ReadingRange ...
│   ├── Structures/
│   │   ├── AvlTree.cs                # self-balancing binary search tree (history index)
│   │   └── MeshGraph.cs              # weighted graph + BFS + Prim + Kruskal + union-find
│   ├── Services/
│   │   ├── DeviceRegistry.cs         # Dictionary, SortedDictionary and AVL tree per device
│   │   ├── TelemetryProcessor.cs     # Queue<T> + PriorityQueue<T,P> ingestion pipeline
│   │   ├── TelemetryWorker.cs        # background consumer, offline sweep
│   │   ├── AlertTracker.cs           # HashSet of unique active alerts
│   │   ├── AlertRules.cs             # threshold classification
│   │   ├── CommandService.cs         # override commands + Stack<T> undo
│   │   ├── PredictionEngine.cs       # learns operator behaviour (transition table)
│   │   ├── InsightService.cs         # suggested actions and automated insights
│   │   ├── MeshService.cs            # failure simulation, self-healing, route tracing
│   │   ├── MeshSeed.cs               # the demo network (gateway + 12 devices, 22 links)
│   │   └── SeedData.cs               # devices, six hours of history, prediction training
│   └── wwwroot/index.html            # the dashboard (HTML, CSS and JS, no build step)
└── tests/SmartX.PoE.Tests/           # xUnit tests (unit + end-to-end)
```

---

## Prerequisites

- **.NET 10 SDK**: <https://dotnet.microsoft.com/download>. Verify with `dotnet --version` (it should print `10.x`).
- A modern browser (Chrome, Edge, Firefox or Safari).
- No Node.js, npm or database is required.

---

## Build, run and test

From the repository root (the folder containing `SmartX.PoE.slnx`):

```bash
# 1. restore packages and compile everything
dotnet build

# 2. run the gateway and dashboard
dotnet run --project src/SmartX.PoE.Api

# 3. run the automated tests (in a second terminal)
dotnet test
```

The console prints the address the app is listening on (usually `http://localhost:5000`). Open it in your browser.

To choose a different port:

```bash
dotnet run --project src/SmartX.PoE.Api --urls http://localhost:5055
```

**Visual Studio:** open `SmartX.PoE.slnx` (or `src/SmartX.PoE.Api/SmartX.PoE.Api.csproj` if your version does not open `.slnx`), set `SmartX.PoE.Api` as the startup project and press **F5**. Run the tests from **Test → Run All Tests**.

**Start with an empty gateway** (no seeded devices, mesh or history):

```bash
dotnet run --project src/SmartX.PoE.Api --Seed:Enabled=false
```

---

## Using the application

### Home
Three cards open the three modules. The header shows whether the gateway API is live.

### Sensor Data Ingestion & Telemetry
Register a sensor by entering a MAC address (or press **Generate**), a name, a deployment location and a category (Environmental, Power consumption or Actuator). Registered devices appear in the table with their live status. A newly registered device automatically joins the mesh and is linked to its two nearest nodes.

### Real-Time Command Stream & History
1. Press **Start simulator**. Every device now posts a reading each second.
2. Watch the cards update. Green is healthy, flashing red is alerting, grey is offline. The gauge shows where the reading sits between the limits.
3. Press **Moisture B drop**. A critical packet bypasses the FIFO queue through the priority queue, and a **Suggested action** such as *Turn Pump A ON* appears with a confidence score. Press **Apply**.
4. Press **Undo last** to pop the command off the undo stack and restore the previous state.
5. Press **Disconnect a node**. After about six seconds the node is flagged as disconnected once, and the *Duplicate alerts blocked* counter shows the HashSet working.
6. Press **Burst 600 packets** to see the FIFO queue fill and drain.
7. Use the search box (for example `pump a`), the category menu and the status chips to filter devices. Click a card to see its timeline.

### Network Topology & Mesh Routing
- **Mesh map:** the gateway, every node and every wireless link. Links in the minimum spanning tree are highlighted, and links touching a failed node are shown as dead.
- **Route tracing:** enter a MAC address (for example `AA:BB:CC:00:00:09`) or a name (for example `pump a`) to see the hop-by-hop path to the gateway, its hop count and its total power cost.
- **Failure simulation:** fail a node to simulate a power loss. BFS finds the nodes that can no longer reach the gateway, Prim's algorithm recomputes the spanning tree over the survivors, and every affected device is rerouted. Recover the node and the original routes return. A node that stops sending telemetry is treated as failed automatically.
- **Historical log:** choose a sensor and a time range (for example 14:00 to 16:00) to list its readings with minimum, maximum and average. The response also reports how many tree nodes the AVL range query visited, compared with the readings stored.

You can drive the same features with `curl`; see the next section.

---

## API reference

All routes are under `/api`. Bodies and responses are JSON.

### Ingestion and devices

| Method | Route | Purpose |
|---|---|---|
| POST | `/telemetry` | One reading: `{ "mac": "...", "value": 42.5 }` or `{ "mac": "...", "state": true }`. Returns `202` with `"queued": "standard"` or `"priority"`. |
| POST | `/telemetry/batch` | An array of the above. Returns how many were routed to each queue and how many were rejected. |
| GET | `/devices` | List devices. Filters: `category`, `status`, `q` (free text). |
| POST | `/devices` | Register a device. |
| GET | `/devices/{mac}/history?take=120` | Latest readings (SortedDictionary), ordered by timestamp. |
| GET | `/devices/{mac}/readings?from=&to=&take=` | **AVL range query.** ISO-8601 times (`from` defaults to one hour before `to`, `to` defaults to now). |

### Commands and analytics

| Method | Route | Purpose |
|---|---|---|
| POST | `/commands` | `{ "macs": [...], "action": "on" \| "off" \| "toggle" }`. |
| POST | `/commands/undo` | Reverts the most recent command. |
| GET | `/commands` | Undo depth and recent commands. |
| GET | `/stats` | Queue depths, processed counters, alert counts. |
| GET | `/alerts` | Currently active alerts. |
| GET | `/insights` | Suggested actions and automated insights. |
| POST | `/search-log` | Records a search so the predictor can learn from it. |

### Network topology

| Method | Route | Purpose |
|---|---|---|
| GET | `/mesh` | Full snapshot: nodes, links, MST costs, down and isolated nodes, BFS order and reroutes. |
| GET | `/mesh/route?q=` | Route of a node found by MAC address or name. |
| POST | `/mesh/nodes/{mac}/fail` | Simulate a failure (the gateway cannot be failed). |
| POST | `/mesh/nodes/{mac}/recover` | Clear a simulated failure. |
| POST | `/mesh/recover-all` | Clear every simulated failure. |

### Examples

```bash
# send a reading like a device would
curl -X POST http://localhost:5000/api/telemetry \
     -H "Content-Type: application/json" \
     -d '{"mac":"AA:BB:CC:00:00:02","value":12}'

# range query: readings from the last two hours (adjust the times; seeded history covers the last six hours)
curl "http://localhost:5000/api/devices/AA:BB:CC:00:00:01/readings?from=2026-11-14T08:00:00Z&to=2026-11-14T10:00:00Z"

# trace a route by name, then fail the node it passes through
curl "http://localhost:5000/api/mesh/route?q=pump%20a"
curl -X POST http://localhost:5000/api/mesh/nodes/AA:BB:CC:00:00:06/fail
curl http://localhost:5000/api/mesh
curl -X POST http://localhost:5000/api/mesh/recover-all
```

The readings response contains `count`, `visitedNodes`, `treeSize`, `treeHeight`, `min`, `max`, `avg` and the readings themselves (newest first).

---

## Data structures and algorithms

### Part 2 structures at a glance

| Structure | Where | Why |
|---|---|---|
| `Queue<T>` | `TelemetryProcessor` | Normal packets are processed first-in, first-out. |
| `PriorityQueue<T,P>` | `TelemetryProcessor` | Critical packets bypass the queue, most severe deviation first. |
| `Stack<T>` | `CommandService` | Last-in, first-out undo of override commands. |
| `Dictionary<K,V>` | `DeviceRegistry` | O(1) device lookup by MAC address on every packet. |
| `SortedDictionary<K,V>` | `DeviceRegistry` | The latest 300 readings per device, already in time order for the live chart. |
| `HashSet<T>` | `AlertTracker` | Unique active alerts, so a duplicate disconnect alert is ignored. |
| Transition table | `PredictionEngine` | Learns which command usually follows which alert. |

### 1. AVL tree: historical telemetry

**Role in the topology feature.** Each device keeps up to 5,000 timestamped readings in an AVL tree (`Structures/AvlTree.cs`), keyed by timestamp. The historical log asks "all readings between 14:00 and 16:00" without scanning every stored reading.

**Why an AVL tree and not a plain binary search tree.** Timestamps arrive in ascending order. Inserted into a plain BST, they would form a linked list and every query would degrade to O(n). An AVL tree (Adelson-Velsky and Landis, 1962) rotates after every insert so that the heights of any node's two subtrees differ by at most one. The height therefore stays below roughly 1.44 · log₂(n + 2) (Cormen et al., 2022).

| Operation | Complexity |
|---|---|
| Insert, remove, find | O(log n) |
| Range query | O(log n + k), where k is the number of matches |
| Latest k readings | O(log n + k) |

**Balancing: rotations after each insert.**

```csharp
private static Node Rebalance(Node n)
{
    Update(n);
    var balance = Balance(n);

    if (balance > 1)                                                  // left-heavy
    {
        if (Balance(n.Left!) < 0) n.Left = RotateLeft(n.Left!);       // left-right case
        return RotateRight(n);
    }
    if (balance < -1)                                                 // right-heavy
    {
        if (Balance(n.Right!) > 0) n.Right = RotateRight(n.Right!);   // right-left case
        return RotateLeft(n);
    }
    return n;
}
```

**Range query: prune whole subtrees.** The search only descends into a subtree when it can still contain a key in the range.

```csharp
private static void Collect(Node? n, TKey from, TKey to, List<KeyValuePair<TKey, TValue>> acc, ref int visited)
{
    if (n is null) return;
    visited++;

    var aboveFrom = from.CompareTo(n.Key) <= 0;
    var belowTo = n.Key.CompareTo(to) <= 0;

    if (from.CompareTo(n.Key) < 0) Collect(n.Left, from, to, acc, ref visited);   // left subtree can still hold matches
    if (aboveFrom && belowTo) acc.Add(new KeyValuePair<TKey, TValue>(n.Key, n.Value));
    if (n.Key.CompareTo(to) < 0) Collect(n.Right, from, to, acc, ref visited);    // right subtree can still hold matches
}
```

The `visited` counter is returned by the API as `visitedNodes`. Comparing it with `treeSize` shows how much of the tree the query skipped.

**Usage in the registry.**

```csharp
public ReadingRange? QueryRange(string mac, DateTime fromUtc, DateTime toUtc)
{
    lock (_gate)
    {
        if (!_tree.TryGetValue(mac, out var tree)) return null;
        var items = tree.Range(fromUtc, toUtc, out var visited);
        return new ReadingRange(items, visited, tree.Count, tree.Height);
    }
}
```

When a tree grows past 5,000 readings, the oldest reading (`TryGetMin`, then `Remove`) is dropped, so memory stays bounded.

### 2. Weighted graph: the mesh network

**Role.** `MeshGraph` models the physical network. Vertices are the gateway and the ESP32 nodes. Undirected edges are wireless links. The edge weight is the **power cost** of one hop (a weaker signal means a higher cost).

It is stored as an **adjacency list** (`Dictionary<string, List<Link>>`). That uses O(V + E) memory and lets every algorithm iterate over a node's neighbours directly, which suits a sparse mesh far better than a V×V matrix. Insertion order is kept, so results are deterministic.

```csharp
public void AddEdge(string a, string b, double cost)
{
    if (!_adj.ContainsKey(a)) throw new ArgumentException($"Unknown node '{a}'.", nameof(a));
    if (!_adj.ContainsKey(b)) throw new ArgumentException($"Unknown node '{b}'.", nameof(b));
    if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A node cannot link to itself.");
    if (!double.IsFinite(cost) || cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost), "Link cost must be a positive number.");

    Set(a, b, cost);
    Set(b, a, cost);
}
```

Every algorithm takes a predicate (`Func<string,bool> isUp`). A failed node is skipped by the predicate, so the graph never has to be rebuilt when nodes fail or recover.

### 3. Breadth-first search: reachability and failures

**Role.** BFS answers two questions: *which devices can still reach the gateway?* and *how many hops away is each one?* Any node the search never reaches is either **down** or **isolated** (powered, but every path to it passes through failed nodes).

BFS visits each vertex and edge at most once, so it runs in O(V + E).

```csharp
public BfsResult Bfs(string start, Func<string, bool>? isUp = null)
{
    isUp ??= _ => true;
    var order = new List<string>();
    var hops = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (!_adj.ContainsKey(start) || !isUp(start)) return new BfsResult(order, hops, parent);

    var queue = new Queue<string>();
    hops[start] = 0;
    order.Add(start);
    queue.Enqueue(start);

    while (queue.Count > 0)
    {
        var u = queue.Dequeue();
        foreach (var link in _adj[u])
        {
            if (hops.ContainsKey(link.To) || !isUp(link.To)) continue;
            hops[link.To] = hops[u] + 1;
            parent[link.To] = u;
            order.Add(link.To);
            queue.Enqueue(link.To);
        }
    }
    return new BfsResult(order, hops, parent);
}
```

`MeshService` runs BFS from the gateway with `IsUp` as the predicate. A node is *up* unless the operator failed it or the gateway has flagged it as disconnected (no telemetry within the timeout). For a failed node, `/api/mesh/route` also lists its working neighbours, which are the places traffic can **hop to** instead.

### 4. Prim's algorithm and the binary heap: the minimum spanning tree

**Role.** The minimum spanning tree (MST) connects every node to the gateway using the cheapest possible set of links. In a mesh where link cost is transmit power, it is the **most power-efficient routing tree** (Prim, 1957).

Prim's algorithm grows the tree from the gateway. A **min-heap** (`PriorityQueue`, a binary heap) holds the candidate links leaving the tree, so the cheapest one is always found in O(log n) (Microsoft, 2025). The total complexity is O(E log V). Ties are broken by vertex order, so the same network always produces the same tree.

```csharp
public MstResult Prim(string root, Func<string, bool>? isUp = null)
{
    isUp ??= _ => true;
    var edges = new List<MstEdge>();
    var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    double total = 0;
    if (!_adj.ContainsKey(root) || !isUp(root)) return new MstResult(root, edges, 0, parent);

    var inTree = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
    var heap = new PriorityQueue<(string From, string To), (double Cost, int ToIdx, int FromIdx)>();

    void PushLinksOf(string u)
    {
        foreach (var l in _adj[u])
            if (!inTree.Contains(l.To) && isUp(l.To))
                heap.Enqueue((u, l.To), (l.Cost, _index[l.To], _index[u]));
    }

    PushLinksOf(root);
    while (heap.TryDequeue(out var link, out var priority))
    {
        if (!inTree.Add(link.To)) continue;                      // a cheaper link already reached this vertex
        parent[link.To] = link.From;
        edges.Add(new MstEdge(link.From, link.To, priority.Cost));
        total += priority.Cost;
        PushLinksOf(link.To);
    }
    return new MstResult(root, edges, total, parent);
}
```

Every node stores its `parent` (the next hop towards the gateway), so a route is found by following parents upwards:

```csharp
public IReadOnlyList<string>? PathToRoot(string node)
{
    var path = new List<string> { node };
    var current = node;
    while (Parent.TryGetValue(current, out var next))
    {
        path.Add(next);
        current = next;
        if (path.Count > Parent.Count + 1) return null;      // defensive: a parent loop would otherwise never end
    }
    return string.Equals(current, Root, StringComparison.OrdinalIgnoreCase) ? path : null;
}
```

### 5. Kruskal's algorithm and union-find: the cross-check

Kruskal's algorithm (Kruskal, 1956) builds the same minimum cost another way: sort all links by cost and keep a link only if it joins two **different** components, which is tracked with a union-find structure (path halving and union by rank). The complexity is O(E log E).

```csharp
foreach (var e in candidates)
{
    if (!sets.Union(_index[e.A], _index[e.B])) continue;     // same component: would create a cycle
    edges.Add(e);
    total += e.Cost;
}
```

The snapshot returned by `/api/mesh` reports both `mstCost` (Prim) and `kruskalCost`. They must always be equal, because an MST's total weight is unique even when the tree itself is not. The test suite checks this for every possible single-node failure.

### 6. Self-healing: putting it together

Every call to `MeshService.Topology()` repeats this pipeline over the surviving nodes:

1. `Bfs(gateway, IsUp)` finds who can still reach the gateway.
2.
