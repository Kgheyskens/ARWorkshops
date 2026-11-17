using UnityEngine;

// Hulpjes om modellen uit primitives op te bouwen.
public static class ModelBuilder
{
    public static PartColor Part(PrimitiveType type, Transform parent, string name,
                                 Vector3 localPos, Vector3 localScale, Vector3 localEuler, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.transform.localEulerAngles = localEuler;

        Collider c = go.GetComponent<Collider>();
        if (c != null) Kill(c);

        PartColor pc = go.AddComponent<PartColor>();
        pc.SetColor(color);
        return pc;
    }

    public static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 localEuler)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        t.localEulerAngles = localEuler;
        return t;
    }

    public static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Kill(t.GetChild(i).gameObject);
    }

    // Destroy in play mode, DestroyImmediate in de Editor.
    public static void Kill(Object o)
    {
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }
}
