using System.Collections.Generic;
using UnityEngine;

// A local sample of the exploration map. The same footprints and crossings are used
// for deployment, highlights, player movement and opponent pathfinding.
public sealed class QingxiBattlefield
{
    const int Span = 13;
    const float FootRadius = 0.26f;
    static readonly Vector2 FootOffset = new Vector2(0f, 0.20f);
    static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    readonly bool[,] east = new bool[Span, Span];
    readonly bool[,] north = new bool[Span, Span];
    readonly bool[,] ground = new bool[Span, Span];
    readonly Collider2D[] overlaps = new Collider2D[16];
    readonly RaycastHit2D[] crossings = new RaycastHit2D[16];
    readonly Dictionary<int, bool> sight = new Dictionary<int, bool>();
    readonly ContactFilter2D solids = new ContactFilter2D { useTriggers = false };
    readonly Rigidbody2D walkerBody;
    readonly Collider2D participatingGuide;
    readonly Vector2Int origin;
    Vector2Int trim;
    public Vector2 Origin => QingxiGridMetric.Offset(origin + trim);
    public Bounds ViewBounds { get; private set; }
    public SparMatch Match { get; private set; }

    QingxiBattlefield(Rigidbody2D walker, Vector2 centre, Collider2D guide)
    {
        walkerBody = walker;
        participatingGuide = guide;
        origin = QingxiGridMetric.Index(centre) - new Vector2Int(Span / 2, Span / 2);
        for (int x = 0; x < Span; x++)
        for (int y = 0; y < Span; y++)
        {
            Vector2 w = World(new Vector2Int(x, y)), d = w - centre;
            // Limit a short spar to the clearing immediately around the participants.
            if (d.x * d.x / 48f + d.y * d.y / 15f <= 1f) ground[x, y] = CanStand(w);
        }
        for (int x = 0; x < Span; x++)
        for (int y = 0; y < Span; y++)
        {
            if (!ground[x, y]) continue;
            Vector2 start = World(new Vector2Int(x, y));
            if (x + 1 < Span && ground[x + 1, y]) east[x, y] = ClearBetween(start, World(new Vector2Int(x + 1, y)));
            if (y + 1 < Span && ground[x, y + 1]) north[x, y] = ClearBetween(start, World(new Vector2Int(x, y + 1)));
        }
    }

    public static QingxiBattlefield Sample(Rigidbody2D walker, Vector2 opponent, Collider2D guide = null,
        FieldParty party = FieldParty.Practice, string name = "药铺学徒", int cultivationBonus = 0,
        bool preparedWard = false, bool studiedWard = false, Vector2? secondOpponent = null)
    {
        Physics2D.SyncTransforms();
        var patch = new QingxiBattlefield(walker, (walker.position + opponent) * 0.5f, guide);
        return patch.Deploy(walker.position, opponent, party, name, cultivationBonus, preparedWard, studiedWard, secondOpponent) ? patch : null;
    }

    Vector2 World(Vector2Int cell) => QingxiGridMetric.Offset(origin + cell);
    bool CanStand(Vector2 w)
    {
        int count = Physics2D.OverlapCircle(w + FootOffset, FootRadius, solids, overlaps);
        for (int i = 0; i < count; i++)
            if (overlaps[i] != participatingGuide && overlaps[i].attachedRigidbody != walkerBody) return false;
        return true;
    }

    bool ClearBetween(Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < 0.000001f) return true;
        int count = Physics2D.CircleCast(a + FootOffset, FootRadius, delta.normalized, solids, crossings, delta.magnitude);
        for (int i = 0; i < count; i++)
            if (crossings[i].collider != participatingGuide && crossings[i].rigidbody != walkerBody) return false;
        return true;
    }

    bool Linked(Vector2Int a, Vector2Int b)
    {
        if (b.x < 0 || b.y < 0 || b.x >= Span || b.y >= Span) return false;
        return a.x != b.x ? east[Mathf.Min(a.x, b.x), a.y] : north[a.x, Mathf.Min(a.y, b.y)];
    }

    bool Deploy(Vector2 player, Vector2 opponent, FieldParty party, string name, int cultivationBonus, bool preparedWard, bool studiedWard, Vector2? secondOpponent)
    {
        Vector2Int first = new Vector2Int(-1, -1);
        float nearest = 1.5f * 1.5f;
        for (int x = 0; x < Span; x++)
        for (int y = 0; y < Span; y++)
        {
            if (!ground[x, y]) continue;
            var c = new Vector2Int(x, y);
            var w = World(c);
            float distance = (w - player).sqrMagnitude;
            if (distance <= nearest && ClearBetween(player, w)) { first = c; nearest = distance; }
        }
        if (first.x < 0) return false;

        var visited = new bool[Span, Span];
        var cells = new List<Vector2Int> { first };
        visited[first.x, first.y] = true;
        Vector2Int lo = first, hi = first;
        var bounds = new Bounds(World(first), Vector3.zero);
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            lo = Vector2Int.Min(lo, c);
            hi = Vector2Int.Max(hi, c);
            bounds.Encapsulate(World(c));
            foreach (var d in Directions)
            {
                var next = c + d;
                if (!Linked(c, next) || visited[next.x, next.y]) continue;
                visited[next.x, next.y] = true;
                cells.Add(next);
            }
        }
        if (cells.Count < 8) return false;
        trim = lo;
        ViewBounds = bounds;
        var blocked = new bool[hi.x - lo.x + 1, hi.y - lo.y + 1];
        for (int x = 0; x < blocked.GetLength(0); x++)
        for (int y = 0; y < blocked.GetLength(1); y++)
            blocked[x, y] = !visited[x + lo.x, y + lo.y];

        Vector2 axis = opponent - player;
        if (axis.sqrMagnitude < 0.01f) axis = Vector2.right;
        axis.Normalize();
        Vector2 flank = new Vector2(-axis.y, axis.x);
        cells.Remove(first);
        Vector2Int rival = TakeNearest(cells, opponent);
        Vector2Int puppet = TakeNearest(cells, player - axis * 0.7f + flank * 1.5f);
        Vector2 partnerWorld = party == FieldParty.Practice || party == FieldParty.Brigands ? opponent + axis * 0.9f - flank * 1.1f :
            player + axis * 1.4f - flank;
        Vector2Int partner = TakeNearest(cells, secondOpponent ?? partnerWorld);
        Match = new SparMatch(blocked, new[] { first-lo, puppet-lo, rival-lo, partner-lo },
            (a, b) => Linked(a + trim, b + trim), HasSight, party, name, cultivationBonus, preparedWard, studiedWard);
        return true;
    }

    bool HasSight(Vector2Int a, Vector2Int b)
    {
        int i = a.x + a.y * Span, j = b.x + b.y * Span;
        int key = Mathf.Min(i, j) * Span * Span + Mathf.Max(i, j);
        if (sight.TryGetValue(key, out bool result)) return result;
        Vector2 start = World(a + trim) + FootOffset;
        Vector2 end = World(b + trim) + FootOffset;
        int count = Physics2D.Linecast(start, end, solids, crossings);
        result = true;
        for (int hit = 0; hit < count; hit++)
        {
            // The creek and low railings have edge colliders. Solid props use boxes.
            if (crossings[hit].rigidbody == walkerBody || !(crossings[hit].collider is BoxCollider2D)) continue;
            result = false;
            break;
        }
        sight[key] = result;
        return result;
    }

    Vector2Int TakeNearest(List<Vector2Int> cells, Vector2 point)
    {
        int best = 0;
        for (int i = 1; i < cells.Count; i++)
            if ((World(cells[i]) - point).sqrMagnitude < (World(cells[best]) - point).sqrMagnitude) best = i;
        Vector2Int result = cells[best];
        cells.RemoveAt(best);
        return result;
    }
}
