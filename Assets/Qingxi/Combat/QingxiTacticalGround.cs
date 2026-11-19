using UnityEngine;
using UnityEngine.Tilemaps;

// The placeable tiles give combat and Scene view the same full-map footprint.
[DisallowMultipleComponent]
public sealed class QingxiTacticalGround : MonoBehaviour
{
    [SerializeField] Tilemap tiles;
    [SerializeField] Rect worldBounds;

    public Tilemap Tilemap => tiles;
    public Rect WorldBounds => worldBounds;

    public bool HasGround(Vector2Int absoluteCell) =>
        tiles && tiles.HasTile(new Vector3Int(absoluteCell.x, absoluteCell.y, 0));

    public static QingxiTacticalGround Find(Transform actor)
    {
        if (!actor) return null;
        var local = actor.root.GetComponentInChildren<QingxiTacticalGround>();
        if (local) return local;

        // Qingxi keeps its ground and player in separate roots. Regional maps keep
        // both within one prefab, which also avoids picking a stage being unloaded.
        var scene = actor.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var ground = root.GetComponentInChildren<QingxiTacticalGround>();
            if (ground) return ground;
        }
        return null;
    }
}
