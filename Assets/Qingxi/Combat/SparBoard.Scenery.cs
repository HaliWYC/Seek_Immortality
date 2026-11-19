using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class SparBoard
{
    const float OccludingOpacity = 0.36f;
    const float FadeSpeed = 6f;
    const float RestoreSpeed = 3f;

    [SerializeField] Sprite[] scenerySprites;

    readonly List<SceneryView> scenery = new List<SceneryView>();
    IReadOnlyList<Vector2Int> sceneryPath;

    readonly struct SceneryView
    {
        public readonly SpriteRenderer Renderer;
        public readonly Rect Coverage;
        public readonly Color OriginalColor;
        public readonly int SortingLayer;

        public SceneryView(SpriteRenderer renderer, Rect coverage)
        {
            Renderer = renderer;
            Coverage = coverage;
            OriginalColor = renderer.color;
            SortingLayer = SortingLayerValue(renderer.sortingLayerID);
        }

        public bool DrawsOver(int layer, int order) =>
            SortingLayer > layer || (SortingLayer == layer && Renderer.sortingOrder >= order);
    }

    void FindScenery(SparMatch match)
    {
        RestoreScenery();
        if (scenerySprites == null || scenerySprites.Length == 0) return;

        // The board's corners enclose the reachable diamonds. Allow room for the bodies above their feet.
        int right = match.Columns - 1;
        int top = match.Rows - 1;
        var bounds = new Bounds(CellWorld(Vector2Int.zero), Vector3.zero);
        bounds.Encapsulate(CellWorld(new Vector2Int(right, 0)));
        bounds.Encapsulate(CellWorld(new Vector2Int(0, top)));
        bounds.Encapsulate(CellWorld(new Vector2Int(right, top)));
        bounds.Expand(new Vector3(2f, 5f, 0f));
        var field = ViewRect(bounds);

        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>())
        {
            if (!renderer.enabled || Array.IndexOf(scenerySprites, renderer.sprite) < 0) continue;
            var coverage = ViewRect(renderer.bounds);
            if (coverage.Overlaps(field)) scenery.Add(new SceneryView(renderer, coverage));
        }
    }

    void UpdateSceneryVisibility(float elapsed)
    {
        if (scenery.Count == 0) return;
        foreach (var prop in scenery)
        {
            if (!prop.Renderer || !prop.Renderer.enabled) continue;
            bool covering = CoversPiece(prop) || CoversPath(prop);
            Color color = prop.OriginalColor;
            float opacity = covering ? OccludingOpacity : 1f;
            float speed = covering ? FadeSpeed : RestoreSpeed;
            Color current = prop.Renderer.color;
            color.a = Mathf.MoveTowards(current.a, color.a * opacity, speed * elapsed * color.a);
            if (current != color) prop.Renderer.color = color;
        }
    }

    bool CoversPiece(SceneryView prop)
    {
        foreach (var piece in pieces)
        {
            if (!piece.group.gameObject.activeInHierarchy ||
                !prop.DrawsOver(SortingLayerValue(piece.group.sortingLayerID), piece.group.sortingOrder)) continue;

            // Use the walking sprite's current position and omit its transparent side margins.
            Bounds body = piece.body.bounds;
            body.extents = new Vector3(body.extents.x * .72f, body.extents.y * .86f, body.extents.z);
            if (prop.Coverage.Overlaps(ViewRect(body))) return true;
        }
        return false;
    }

    bool CoversPath(SceneryView prop)
    {
        if (sceneryPath == null) return false;
        int layer = SortingLayerValue(pieces[0].group.sortingLayerID);
        for (int i = 1; i < sceneryPath.Count; i++)
        {
            Vector3 step = CellWorld(sceneryPath[i]);
            if (prop.DrawsOver(layer, PieceSortingOrder(step.y)) && prop.Coverage.Contains(step)) return true;
        }
        return false;
    }

    void RestoreScenery()
    {
        foreach (var prop in scenery)
            if (prop.Renderer) prop.Renderer.color = prop.OriginalColor;
        scenery.Clear();
        sceneryPath = null;
    }

    static int SortingLayerValue(int id) => SortingLayer.GetLayerValueFromID(id);
    static Rect ViewRect(Bounds bounds) => Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
}
