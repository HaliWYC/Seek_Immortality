using UnityEngine;

public enum HerbalSiteType { Pharmacopoeia, LivingPlant, WanderingHerbalist, PracticeMat }

[DisallowMultipleComponent]
public sealed class HerbalSite : MonoBehaviour
{
    public HerbalSiteType type;
    [Min(0.15f)] public float radius = 1.5f;
    [SerializeField] SpriteRenderer plantRenderer;
    [SerializeField] Sprite harvestedSprite;

    public bool InReach(Vector2 feet) => ((Vector2)transform.position - feet).sqrMagnitude <= radius * radius;

    public void ShowHarvestedPlant()
    {
        if (type == HerbalSiteType.LivingPlant && plantRenderer && harvestedSprite)
            plantRenderer.sprite = harvestedSprite;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = type == HerbalSiteType.Pharmacopoeia ? new Color(0.90f, 0.68f, 0.35f) : new Color(0.30f, 0.74f, 0.56f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
