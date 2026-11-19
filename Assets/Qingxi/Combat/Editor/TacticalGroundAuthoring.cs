using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class TacticalGroundAuthoring
{
    const string TownScene = "Assets/Qingxi/Scenes/QingxiTrail.unity";
    const string Regions = "Assets/Travel/Resources/Regions/";
    const string GroundAssets = "Assets/Qingxi/Combat/Ground/";
    const string GroundName = "战棋地面";

    [MenuItem("行旅/重建三个地点的战棋地面")]
    public static void Rebuild()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Directory.CreateDirectory(GroundAssets);
            Tile tile = GroundTile();
            BuildTown(tile);
            BuildRegion("QixiaGate", "山门石庭", tile);
            BuildRegion("SongfengPass", "松风溪岸", tile);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    [MenuItem("行旅/检查三个地点的战棋地面")]
    public static void Validate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(TownScene);
            var walker = UnityEngine.Object.FindAnyObjectByType<QingxiWalker>();
            CheckMap(QingxiTacticalGround.Find(walker.transform), "Qingxi");
            CheckRegion("QixiaGate");
            CheckRegion("SongfengPass");
        }
        finally
        {
            if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    static void CheckRegion(string name)
    {
        var root = PrefabUtility.LoadPrefabContents(Regions + name + ".prefab");
        try { CheckMap(QingxiTacticalGround.Find(root.transform.Find("行者")), name); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void CheckMap(QingxiTacticalGround ground, string name)
    {
        if (!ground || !ground.Tilemap || !ground.Tilemap.TryGetComponent<TilemapRenderer>(out var renderer) ||
            !renderer.sharedMaterial || !renderer.sharedMaterial.GetTexture("_GroundArt") ||
            ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))
            throw new InvalidOperationException(name + " 缺少可绘制的战棋地面。");

        int total = 0;
        foreach (var cell in ground.Tilemap.cellBounds.allPositionsWithin)
        {
            var index = new Vector2Int(cell.x, cell.y);
            if (!ground.HasGround(index)) continue;
            Vector2 position = ground.Tilemap.GetCellCenterWorld(cell);
            if (!ground.WorldBounds.Contains(position) || (position - QingxiGridMetric.Offset(index)).sqrMagnitude > .00001f)
                throw new InvalidOperationException($"{name} 地格 {cell} 与战棋地图范围或坐标不符。");
            total++;
        }

        var limits = ground.WorldBounds;
        if (total < 500 || ground.HasGround(QingxiGridMetric.Index(limits.max + Vector2.one)))
            throw new InvalidOperationException(name + " 的战棋地面范围有误。");
        Debug.Log($"{name} 战棋地面检查通过，共 {total} 格。");
    }

    static void BuildTown(Tile tile)
    {
        var town = EditorSceneManager.OpenScene(TownScene);
        var valley = Points(GameObject.Find("山谷边界").GetComponent<EdgeCollider2D>());
        var bank = Points(GameObject.Find("溪岸碰撞").GetComponent<EdgeCollider2D>());
        var art = GameObject.Find("谷地底色").GetComponent<SpriteRenderer>();
        var bounds = Enclose(valley);
        var grid = GameObject.Find(GroundName);
        if (!grid) grid = new GameObject(GroundName);
        Bake(grid, art, "Qingxi", bounds, p => Inside(valley, p) && NorthOf(bank, p), tile);
        if (!EditorSceneManager.SaveScene(town)) throw new InvalidOperationException("青溪战棋地面保存失败。");
    }

    static void BuildRegion(string resourceName, string groundArt, Tile tile)
    {
        string path = Regions + resourceName + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var art = root.transform.Find(groundArt).GetComponent<SpriteRenderer>();
            var left = WallFace(root, "西界", Vector2.right);
            var right = WallFace(root, "东界", Vector2.left);
            var lower = WallFace(root, "南界", Vector2.up);
            var upper = WallFace(root, "北界", Vector2.down);
            Rect bounds = Rect.MinMaxRect(left.x, lower.y, right.x, upper.y);
            var existing = root.transform.Find(GroundName);
            var grid = existing ? existing.gameObject : new GameObject(GroundName);
            grid.transform.SetParent(root.transform, false);
            Bake(grid, art, resourceName, bounds, bounds.Contains, tile);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Vector2 WallFace(GameObject root, string name, Vector2 side)
    {
        var wall = root.transform.Find(name).GetComponent<BoxCollider2D>();
        return wall.transform.TransformPoint(wall.offset + Vector2.Scale(wall.size * .5f, side));
    }

    static void Bake(GameObject gridRoot, SpriteRenderer art, string assetName, Rect bounds,
        Func<Vector2, bool> hasGround, Tile tile)
    {
        var grid = GetOrAdd<Grid>(gridRoot);
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = QingxiGridMetric.TilemapCellSize;
        var child = grid.transform.Find("地板");
        var ground = child ? child.gameObject : new GameObject("地板");
        ground.transform.SetParent(grid.transform, false);
        var map = GetOrAdd<Tilemap>(ground);
        map.tileAnchor = Vector3.zero;
        map.ClearAllTiles();

        var corners = new[] {
            QingxiGridMetric.Index(bounds.min), QingxiGridMetric.Index(bounds.max),
            QingxiGridMetric.Index(new Vector2(bounds.xMin, bounds.yMax)),
            QingxiGridMetric.Index(new Vector2(bounds.xMax, bounds.yMin))
        };
        var low = corners[0];
        var high = corners[0];
        foreach (Vector2Int corner in corners)
        {
            low = Vector2Int.Min(low, corner);
            high = Vector2Int.Max(high, corner);
        }
        var positions = new List<Vector3Int>();
        var entries = new List<TileBase>();
        for (int x = low.x; x <= high.x; x++)
        for (int y = low.y; y <= high.y; y++)
        {
            if (!hasGround(QingxiGridMetric.Offset(new Vector2Int(x, y)))) continue;
            positions.Add(new Vector3Int(x, y, 0));
            entries.Add(tile);
        }
        if (positions.Count == 0) throw new InvalidOperationException(assetName + " 缺少地面瓦片。");
        map.SetTiles(positions.ToArray(), entries.ToArray());
        map.CompressBounds();

        foreach (var cell in new[] { Vector3Int.zero, new Vector3Int(10, -4, 0) })
        {
            var geometric = QingxiGridMetric.Offset(new Vector2Int(cell.x, cell.y));
            if (((Vector2)map.GetCellCenterWorld(cell) - geometric).sqrMagnitude > .00001f)
                throw new InvalidOperationException("Tilemap 的地板中心与战棋地格对不上。");
        }

        var renderer = GetOrAdd<TilemapRenderer>(ground);
        renderer.sortingLayerID = art.sortingLayerID;
        renderer.sortingOrder = art.sortingOrder + 1;
        renderer.sharedMaterial = MaterialFor(art, assetName);

        var serialized = new SerializedObject(GetOrAdd<QingxiTacticalGround>(gridRoot));
        serialized.FindProperty("tiles").objectReferenceValue = map;
        serialized.FindProperty("worldBounds").rectValue = bounds;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"{assetName} 战棋地面已生成，共 {positions.Count} 格。");
    }

    static Material MaterialFor(SpriteRenderer art, string name)
    {
        string path = GroundAssets + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Qinglan/Tactical Ground");
        if (!shader) throw new InvalidOperationException("找不到战棋地面着色器。");
        if (!material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        Bounds artBounds = art.bounds;
        material.SetTexture("_GroundArt", art.sprite.texture);
        material.SetVector("_ArtBounds", new Vector4(artBounds.min.x, artBounds.min.y, artBounds.size.x, artBounds.size.y));
        material.SetColor("_GroundTint", art.color);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Tile GroundTile()
    {
        const string spritePath = GroundAssets + "FloorMask.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (!sprite)
        {
            const int width = 164, height = 90;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float distance = Mathf.Abs(2f * (x + .5f) / width - 1f) + Mathf.Abs(2f * (y + .5f) / height - 1f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * Mathf.Clamp01((1.012f - distance) * 80f)));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(spritePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        }

        const string assetPath = GroundAssets + "FloorTile.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        if (!tile)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, assetPath);
        }
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static Vector2[] Points(EdgeCollider2D edge)
    {
        var points = edge.points;
        for (int i = 0; i < points.Length; i++)
            points[i] = edge.transform.TransformPoint(points[i] + edge.offset);
        return points;
    }

    static Rect Enclose(Vector2[] points)
    {
        var low = points[0];
        var high = points[0];
        foreach (Vector2 p in points)
        {
            low = Vector2.Min(low, p);
            high = Vector2.Max(high, p);
        }
        return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
    }

    static bool Inside(Vector2[] polygon, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    static bool NorthOf(Vector2[] bank, Vector2 p)
    {
        for (int i = 1; i < bank.Length; i++)
            if (p.x >= bank[i - 1].x && p.x <= bank[i].x)
                return p.y >= Mathf.Lerp(bank[i - 1].y, bank[i].y, Mathf.InverseLerp(bank[i - 1].x, bank[i].x, p.x));
        return false;
    }

    static T GetOrAdd<T>(GameObject holder) where T : Component =>
        holder.TryGetComponent<T>(out var component) ? component : holder.AddComponent<T>();
}
