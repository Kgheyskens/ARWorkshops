using UnityEngine;

// Kleur per onderdeel zonder aparte materialen (ook in de Editor).
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class PartColor : MonoBehaviour
{
    public Color color = Color.white;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in
    private MaterialPropertyBlock block;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    public void SetColor(Color c)
    {
        color = c;
        Apply();
    }

    public void Apply()
    {
        var r = GetComponent<Renderer>();
        if (r == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        r.SetPropertyBlock(block);
    }
}
