using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-knop: menu "Genie in a Box → Maak modellen".
/// Bouwt de geest, de Wachter (boze djinn uit het geheime gevecht), het vliegende tapijt en de onderdelen van het
/// doolhof (muur, poort, vloertegels, gat, drukplaat, knikker) en bewaart ze als prefabs met echte materialen in Assets/Models.
/// Daarna worden ze meteen ingevuld op de Genie- en MazeGame-componenten in je scène.
/// Je kan de prefabs nadien openen en aanpassen (kleuren, vormen, ...): de game gebruikt ze.
/// </summary>
public static class GenieModelBaker
{
    private const string Folder = "Assets/Models";
    private const string MatFolder = "Assets/Models/Materials";
    private const string MeshFolder = "Assets/Models/Meshes";

    [MenuItem("Genie in a Box/Maak modellen (prefabs in Assets/Models)")]
    public static void BakeAll()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Models");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder(Folder, "Materials");

        var genieComp = Object.FindFirstObjectByType<Genie>();
        var mazeComp = Object.FindFirstObjectByType<MazeGame>();
        Color skin = genieComp != null ? genieComp.skin : new Color(0.3f, 0.75f, 1f);
        Color vest = genieComp != null ? genieComp.vest : new Color(0.55f, 0.2f, 0.75f);
        Color gold = genieComp != null ? genieComp.gold : new Color(1f, 0.8f, 0.3f);
        Color wall = mazeComp != null ? mazeComp.wallColor : new Color(0.86f, 0.68f, 0.42f);
        Color tileA = mazeComp != null ? mazeComp.tileA : new Color(0.16f, 0.12f, 0.3f);
        Color tileB = mazeComp != null ? mazeComp.tileB : new Color(0.2f, 0.15f, 0.36f);
        Color ball = mazeComp != null ? mazeComp.ballColor : new Color(0.35f, 0.95f, 1f);

        Gfx.BakeMode = true;
        Gfx.BakedMaterials.Clear();
        try
        {
            // Eerst alles bouwen (materialen worden verzameld), dan materialen bewaren, dan prefabs.
            var temp = new GameObject("GenieTemp");
            GameObject genie = Genie.BuildModel(temp.transform, skin, vest, gold).gameObject;
            genie.name = "Genie";
            GameObject wallGo = MazeGame.BuildWall(temp.transform, wall);
            GameObject gate = MazeGame.BuildGate(temp.transform);
            GameObject tA = MazeGame.BuildTile(temp.transform, tileA); tA.name = "MazeTileA";
            GameObject tB = MazeGame.BuildTile(temp.transform, tileB); tB.name = "MazeTileB";
            GameObject hole = MazeGame.BuildHole(temp.transform);
            GameObject sw = MazeGame.BuildSwitch(temp.transform);
            GameObject ballGo = MazeGame.BuildBall(temp.transform, ball);
            GameObject guardGo = GuardBattle.BuildGuardModel(temp.transform).gameObject;
            GameObject carpetGo = MagicCarpet.BuildModel(temp.transform).gameObject;
            // Het tapijt gebruikt een getekende textuur: die en het materiaal apart bewaren
            var clothR = carpetGo.transform.Find("Cloth").GetComponent<MeshRenderer>();
            var carpetMat = clothR.sharedMaterial;
            var carpetTex = carpetMat.mainTexture;
            string texPath = $"{MatFolder}/CarpetPattern.asset", carpetMatPath = $"{MatFolder}/Carpet.mat";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) != null) AssetDatabase.DeleteAsset(texPath);
            if (AssetDatabase.LoadAssetAtPath<Material>(carpetMatPath) != null) AssetDatabase.DeleteAsset(carpetMatPath);
            AssetDatabase.CreateAsset(carpetTex, texPath);
            AssetDatabase.CreateAsset(carpetMat, carpetMatPath);

            foreach (var kv in Gfx.BakedMaterials)
            {
                string path = $"{MatFolder}/{kv.Value.name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) { existing.CopyPropertiesFromMaterial(kv.Value); continue; }
                AssetDatabase.CreateAsset(kv.Value, path);
            }
            // Eigen meshes (MeshKit: hoorns, zwaard, staart, ...) moeten ook als asset bewaard worden,
            // anders zijn ze leeg in het prefab.
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(Folder, "Meshes");
            int meshNr = 0;
            foreach (var mf in temp.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh)) continue;
                string meshPath = $"{MeshFolder}/{mesh.name}_{meshNr++}.asset";
                var old = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (old != null) AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            // Renderers laten verwijzen naar de bewaarde materialen (bij al bestaande assets)
            foreach (var r in temp.GetComponentsInChildren<Renderer>(true))
            {
                string path = $"{MatFolder}/{r.sharedMaterial.name}.mat";
                var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (asset != null) r.sharedMaterial = asset;
            }

            var genieP = Save(genie, "Genie");
            var wallP = Save(wallGo, "MazeWall");
            var gateP = Save(gate, "MazeGate");
            var tAP = Save(tA, "MazeTileA");
            var tBP = Save(tB, "MazeTileB");
            var holeP = Save(hole, "MazeHole");
            var swP = Save(sw, "MazeSwitch");
            var ballP = Save(ballGo, "MazeBall");
            var guardP = Save(guardGo, "Guard");
            var carpetP = Save(carpetGo, "MagicCarpet");
            Object.DestroyImmediate(temp);

            // Meteen invullen in de scène
            if (genieComp != null) { Undo.RecordObject(genieComp, "Genie model"); genieComp.modelPrefab = genieP; EditorUtility.SetDirty(genieComp); }
            else Debug.LogWarning("[Genie in a Box] Geen Genie in de scène: sleep Models/Genie zelf in het veld 'Model Prefab'.");
            if (mazeComp != null)
            {
                Undo.RecordObject(mazeComp, "Maze modellen");
                mazeComp.wallPrefab = wallP; mazeComp.gatePrefab = gateP;
                mazeComp.tileAPrefab = tAP; mazeComp.tileBPrefab = tBP;
                mazeComp.holePrefab = holeP; mazeComp.switchPrefab = swP; mazeComp.ballPrefab = ballP;
                EditorUtility.SetDirty(mazeComp);
            }
            var battleComp = Object.FindFirstObjectByType<GuardBattle>();
            if (battleComp != null) { Undo.RecordObject(battleComp, "Guard model"); battleComp.guardPrefab = guardP; EditorUtility.SetDirty(battleComp); }
            var wishComp = Object.FindFirstObjectByType<WishEffects>();
            if (wishComp != null) { Undo.RecordObject(wishComp, "Carpet model"); wishComp.carpetPrefab = carpetP; EditorUtility.SetDirty(wishComp); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Genie in a Box] Modellen bewaard in Assets/Models" +
                      (genieComp == null || mazeComp == null ? " (Genie/Maze niet in de scène: sleep de prefabs zelf in de Inspector)" : " en ingevuld in de scène."));
        }
        finally
        {
            Gfx.BakeMode = false;
            Gfx.BakedMaterials.Clear();
        }
    }

    private static GameObject Save(GameObject go, string name)
    {
        go.transform.localPosition = Vector3.zero; // blijft onder 'temp', dat daarna verwijderd wordt
        string path = $"{Folder}/{name}.prefab";
        return PrefabUtility.SaveAsPrefabAsset(go, path);
    }
}
