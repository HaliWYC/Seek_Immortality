using System.Collections.Generic;
using UnityEngine;

// The loaded scene's ground, footprints and crossings are shared by deployment,
// highlights, player movement and opponent pathfinding.
public sealed class QingxiBattlefield
{
    const float FootRadius = 0.26f;
    static readonly Vector2 FootOffset = new Vector2(0f, 0.20f);
    static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    readonly bool[,] east;
    readonly bool[,] north;
    readonly bool[,] ground;
    readonly Collider2D[] overlaps = new Collider2D[16];
    readonly RaycastHit2D[] crossings = new RaycastHit2D[16];
    readonly Dictionary<long, bool> sight = new Dictionary<long, bool>();
    readonly ContactFilter2D solids = new ContactFilter2D { useTriggers = false };
    readonly Rigidbody2D walkerBody;
    readonly Collider2D participatingGuide;
    readonly Vector2Int origin;
    public Vector2 Origin => QingxiGridMetric.Offset(origin);
    public Bounds MapBounds { get; }
    public Bounds ViewBounds { get; private set; }
    public SparMatch Match { get; private set; }
    System.Func<int, int, SparMatch> replay;
    public SparMatch ReplayPractice(int gearAttack = 0, int gearHealth = 0) => replay?.Invoke(gearAttack, gearHealth);

    int Columns => ground.GetLength(0);
    int Rows => ground.GetLength(1);

    QingxiBattlefield(Rigidbody2D walker, QingxiTacticalGround surface, Collider2D guide)
    {
        walkerBody = walker;
        participatingGuide = guide;
        Rect area = surface.WorldBounds;
        MapBounds = new Bounds(area.center, area.size);
        var a = QingxiGridMetric.Index(area.min);
        var b = QingxiGridMetric.Index(area.max);
        var c = QingxiGridMetric.Index(new Vector2(area.xMin, area.yMax));
        var d = QingxiGridMetric.Index(new Vector2(area.xMax, area.yMin));
        origin = Vector2Int.Min(Vector2Int.Min(a, b), Vector2Int.Min(c, d));
        var limit = Vector2Int.Max(Vector2Int.Max(a, b), Vector2Int.Max(c, d));
        var size = limit - origin + Vector2Int.one;
        east = new bool[size.x, size.y];
        north = new bool[size.x, size.y];
        ground = new bool[size.x, size.y];
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
        {
            var cell = new Vector2Int(x, y);
            ground[x, y] = surface.HasGround(origin + cell) && CanStand(World(cell));
        }
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
        {
            if (!ground[x, y]) continue;
            Vector2 start = World(new Vector2Int(x, y));
            if (x + 1 < Columns && ground[x + 1, y]) east[x, y] = ClearBetween(start, World(new Vector2Int(x + 1, y)));
            if (y + 1 < Rows && ground[x, y + 1]) north[x, y] = ClearBetween(start, World(new Vector2Int(x, y + 1)));
        }
    }

    public static QingxiBattlefield Sample(Rigidbody2D walker, Vector2 opponent, Collider2D guide = null,
        FieldParty party = FieldParty.Practice, string name = "药铺学徒", int cultivationBonus = 0,
        bool preparedWard = false, bool studiedWard = false, Vector2? secondOpponent = null,
        CultivatorAttributes? heroAttributes = null, float? heroMana = null, int practiceGearAttack = 0, int practiceGearHealth = 0)
    {
        var surface = walker ? QingxiTacticalGround.Find(walker.transform) : null;
        if (!surface) return null;
        Physics2D.SyncTransforms();
        var patch = new QingxiBattlefield(walker, surface, guide);
        return patch.Deploy(walker.position, opponent, party, name, cultivationBonus,
            preparedWard, studiedWard, secondOpponent, heroAttributes, heroMana, practiceGearAttack, practiceGearHealth) ? patch : null;
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
        if (b.x < 0 || b.y < 0 || b.x >= Columns || b.y >= Rows) return false;
        return a.x != b.x ? east[Mathf.Min(a.x, b.x), a.y] : north[a.x, Mathf.Min(a.y, b.y)];
    }

    bool Deploy(Vector2 player, Vector2 opponent, FieldParty party, string name, int cultivationBonus, bool preparedWard, bool studiedWard,
        Vector2? secondOpponent, CultivatorAttributes? heroAttributes, float? heroMana, int practiceGearAttack, int practiceGearHealth)
    {
        Vector2Int first = new Vector2Int(-1, -1);
        float nearest = 1.5f * 1.5f;
        Vector2Int walkCell = QingxiGridMetric.Index(player) - origin;
        // The search for a legal start stays near the existing actor. Only the
        // deployment is local; the chosen cells do not constrain movement.
        const int landingSearch = 3;
        for (int x = Mathf.Max(0, walkCell.x - landingSearch); x < Columns && x <= walkCell.x + landingSearch; x++)
        for (int y = Mathf.Max(0, walkCell.y - landingSearch); y < Rows && y <= walkCell.y + landingSearch; y++)
        {
            if (!ground[x, y]) continue;
            var c = new Vector2Int(x, y);
            var w = World(c);
            float distance = (w - player).sqrMagnitude;
            if (distance <= nearest && ClearBetween(player, w)) { first = c; nearest = distance; }
        }
        if (first.x < 0) return false;

        var visited = new bool[Columns, Rows];
        var cells = new List<Vector2Int> { first };
        visited[first.x, first.y] = true;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            foreach (var d in Directions)
            {
                var next = c + d;
                if (!Linked(c, next) || visited[next.x, next.y]) continue;
                visited[next.x, next.y] = true;
                cells.Add(next);
            }
        }
        if (cells.Count < 8) return false;
        var blocked = new bool[Columns, Rows];
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
            blocked[x, y] = !ground[x, y];

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
        var startingCells = new[] { first, puppet, rival, partner };
        var bounds = new Bounds(World(first), Vector3.zero);
        foreach (var cell in startingCells) bounds.Encapsulate(World(cell));
        ViewBounds = bounds;
        SparMatch StartMatch(int attack, int health) => new SparMatch(blocked, startingCells, Linked,
            HasSight, party, name, cultivationBonus, preparedWard, studiedWard, heroAttributes, heroMana, attack, health);
        Match = StartMatch(practiceGearAttack, practiceGearHealth);
        if (party == FieldParty.Practice) replay = StartMatch;
        return true;
    }

    bool HasSight(Vector2Int a, Vector2Int b)
    {
        int i = a.x + a.y * Columns, j = b.x + b.y * Columns;
        long key = (long)Mathf.Min(i, j) * Columns * Rows + Mathf.Max(i, j);
        if (sight.TryGetValue(key, out bool result)) return result;
        Vector2 start = World(a) + FootOffset;
        Vector2 end = World(b) + FootOffset;
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
