using System.Collections.Generic;
using UnityEngine;

// Eigen meshes: lathe, torus, cone, tube, blade.
public static class MeshKit
{
    // Omwentelingslichaam; profile = (straal, hoogte).
    public static Mesh Lathe(Vector2[] profile, int segments, Color[] colors = null, float[] bendX = null, string name = "Lathe")
    {
        var v = new List<Vector3>();
        var c = new List<Color>();
        var tri = new List<int>();
        for (int i = 0; i < profile.Length; i++)
        {
            float r = profile[i].x, y = profile[i].y, bx = bendX != null ? bendX[i] : 0f;
            Color col = colors != null ? colors[Mathf.Min(i, colors.Length - 1)] : Color.white;
            for (int j = 0; j <= segments; j++)
            {
                float a = j / (float)segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r + bx, y, Mathf.Sin(a) * r));
                c.Add(col);
            }
        }
        int ring = segments + 1;
        for (int i = 0; i < profile.Length - 1; i++)
            for (int j = 0; j < segments; j++)
            {
                int a = i * ring + j, b = a + 1, cc = a + ring, d = cc + 1;
                tri.Add(a); tri.Add(cc); tri.Add(b);
                tri.Add(b); tri.Add(cc); tri.Add(d);
            }
        return Build(name, v, c, tri);
    }

    public static Mesh Torus(float radius, float thickness, int segments = 24, int sides = 10, string name = "Torus")
    {
        var profile = new Vector2[sides + 1];
        for (int k = 0; k <= sides; k++)
        {
            // tegen de klok in rond de buis (buitenkant omhoog), zodat de normalen naar buiten wijzen
            float t = k / (float)sides * Mathf.PI * 2f;
            profile[k] = new Vector2(radius + Mathf.Cos(t) * thickness, Mathf.Sin(t) * thickness);
        }
        return Lathe(profile, segments, null, null, name);
    }

    public static Mesh Cone(float radius, float height, int segments = 12, string name = "Cone")
    {
        return Lathe(new[] { new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius * 0.98f, height * 0.02f), new Vector2(0f, height) }, segments, null, null, name);
    }

    // Buis langs punten.
    public static Mesh Tube(Vector3[] path, float[] radii, Color[] colors = null, int sides = 10, string name = "Tube")
    {
        var v = new List<Vector3>();
        var c = new List<Color>();
        var tri = new List<int>();
        Vector3 prevN = Vector3.zero;
        for (int i = 0; i < path.Length; i++)
        {
            Vector3 t = (i < path.Length - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
            // stabiel assenkruis langs het pad (normaal zo weinig mogelijk laten draaien)
            Vector3 n = i == 0 ? Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right).normalized
                               : Vector3.ProjectOnPlane(prevN, t).normalized;
            prevN = n;
            Vector3 b = Vector3.Cross(n, t);
            Color col = colors != null ? colors[Mathf.Min(i, colors.Length - 1)] : Color.white;
            for (int j = 0; j <= sides; j++)
            {
                float a = j / (float)sides * Mathf.PI * 2f;
                v.Add(path[i] + (n * Mathf.Cos(a) + b * Mathf.Sin(a)) * radii[i]);
                c.Add(col);
            }
        }
        int ring = sides + 1;
        for (int i = 0; i < path.Length - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * ring + j, bb = a + 1, cc = a + ring, d = cc + 1;
                tri.Add(a); tri.Add(cc); tri.Add(bb);
                tri.Add(bb); tri.Add(cc); tri.Add(d);
            }
        return Build(name, v, c, tri);
    }

    // Kromzwaardblad.
    public static Mesh Blade(float length, float width, float curve, float thickness, string name = "Blade")
    {
        int n = 14;
        var v = new List<Vector3>();
        var c = new List<Color>();
        var tri = new List<int>();
        for (int k = 0; k <= n; k++)
        {
            float f = k / (float)n;
            float x = curve * f * f;                                       // buigt naar opzij
            float w = width * (0.55f + 0.7f * f) * (f > 0.8f ? (1f - f) / 0.2f : 1f); // breder, dan punt
            float y = -length * f;
            Color col = Color.Lerp(new Color(0.85f, 0.88f, 0.95f), Color.white, f);
            // snede (scherpe kant) en rug
            v.Add(new Vector3(x + w * 0.75f, y, thickness)); c.Add(col);
            v.Add(new Vector3(x - w * 0.25f, y, thickness)); c.Add(col * 0.8f);
            v.Add(new Vector3(x + w * 0.75f, y, -thickness)); c.Add(col);
            v.Add(new Vector3(x - w * 0.25f, y, -thickness)); c.Add(col * 0.8f);
        }
        for (int k = 0; k < n; k++)
        {
            int a = k * 4, b = a + 4;
            // voorkant (+Z kant, met de klok mee gezien van voren)
            tri.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            // achterkant (omgekeerde draairichting)
            tri.AddRange(new[] { a + 2, b + 2, a + 3, a + 3, b + 2, b + 3 });
            // rug (dikke kant) dicht
            tri.AddRange(new[] { a + 1, a + 3, b + 1, a + 3, b + 3, b + 1 });
        }
        return Build(name, v, c, tri);
    }

    private static Mesh Build(string name, List<Vector3> v, List<Color> c, List<int> tri)
    {
        var m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetColors(c);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
