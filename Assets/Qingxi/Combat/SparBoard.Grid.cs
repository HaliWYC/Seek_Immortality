using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed partial class SparBoard
{
    Tilemap groundMarks, orderMarks;
    Tile groundTile, reachableTile, selectedTile, targetTile, routeTile, destinationTile;
    Dictionary<Vector3Int, Tile> visibleMarks = new Dictionary<Vector3Int, Tile>();
    Dictionary<Vector3Int, Tile> nextMarks = new Dictionary<Vector3Int, Tile>();

    void LayoutMarks(SparMatch match)
    {
        if (!groundMarks) CreateMarkLayers();
        groundMarks.ClearAllTiles();
        orderMarks.ClearAllTiles();
        visibleMarks.Clear();
        var tiles = new TileBase[match.Columns * match.Rows];
        for (int y = 0; y < match.Rows; y++)
        for (int x = 0; x < match.Columns; x++)
            if (!match.IsBlocked(new Vector2Int(x, y))) tiles[y * match.Columns + x] = groundTile;
        groundMarks.SetTilesBlock(new BoundsInt(0, 0, 0, match.Columns, match.Rows, 1), tiles);
    }

    void CreateMarkLayers()
    {
        gridStamp.enabled = false;
        var grid = new GameObject("战斗地格", typeof(Grid)).GetComponent<Grid>();
        grid.transform.SetParent(transform, false);
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = QingxiGridMetric.TilemapCellSize;
        groundMarks = CreateMarkLayer(grid.transform, "全图格线", gridStamp.sortingOrder);
        orderMarks = CreateMarkLayer(grid.transform, "战斗指令高亮", gridStamp.sortingOrder + 1);
        groundTile = CreateMarker(gridStamp.sprite, Plain);
        reachableTile = CreateMarker(rangeStamp, Reachable);
        selectedTile = CreateMarker(rangeStamp, Selected);
        targetTile = CreateMarker(rangeStamp, Target);
        routeTile = CreateMarker(rangeStamp, Route);
        destinationTile = CreateMarker(rangeStamp, Destination);
    }

    Tilemap CreateMarkLayer(Transform parent, string label, int sortingOrder)
    {
        var layer = new GameObject(label, typeof(Tilemap), typeof(TilemapRenderer));
        layer.transform.SetParent(parent, false);
        var renderer = layer.GetComponent<TilemapRenderer>();
        renderer.sortingLayerID = gridStamp.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = gridStamp.sharedMaterial;
        var map = layer.GetComponent<Tilemap>();
        map.tileAnchor = Vector3.zero;
        return map;
    }

    Tile CreateMarker(Sprite sprite, Color tint)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = tint;
        tile.transform = Matrix4x4.Scale(gridStamp.transform.localScale);
        tile.colliderType = Tile.ColliderType.None;
        return tile;
    }

    void UpdateMarks(SparMatch match, SparUnit selected, IReadOnlyList<Vector2Int> previewPath, Vector2Int? pendingTarget)
    {
        nextMarks.Clear();
        if (selected != null && selected.Alive)
        {
            foreach (var cell in match.MovesInRange(selected)) nextMarks[(Vector3Int)cell] = reachableTile;
            foreach (var unit in match.Units)
                if (unit.Cell != selected.Cell && match.CanAttack(selected, unit.Cell))
                    nextMarks[(Vector3Int)unit.Cell] = targetTile;
            nextMarks[(Vector3Int)selected.Cell] = selectedTile;
        }
        if (previewPath != null)
            for (int i = 1; i < previewPath.Count; i++) nextMarks[(Vector3Int)previewPath[i]] = routeTile;
        if (pendingTarget.HasValue) nextMarks[(Vector3Int)pendingTarget.Value] = destinationTile;

        // The ground remains batched. Cursor movement only updates changed highlights.
        foreach (var mark in visibleMarks)
            if (!nextMarks.ContainsKey(mark.Key)) orderMarks.SetTile(mark.Key, null);
        foreach (var mark in nextMarks)
            if (!visibleMarks.TryGetValue(mark.Key, out var previous) || previous != mark.Value)
                orderMarks.SetTile(mark.Key, mark.Value);
        (visibleMarks, nextMarks) = (nextMarks, visibleMarks);
    }

    void ReleaseMarkerTiles()
    {
        Destroy(groundTile);
        Destroy(reachableTile);
        Destroy(selectedTile);
        Destroy(targetTile);
        Destroy(routeTile);
        Destroy(destinationTile);
    }
}
