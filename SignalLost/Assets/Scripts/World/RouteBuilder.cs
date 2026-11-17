using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class RouteResult
{
    public List<Waypoint> waypoints = new List<Waypoint>();
    public List<DangerZone> zones = new List<DangerZone>();
    public float routeLength;     // meter, echte wandelafstand over de weg (zoals een routeplanner)
    public bool walkingDistance;  // true = routeLength is over het wegennet, false = vogelvlucht (testmodus)
    public int poiCount;
    public int roadNodeCount;
    public bool usedRealPlaces;
    public bool usedCache;        // kaartgegevens uit de cache (geen internet nodig)
    public RoadNetwork network;   // voor de live afstand tijdens het spelen
    public bool failed;
    public string error;
}

// Bouwt een route met masten die via OSM-wegen bereikbaar zijn.
public class RouteBuilder : MonoBehaviour
{
    [Header("OpenStreetMap")]
    public bool useRealPlaces = true;
    public string[] overpassMirrors =
    {
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    };
    [Tooltip("Mobiel internet (4G) is trager dan wifi: geef de server voldoende tijd")]
    public int downloadTimeoutSeconds = 60;
    public int attemptsPerServer = 2;

    [Header("Route (wandelafstand over de weg)")]
    [Tooltip("Tip voor de demovideo: zet dit lager (bv. 30/80) voor een korte route")]
    public float minLegDistance = 50f;
    public float maxLegDistance = 200f;
    public float reachRadius = 15f;
    [Tooltip("Zoekstraal = basis + per mast (meter)")]
    public float baseSearchRadius = 250f;
    public float searchRadiusPerMast = 120f;
    [Tooltip("Een bezienswaardigheid telt enkel als er binnen deze afstand een openbare weg ligt")]
    public float maxPoiToRoadDistance = 35f;
    [Tooltip("Lange wegstukken worden opgedeeld in stukjes van maximaal deze lengte (meter)")]
    public float nodeSpacing = 15f;

    [Header("Automatisch vergroten als er te weinig plekken zijn")]
    [Tooltip("Hoe vaak het zoekgebied maximaal vergroot wordt")]
    public int maxGrowSteps = 3;
    [Tooltip("Zoekstraal x deze factor bij elke vergroting")]
    public float growFactor = 1.8f;
    [Tooltip("Grootste zoekstraal ooit (meter) - groter = trager downloaden")]
    public float maxSearchRadius = 4000f;

    [Header("Storingsvelden (straal per moeilijkheid)")]
    public float easyZoneRadius = 12f;
    public float mediumZoneRadius = 15f;
    public float hardZoneRadius = 18f;

    public Action<string> statusCallback;
    public string LastStatus { get; private set; } = "";

    private class Poi { public string name; public string kind; public Vector2 pos; }

    [Serializable] private class OverpassResponse { public OverpassElement[] elements; }
    [Serializable] private class OverpassElement { public string type; public long id; public double lat; public double lon; public OverpassPoint center; public OverpassPoint[] geometry; public long[] nodes; public OverpassTags tags; }
    [Serializable] private class OverpassPoint { public double lat; public double lon; }
    [Serializable] private class OverpassTags { public string name; public string amenity; public string tourism; public string historic; public string leisure; public string highway; }

    private string CachePath => Path.Combine(Application.persistentDataPath, "osm_cache.json");

    // Hoe soepel de regels worden als er geen mast past.
    private struct PickLevel
    {
        public bool poisOnly; public float minLeg, maxLeg, tooClose;
        public PickLevel(bool p, float min, float max, float close) { poisOnly = p; minLeg = min; maxLeg = max; tooClose = close; }
    }

    public IEnumerator Build(double startLat, double startLon, int count, Difficulty difficulty, Action<RouteResult> done)
    {
        var result = new RouteResult();
        var rnd = new System.Random();

        if (!useRealPlaces)
        {
            var testNet = new RoadNetwork { originLat = startLat, originLon = startLon };
            BuildTestRoute(result, testNet, count, rnd);
            done?.Invoke(result);
            yield break;
        }

        float radius = Mathf.Min(baseSearchRadius + searchRadiusPerMast * count, maxSearchRadius);
        string problem = "";

        // Begin met de normale zoekstraal; lukt de route niet, dan wordt het gebied telkens groter.
        for (int step = 0; step <= maxGrowSteps; step++)
        {
            // ------------------------------------------------ 1. Kaartgegevens (cache of download)
            string json = LoadCache(startLat, startLon, radius, true);
            bool fromCache = json != null;
            if (!fromCache)
            {
                string downloaded = null, error = "";
                yield return Download(BuildQuery(startLat, startLon, radius, downloadTimeoutSeconds), radius,
                                      r => downloaded = r, e => error = e);
                json = downloaded;
                if (json != null) SaveCache(startLat, startLon, radius, json);
                else
                {
                    // Geen verbinding: dan maar een oudere kaart als die de buurt grotendeels dekt
                    json = LoadCache(startLat, startLon, radius, false);
                    fromCache = json != null;
                    if (json == null)
                    {
                        result.failed = true;
                        string net0 = Application.internetReachability == NetworkReachability.NotReachable ? "Geen internetverbinding." : "Kaartserver niet bereikbaar.";
                        result.error = $"{net0}\nDe app heeft internet nodig om veilige, bereikbare plekken te vinden.\n<size=70%>({error})</size>";
                        done?.Invoke(result);
                        yield break;
                    }
                }
            }
            result.usedCache = fromCache;

            // ------------------------------------------------ 2. Wegennet opbouwen
            statusCallback?.Invoke($"Wegennet en bereikbare plekken berekenen...\n<size=70%>zoekgebied {radius:F0} m</size>");
            yield return null;
            var net = new RoadNetwork { originLat = startLat, originLon = startLon };
            var pois = new List<Poi>();
            Parse(json, net, pois);
            yield return null;

            int startNode = PickStartNode(net);
            if (startNode < 0)
            {
                problem = "Geen openbare wegen of paden gevonden in je buurt.";
            }
            else
            {
                // ------------------------------------------------ 3. Route kiezen (3 pogingen met andere toeval)
                var legPaths = new List<List<int>>();
                bool ok = false;
                for (int attempt = 0; attempt < 3 && !ok; attempt++)
                {
                    ok = PlanRoute(net, startNode, pois, count, rnd, result, legPaths);
                    yield return null;
                }

                if (ok)
                {
                    result.walkingDistance = true;
                    result.network = net;
                    result.roadNodeCount = net.Count;
                    result.zones = BuildZones(net, legPaths, result.waypoints, difficulty, rnd);
                    SaveRecentMasts(result.waypoints);
                    LastStatus = $"{result.poiCount} plekken, {net.Count} wegknooppunten, {result.routeLength:F0} m wandelen, zoekgebied {radius:F0} m";
                    Debug.Log("[RouteBuilder] " + LastStatus);
                    done?.Invoke(result);
                    yield break;
                }
                problem = $"Te weinig bereikbare plekken voor {count} masten.";
            }

            // ------------------------------------------------ 4. Niet gelukt: zoekgebied vergroten
            if (radius >= maxSearchRadius) break;
            radius = Mathf.Min(radius * growFactor, maxSearchRadius);
            Debug.Log($"[RouteBuilder] {problem} Zoekgebied vergroten naar {radius:F0} m");
            statusCallback?.Invoke($"Nog niet genoeg plekken gevonden...\nZoekgebied vergroten naar {radius:F0} m");
            yield return new WaitForSeconds(1f); // servers even rust gunnen
        }

        result.failed = true;
        result.error = problem + "\nOok in een groter gebied lukte het niet. Probeer een andere plek of een korter verhaal.";
        done?.Invoke(result);
    }

    // Start op de dichtstbijzijnde weg van het grootste wegennet.
    private static int PickStartNode(RoadNetwork net)
    {
        if (net.Count == 0) return -1;
        int[] comp = net.ComponentIds(out List<int> sizes);
        int largest = 0;
        foreach (int s in sizes) largest = Mathf.Max(largest, s);
        int minSize = Mathf.Min(largest, Mathf.Max(40, largest / 5));

        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < net.Count; i++)
        {
            if (sizes[comp[i]] < minSize) continue;
            float d = net.nodes[i].sqrMagnitude; // speler staat op (0,0)
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    // Plaats 'count' masten, met stap voor stap soepelere regels.
    private bool PlanRoute(RoadNetwork net, int startNode, List<Poi> pois, int count, System.Random rnd,
                           RouteResult result, List<List<int>> legPaths)
    {
        result.waypoints.Clear();
        result.routeLength = 0f;
        result.usedRealPlaces = false;
        legPaths.Clear();

        var prev = new int[net.Count];
        float[] dist = net.Dijkstra(startNode, prev);

        // Bezienswaardigheden vastpinnen op het dichtstbijzijnde bereikbare knooppunt
        var poiAtNode = new Dictionary<int, Poi>();
        foreach (var p in pois)
        {
            int node = net.Nearest(p.pos, out float d, dist);
            if (node >= 0 && d <= maxPoiToRoadDistance && !poiAtNode.ContainsKey(node)) poiAtNode[node] = p;
        }
        result.poiCount = poiAtNode.Count;

        float min = minLegDistance, max = maxLegDistance;
        var levels = new[]
        {
            new PickLevel(true,  min,                    max,        min * 0.6f),
            new PickLevel(false, min,                    max,        min * 0.6f),
            new PickLevel(false, min * 0.5f,             max * 2f,   min * 0.4f),
            new PickLevel(false, Mathf.Min(20f, min),    max * 4f,   15f),
        };

        var placed = new List<Vector2> { net.nodes[startNode] };
        var recent = LoadRecentMasts(net);
        var used = new HashSet<int> { startNode };
        int current = startNode;
        float prevBearing = (float)(rnd.NextDouble() * 360.0);

        for (int i = 0; i < count; i++)
        {
            int pick = -1;
            for (int l = 0; l < levels.Length && pick < 0; l++)
            {
                var lv = levels[l];
                pick = PickNext(net, dist, current, placed, used, poiAtNode, recent, lv.poisOnly, prevBearing, lv.minLeg, lv.maxLeg, lv.tooClose, rnd);
                if (pick >= 0 && lv.poisOnly) result.usedRealPlaces = true;
            }
            if (pick < 0) return false;

            legPaths.Add(RoadNetwork.PathTo(prev, pick));
            result.routeLength += dist[pick];
            used.Add(pick);
            placed.Add(net.nodes[pick]);

            string name, kind;
            if (poiAtNode.TryGetValue(pick, out Poi poi)) { name = poi.name; kind = poi.kind; }
            else if (!string.IsNullOrEmpty(net.roadNames[pick])) { name = net.roadNames[pick]; kind = "straat"; }
            else { name = "Verdwaald signaal"; kind = "wandelpad"; }

            net.ToGeo(net.nodes[pick], out double lat, out double lon);
            result.waypoints.Add(new Waypoint { name = name, poiKind = kind, latitude = lat, longitude = lon, reachRadius = reachRadius, nodeIndex = pick });

            prevBearing = Bearing(net.nodes[current], net.nodes[pick]);
            current = pick;
            dist = net.Dijkstra(current, prev);
        }
        return true;
    }

    // ------------------------------------------------------------------ Overpass + cache

    private static string BuildQuery(double lat, double lon, float radius, int timeoutSeconds)
    {
        string around = string.Format(CultureInfo.InvariantCulture, "(around:{0:F0},{1:F6},{2:F6})", radius, lat, lon);
        int serverTimeout = Mathf.Clamp(timeoutSeconds, 25, 120);
        return
            "[out:json][timeout:" + serverTimeout + "];(" +
            "nwr" + around + "[name][tourism~\"^(attraction|artwork|viewpoint|museum|picnic_site|gallery)$\"];" +
            "nwr" + around + "[name][historic];" +
            "nwr" + around + "[name][amenity~\"^(place_of_worship|fountain|library|townhall|theatre|cinema|arts_centre|cafe|pub|bar|ice_cream|restaurant|marketplace|post_office|school|college|university)$\"];" +
            "nwr" + around + "[name][leisure~\"^(park|playground|garden|sports_centre|stadium)$\"];" +
            ");out center 300;" +
            // Alle wegen waar je te voet mag komen (ook grote wegen met voetpad, dienstwegen, veldwegen, trappen).
            // Enkel autosnelwegen en niet-bestaande wegen vallen weg. Zo valt het wegennet niet in losse stukjes.
            "(way" + around + "[highway][highway!~\"^(motorway|motorway_link|trunk|trunk_link|construction|proposed|abandoned|raceway|bus_guideway|platform|elevator|corridor)$\"]" +
            "[area!=yes][access!~\"^(private|no|customers|delivery|agricultural|forestry)$\"][foot!~\"^(private|no)$\"];);out geom;";
    }

    // Vraag alle servers tegelijk, eerste goede antwoord wint.
    private IEnumerator Download(string query, float radius, Action<string> done, Action<string> onError)
    {
        string lastError = "";
        for (int attempt = 1; attempt <= attemptsPerServer; attempt++)
        {
            statusCallback?.Invoke($"Kaart van je buurt ophalen...\n<size=70%>zoekgebied {radius:F0} m - poging {attempt}/{attemptsPerServer}</size>");

            string winner = null;
            int finished = 0;
            var running = new List<UnityWebRequest>();
            foreach (string server in overpassMirrors)
                StartCoroutine(Request(server, query, running, r =>
                {
                    finished++;
                    if (winner != null || r == null) return;
                    if (IsValidResponse(r, out string why)) winner = r;
                    else lastError = why;
                }, e => lastError = e));

            while (winner == null && finished < overpassMirrors.Length) yield return null;

            // De andere (tragere) servers afbreken
            foreach (var req in running.ToArray())
                if (req != null && !req.isDone) req.Abort();

            if (winner != null) { done(winner); yield break; }
            yield return new WaitForSeconds(2f);
        }
        onError?.Invoke(lastError);
        done(null);
    }

    // Halve kaart na time-out ('remark') negeren.
    private static bool IsValidResponse(string r, out string why)
    {
        why = "";
        if (string.IsNullOrEmpty(r) || !r.TrimStart().StartsWith("{")) { why = "ongeldig antwoord"; return false; }
        int remark = r.IndexOf("\"remark\"", StringComparison.Ordinal);
        if (remark >= 0 && r.IndexOf("runtime error", remark, StringComparison.Ordinal) >= 0) { why = "server time-out (gebied te groot of te druk)"; return false; }
        return true;
    }

    private IEnumerator Request(string url, string query, List<UnityWebRequest> running, Action<string> done, Action<string> onError)
    {
        var form = new WWWForm();
        form.AddField("data", query);
        using (UnityWebRequest req = UnityWebRequest.Post(url, form))
        {
            req.timeout = downloadTimeoutSeconds;
            running.Add(req);
            yield return req.SendWebRequest();
            running.Remove(req);

            if (req.result != UnityWebRequest.Result.Success)
            {
                // 429 = te veel aanvragen (vaak op 4G: veel gsm's delen een IP-adres), 504 = server te druk
                if (req.error != "Request aborted")
                {
                    LastStatus = $"HTTP {req.responseCode}: {req.error}";
                    Debug.LogWarning("[RouteBuilder] " + url + " -> " + LastStatus);
                    onError?.Invoke(LastStatus);
                }
                done(null);
                yield break;
            }
            done(req.downloadHandler.text);
        }
    }

    // Onthoudt de laatste 30 masten voor afwisseling.
    private static void SaveRecentMasts(List<Waypoint> wps)
    {
        var parts = new List<string>(PlayerPrefs.GetString("recentMasts", "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        foreach (var w in wps)
            parts.Add(w.latitude.ToString("F6", CultureInfo.InvariantCulture) + "," + w.longitude.ToString("F6", CultureInfo.InvariantCulture));
        while (parts.Count > 30) parts.RemoveAt(0);
        PlayerPrefs.SetString("recentMasts", string.Join(";", parts));
        PlayerPrefs.Save();
    }

    private static List<Vector2> LoadRecentMasts(RoadNetwork net)
    {
        var list = new List<Vector2>();
        foreach (string part in PlayerPrefs.GetString("recentMasts", "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] ll = part.Split(',');
            if (ll.Length == 2 &&
                double.TryParse(ll[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(ll[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                list.Add(net.ToLocal(lat, lon));
        }
        return list;
    }

    private void SaveCache(double lat, double lon, float radius, string json)
    {
        try
        {
            File.WriteAllText(CachePath, json);
            PlayerPrefs.SetString("osmCacheLat", lat.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString("osmCacheLon", lon.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetFloat("osmCacheRadius", radius);
            PlayerPrefs.Save();
        }
        catch (Exception e) { Debug.LogWarning("[RouteBuilder] Cache opslaan mislukt: " + e.Message); }
    }

    // Vorige kaart hergebruiken in hetzelfde gebied.
    private string LoadCache(double lat, double lon, float neededRadius, bool fully)
    {
        try
        {
            if (!File.Exists(CachePath) || !PlayerPrefs.HasKey("osmCacheLat")) return null;
            double cLat = double.Parse(PlayerPrefs.GetString("osmCacheLat"), CultureInfo.InvariantCulture);
            double cLon = double.Parse(PlayerPrefs.GetString("osmCacheLon"), CultureInfo.InvariantCulture);
            float cRadius = PlayerPrefs.GetFloat("osmCacheRadius");
            double offset = GeoUtils.Distance(lat, lon, cLat, cLon);
            // fully = de opgeslagen kaart dekt het hele zoekgebied -> geen download nodig (snel!)
            // anders = noodoplossing zonder internet: grotendeels dekken is goed genoeg
            if (offset + neededRadius * (fully ? 1f : 0.6f) > cRadius) return null;
            Debug.Log(fully ? "[RouteBuilder] Kaart uit cache (dekt het hele gebied)." : "[RouteBuilder] Geen verbinding: kaart uit cache gebruikt.");
            return File.ReadAllText(CachePath);
        }
        catch (Exception e) { Debug.LogWarning("[RouteBuilder] Cache lezen mislukt: " + e.Message); return null; }
    }

    // ------------------------------------------------------------------ Graaf opbouwen

    private void Parse(string json, RoadNetwork net, List<Poi> pois)
    {
        OverpassResponse response = null;
        try { response = JsonUtility.FromJson<OverpassResponse>(json); }
        catch (Exception e) { Debug.LogWarning("[RouteBuilder] JSON-fout: " + e.Message); }
        if (response == null || response.elements == null) return;

        var osmNodeToIndex = new Dictionary<long, int>();
        var seenNames = new HashSet<string>();

        foreach (var e in response.elements)
        {
            if (e.tags == null) continue;

            // ---- Weg of pad → knooppunten + verbindingen (kruispunten delen hetzelfde OSM-knooppunt)
            if (!string.IsNullOrEmpty(e.tags.highway) && e.geometry != null && e.geometry.Length >= 2)
            {
                string roadName = string.IsNullOrEmpty(e.tags.name) ? null : e.tags.name;
                bool hasIds = e.nodes != null && e.nodes.Length == e.geometry.Length;
                int previous = -1;

                for (int i = 0; i < e.geometry.Length; i++)
                {
                    Vector2 pos = net.ToLocal(e.geometry[i].lat, e.geometry[i].lon);
                    // Kruispunten herkennen: zelfde OSM-knooppunt-id (of anders: zelfde coördinaat)
                    long key = hasIds ? e.nodes[i]
                                      : -(long)Math.Round(e.geometry[i].lat * 1e6) * 1000000000L - (long)Math.Round(e.geometry[i].lon * 1e6);
                    int index;
                    if (osmNodeToIndex.TryGetValue(key, out int existing)) index = existing;
                    else
                    {
                        index = net.AddNode(pos, roadName);
                        osmNodeToIndex[key] = index;
                    }

                    if (previous >= 0)
                    {
                        // Lange stukken opdelen zodat er overal kandidaat-plekken liggen
                        Vector2 a = net.nodes[previous], b = net.nodes[index];
                        int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / nodeSpacing));
                        int last = previous;
                        for (int k = 1; k < pieces; k++)
                        {
                            int mid = net.AddNode(Vector2.Lerp(a, b, k / (float)pieces), roadName);
                            net.AddEdge(last, mid);
                            last = mid;
                        }
                        net.AddEdge(last, index);
                    }
                    previous = index;
                }
                continue;
            }

            // ---- Bezienswaardigheid
            if (string.IsNullOrEmpty(e.tags.name)) continue;
            double pLat = e.lat, pLon = e.lon;
            if (pLat == 0 && pLon == 0 && e.center != null) { pLat = e.center.lat; pLon = e.center.lon; }
            if (pLat == 0 && pLon == 0) continue;
            if (!seenNames.Add(e.tags.name)) continue;
            pois.Add(new Poi { name = e.tags.name, kind = KindLabel(e.tags), pos = net.ToLocal(pLat, pLon) });
        }
        Debug.Log($"[RouteBuilder] {pois.Count} bezienswaardigheden, {net.Count} wegknooppunten");
    }

    // ------------------------------------------------------------------ Mast kiezen

    private static float Bearing(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        return Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f);
    }

    // Volgende mast op minLeg-maxLeg meter wandelafstand, liefst in dezelfde richting.
    private static int PickNext(RoadNetwork net, float[] dist, int current, List<Vector2> placed, HashSet<int> used,
                                Dictionary<int, Poi> poiAtNode, List<Vector2> recent, bool poisOnly, float prevBearing,
                                float minLeg, float maxLeg, float tooCloseDistance, System.Random rnd)
    {
        var options = new List<(int node, float score)>();
        IEnumerable<int> source = poisOnly ? (IEnumerable<int>)poiAtNode.Keys : AllIndices(net.Count);

        foreach (int i in source)
        {
            float d = dist[i];
            if (float.IsInfinity(d) || d < minLeg || d > maxLeg || used.Contains(i)) continue;

            bool tooClose = false;
            foreach (var p in placed)
                if (Vector2.Distance(p, net.nodes[i]) < tooCloseDistance) { tooClose = true; break; }
            if (tooClose) continue;

            float turn = Mathf.Abs(Mathf.DeltaAngle(prevBearing, Bearing(net.nodes[current], net.nodes[i])));
            float score = turn * 0.5f + (float)rnd.NextDouble() * (poisOnly ? 40f : 90f);
            foreach (var r in recent)
                if (Vector2.Distance(r, net.nodes[i]) < 50f) { score += 300f; break; } // vorige keer al een mast hier
            options.Add((i, score));
        }
        if (options.Count == 0) return -1;

        options.Sort((a, b) => a.score.CompareTo(b.score));
        return options[rnd.Next(Mathf.Min(5, options.Count))].node;
    }

    private static IEnumerable<int> AllIndices(int n)
    {
        for (int i = 0; i < n; i++) yield return i;
    }

    // ------------------------------------------------------------------ Storingsvelden

    // Storingsvelden langs het gewandelde pad; hoe moeilijker, hoe meer op de weg.
    private List<DangerZone> BuildZones(RoadNetwork net, List<List<int>> legs, List<Waypoint> wps, Difficulty difficulty, System.Random rnd)
    {
        var zones = new List<DangerZone>();
        float radius = difficulty == Difficulty.Easy ? easyZoneRadius : difficulty == Difficulty.Medium ? mediumZoneRadius : hardZoneRadius;
        float sideOffset = difficulty == Difficulty.Easy ? radius + 6f : difficulty == Difficulty.Medium ? radius * 0.5f : 0f;
        int perLeg = difficulty == Difficulty.Hard ? 2 : 1;

        var zoneLocal = new List<Vector2>();
        var wpLocal = new List<Vector2>();
        foreach (var w in wps) wpLocal.Add(net.ToLocal(w.latitude, w.longitude));

        for (int i = 0; i < legs.Count; i++)
        {
            List<int> path = legs[i];
            if (path.Count < 3) continue;
            if (difficulty == Difficulty.Easy && i % 2 == 0) continue;

            // Cumulatieve lengte langs het pad
            var cumulative = new float[path.Count];
            for (int k = 1; k < path.Count; k++)
                cumulative[k] = cumulative[k - 1] + Vector2.Distance(net.nodes[path[k - 1]], net.nodes[path[k]]);
            float length = cumulative[path.Count - 1];
            if (length < 70f) continue;

            for (int z = 0; z < perLeg; z++)
            {
                float t = perLeg == 1 ? 0.4f + (float)rnd.NextDouble() * 0.2f : (z == 0 ? 0.3f : 0.65f) + (float)rnd.NextDouble() * 0.1f;
                float target = length * t;
                int k = 1;
                while (k < path.Count - 1 && cumulative[k] < target) k++;

                Vector2 here = net.nodes[path[k]];
                Vector2 dir = (net.nodes[path[k]] - net.nodes[path[k - 1]]).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x) * (rnd.NextDouble() < 0.5 ? -1f : 1f);
                Vector2 center = here + side * sideOffset;

                bool ok = Vector2.Distance(center, Vector2.zero) > radius + reachRadius + 10f;
                foreach (var w in wpLocal)
                    if (Vector2.Distance(center, w) < radius + reachRadius + 10f) ok = false;
                foreach (var other in zoneLocal)
                    if (Vector2.Distance(center, other) < radius * 2.5f) ok = false; // niet op elkaar
                if (!ok) continue;
                zoneLocal.Add(center);

                net.ToGeo(center, out double lat, out double lon);
                zones.Add(new DangerZone { name = "Storingsveld " + (zones.Count + 1), latitude = lat, longitude = lon, radius = radius });
            }
        }
        return zones;
    }

    // ------------------------------------------------------------------ Testmodus (Editor, zonder internet)

    private void BuildTestRoute(RouteResult result, RoadNetwork net, int count, System.Random rnd)
    {
        Vector2 current = Vector2.zero;
        float bearing = (float)(rnd.NextDouble() * 360.0);
        for (int i = 0; i < count; i++)
        {
            bearing += (float)(rnd.NextDouble() * 120.0 - 60.0);
            float d = minLegDistance + (float)rnd.NextDouble() * (Mathf.Min(maxLegDistance, 120f) - minLegDistance);
            Vector2 next = current + new Vector2(Mathf.Sin(bearing * Mathf.Deg2Rad), Mathf.Cos(bearing * Mathf.Deg2Rad)) * d;
            net.ToGeo(next, out double lat, out double lon);
            result.waypoints.Add(new Waypoint { name = "Testsignaal " + (i + 1), poiKind = "testplek", latitude = lat, longitude = lon, reachRadius = reachRadius });
            result.routeLength += d;
            current = next;
        }
    }

    private static string KindLabel(OverpassTags t)
    {
        switch (t.amenity)
        {
            case "place_of_worship": return "kerk";
            case "fountain": return "fontein";
            case "library": return "bibliotheek";
            case "townhall": return "stadhuis";
            case "theatre": case "arts_centre": return "cultuurhuis";
            case "cinema": return "cinema";
            case "cafe": case "pub": case "bar": return "cafe";
            case "ice_cream": return "ijssalon";
            case "restaurant": return "restaurant";
            case "marketplace": return "markt";
            case "post_office": return "postkantoor";
            case "school": case "college": case "university": return "school";
        }
        switch (t.tourism)
        {
            case "artwork": return "kunstwerk";
            case "viewpoint": return "uitkijkpunt";
            case "museum": case "gallery": return "museum";
            case "picnic_site": return "picknickplek";
            case "attraction": return "bezienswaardigheid";
        }
        if (!string.IsNullOrEmpty(t.historic)) return "historische plek";
        switch (t.leisure)
        {
            case "park": return "park";
            case "playground": return "speeltuin";
            case "garden": return "tuin";
            case "sports_centre": case "stadium": return "sportplek";
        }
        return "bezienswaardigheid";
    }
}