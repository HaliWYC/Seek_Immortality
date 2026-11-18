using UnityEngine;

public enum QingxiArea { Town, Gate, Foothill, Trail }

[RequireComponent(typeof(Collider2D))]
public sealed class QingxiLandmark : MonoBehaviour
{
    public QingxiArea kind;
    public string placeName;

    void OnTriggerEnter2D(Collider2D other)
    {
        var body = other.attachedRigidbody;
        if (body != null && body.TryGetComponent(out QingxiWalker walker))
            walker.Reach(this);
    }

    void OnDrawGizmosSelected()
    {
        var area = GetComponent<Collider2D>();
        if (area == null) return;
        Gizmos.color = new Color(0.45f, 0.78f, 0.62f, 0.8f);
        Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
    }
}
