using UnityEngine;

// Both exploration coordinates and encounters share this fixed map origin.
public static class QingxiGridMetric
{
    public const float HalfWidth = 0.82f;
    public const float HalfHeight = 0.45f;
    public static Vector3 TilemapCellSize => new Vector3(2 * HalfWidth, 2 * HalfHeight, 1f);

    public static Vector2 Offset(Vector2Int cell) =>
        new Vector2((cell.x - cell.y) * HalfWidth, (cell.x + cell.y) * HalfHeight);

    public static Vector2Int Index(Vector2 position) => new Vector2Int(
        Mathf.RoundToInt((position.x / HalfWidth + position.y / HalfHeight) * 0.5f),
        Mathf.RoundToInt((position.y / HalfHeight - position.x / HalfWidth) * 0.5f));
}
