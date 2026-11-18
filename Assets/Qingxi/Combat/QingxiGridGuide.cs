using UnityEngine;

[DisallowMultipleComponent]
public sealed class QingxiGridGuide : MonoBehaviour
{
    [SerializeField, Tooltip("在此世界坐标矩形内显示地格。")]
    Rect worldBounds = new Rect(-11f, -8.5f, 56f, 24.5f);

    [SerializeField, Tooltip("在 Scene 视图中标出这个战棋坐标。")]
    Vector2Int inspectCell;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (Camera.current == null || Camera.current.cameraType != CameraType.SceneView ||
            worldBounds.width <= 0f || worldBounds.height <= 0f) return;

        Vector2Int a = QingxiGridMetric.Index(worldBounds.min);
        Vector2Int b = QingxiGridMetric.Index(worldBounds.max);
        Vector2Int c = QingxiGridMetric.Index(new Vector2(worldBounds.xMin, worldBounds.yMax));
        Vector2Int d = QingxiGridMetric.Index(new Vector2(worldBounds.xMax, worldBounds.yMin));
        Vector2Int min = Vector2Int.Min(Vector2Int.Min(a, b), Vector2Int.Min(c, d));
        Vector2Int max = Vector2Int.Max(Vector2Int.Max(a, b), Vector2Int.Max(c, d));
        Vector2 u = QingxiGridMetric.Offset(Vector2Int.right);
        Vector2 v = QingxiGridMetric.Offset(Vector2Int.up);

        // Lines run halfway between adjacent cell centres, exactly as the battle overlay does.
        Gizmos.color = new Color(0.13f, 0.41f, 0.35f, 0.70f);
        for (int x = min.x - 1; x <= max.x; x++)
            DrawClippedLine(QingxiGridMetric.Offset(new Vector2Int(x, 0)) + u * 0.5f, v);
        for (int y = min.y - 1; y <= max.y; y++)
            DrawClippedLine(QingxiGridMetric.Offset(new Vector2Int(0, y)) + v * 0.5f, u);

        Vector2 centre = QingxiGridMetric.Offset(inspectCell);
        if (!worldBounds.Contains(centre)) return;
        Vector2 top = (u + v) * .5f;
        Vector2 right = (u - v) * .5f;
        Gizmos.color = new Color(1f, .73f, .28f, 1f);
        Gizmos.DrawLine(centre + top, centre + right);
        Gizmos.DrawLine(centre + right, centre - top);
        Gizmos.DrawLine(centre - top, centre - right);
        Gizmos.DrawLine(centre - right, centre + top);
        UnityEditor.Handles.Label(centre + top, $"地格 ({inspectCell.x}, {inspectCell.y})");
    }

    void DrawClippedLine(Vector2 point, Vector2 direction)
    {
        float start = float.NegativeInfinity;
        float end = float.PositiveInfinity;
        if (!ClipAxis(point.x, direction.x, worldBounds.xMin, worldBounds.xMax, ref start, ref end) ||
            !ClipAxis(point.y, direction.y, worldBounds.yMin, worldBounds.yMax, ref start, ref end)) return;
        Gizmos.DrawLine(point + direction * start, point + direction * end);
    }

    static bool ClipAxis(float position, float direction, float min, float max, ref float start, ref float end)
    {
        if (Mathf.Approximately(direction, 0f)) return position >= min && position <= max;
        float a = (min - position) / direction;
        float b = (max - position) / direction;
        start = Mathf.Max(start, Mathf.Min(a, b));
        end = Mathf.Min(end, Mathf.Max(a, b));
        return start <= end;
    }
#endif
}
