using System.Collections.Generic;
using UnityEngine;

// Wegennet uit OpenStreetMap als graaf, met Dijkstra voor echte wandelafstand.
public class RoadNetwork
{
    public readonly List<Vector2> nodes = new List<Vector2>();
    public readonly List<string> roadNames = new List<string>();
    private readonly List<List<int>> neighbours = new List<List<int>>();
    private readonly List<List<float>> weights = new List<List<float>>();

    public double originLat, originLon;
    public int Count => nodes.Count;

    public int AddNode(Vector2 position, string roadName)
    {
        nodes.Add(position);
        roadNames.Add(roadName);
        neighbours.Add(new List<int>());
        weights.Add(new List<float>());
        return nodes.Count - 1;
    }

    public void AddEdge(int a, int b)
    {
        if (a == b) return;
        float w = Vector2.Distance(nodes[a], nodes[b]);
        neighbours[a].Add(b); weights[a].Add(w);
        neighbours[b].Add(a); weights[b].Add(w);
    }

    public Vector2 ToLocal(double lat, double lon) => GeoUtils.ToLocalMeters(originLat, originLon, lat, lon);

    public void ToGeo(Vector2 local, out double lat, out double lon) =>
        GeoUtils.OffsetByMeters(originLat, originLon, local.y, local.x, out lat, out lon);

    // Dichtstbijzijnde (optioneel bereikbare) knoop.
    public int Nearest(Vector2 p, out float distance, float[] reachable = null)
    {
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (reachable != null && float.IsInfinity(reachable[i])) continue;
            float d = (nodes[i] - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        distance = best >= 0 ? Mathf.Sqrt(bestD) : float.PositiveInfinity;
        return best;
    }

    // Splitst het wegennet in losse eilanden; 'sizes' = knopen per eiland.
    public int[] ComponentIds(out List<int> sizes)
    {
        int n = nodes.Count;
        var comp = new int[n];
        for (int i = 0; i < n; i++) comp[i] = -1;
        sizes = new List<int>();
        var stack = new Stack<int>();

        for (int s = 0; s < n; s++)
        {
            if (comp[s] >= 0) continue;
            int id = sizes.Count, size = 0;
            comp[s] = id;
            stack.Push(s);
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                size++;
                foreach (int v in neighbours[u])
                    if (comp[v] < 0) { comp[v] = id; stack.Push(v); }
            }
            sizes.Add(size);
        }
        return comp;
    }

    // Kortste wandelafstand naar elke knoop (Infinity = onbereikbaar).
    public float[] Dijkstra(int source, int[] previous = null)
    {
        int n = nodes.Count;
        var dist = new float[n];
        for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;
        if (previous != null) for (int i = 0; i < n; i++) previous[i] = -1;
        if (source < 0 || source >= n) return dist;

        var heap = new MinHeap(n);
        dist[source] = 0f;
        heap.Push(source, 0f);

        while (heap.Count > 0)
        {
            heap.Pop(out int u, out float du);
            if (du > dist[u]) continue; // verouderde invoer
            var nb = neighbours[u];
            var w = weights[u];
            for (int k = 0; k < nb.Count; k++)
            {
                int v = nb[k];
                float alt = du + w[k];
                if (alt < dist[v])
                {
                    dist[v] = alt;
                    if (previous != null) previous[v] = u;
                    heap.Push(v, alt);
                }
            }
        }
        return dist;
    }

    // Pad van de bron naar 'target'.
    public static List<int> PathTo(int[] previous, int target)
    {
        var path = new List<int>();
        for (int v = target; v >= 0; v = previous[v]) path.Add(v);
        path.Reverse();
        return path;
    }

    // Binaire min-heap voor Dijkstra.
    private class MinHeap
    {
        private readonly List<int> items;
        private readonly List<float> keys;
        public int Count => items.Count;

        public MinHeap(int capacity)
        {
            items = new List<int>(capacity);
            keys = new List<float>(capacity);
        }

        public void Push(int item, float key)
        {
            items.Add(item);
            keys.Add(key);
            int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (keys[parent] <= keys[i]) break;
                Swap(i, parent);
                i = parent;
            }
        }

        public void Pop(out int item, out float key)
        {
            item = items[0];
            key = keys[0];
            int last = items.Count - 1;
            items[0] = items[last]; keys[0] = keys[last];
            items.RemoveAt(last); keys.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, smallest = i;
                if (l < items.Count && keys[l] < keys[smallest]) smallest = l;
                if (r < items.Count && keys[r] < keys[smallest]) smallest = r;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
            (keys[a], keys[b]) = (keys[b], keys[a]);
        }
    }
}