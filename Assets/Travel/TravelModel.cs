using System;
using UnityEngine;

public enum MapPlace { World, Town, Pass, Sect }
public enum TravelRoute { Road, Trail }

/// <summary>Map coordinates use a top-left origin. The clock advances only for distance actually
/// travelled or a deliberate rest. All travel and discovery rules are shared by manual and guided movement.</summary>
public sealed partial class TravelModel
{
    public static readonly Vector2 Town = new Vector2(130, 365);
    public static readonly Vector2 Pass = new Vector2(500, 213);
    public static readonly Vector2 Sect = new Vector2(852, 183);
    public static readonly Vector2 LocalGate = new Vector2(500, 525);
    static readonly Vector2 LocalArrival = LocalGate - new Vector2(0, 43);

    public static readonly Vector2[] Road = {
        Town, new Vector2(213, 475), new Vector2(390, 514), new Vector2(594, 505),
        new Vector2(747, 432), new Vector2(851, 317), Sect
    };
    public static readonly Vector2[] Trail = {
        Town, new Vector2(233, 310), new Vector2(335, 244), new Vector2(419, 199), Pass,
        new Vector2(594, 238), new Vector2(688, 190), new Vector2(764, 155), Sect
    };
    public static readonly Vector2[] Ridge = {
        new Vector2(349, 100), new Vector2(570, 66), new Vector2(715, 134),
        new Vector2(747, 272), new Vector2(685, 371), new Vector2(552, 425),
        new Vector2(458, 401), new Vector2(359, 425), new Vector2(287, 332),
        new Vector2(296, 211)
    };
    static readonly Rect[] TownBuildings = {
        new Rect(175, 160, 142, 80), new Rect(368, 112, 145, 90), new Rect(614, 152, 150, 90),
        new Rect(216, 323, 117, 69), new Rect(674, 330, 113, 70)
    };
    static readonly Rect[] SectBuildings = {
        new Rect(355, 102, 290, 95), new Rect(174, 253, 143, 78), new Rect(675, 253, 143, 78)
    };
    static readonly Rect[] PassBuildings = {
        new Rect(135, 156, 195, 115), new Rect(622, 95, 206, 136), new Rect(695, 380, 126, 75)
    };

    const float RoadWidth = 36f;
    const float TrailWidth = 30f;
    const float PlaceRadius = 35f;
    const float GateRadius = 58f;
    const float RoadRate = 3.5f;
    const float TrailRate = 4f;
    const float WildRate = 5.2f;
    const float FlightRate = 2.2f;
    const float LocalDistanceScale = 1f / 180f;
    const float ManaPerUnit = 0.075f;

    public MapPlace Place { get; private set; } = MapPlace.Town;
    public Func<Vector2, bool> LocalObstruction { private get; set; }
    public Vector2 Position { get; private set; } = LocalArrival;
    public double Minutes { get; private set; }
    public bool Flying { get; private set; }
    public float Mana { get; private set; } = 100f;
    public string Notice { get; private set; } = "镇口整装。沿官道拜山，或向松风隘自行探路。";
    public bool IsAuto => guidedPath != null;
    public int SeenMask { get; private set; }
    public bool TrailKnown => SeenMask == FullTrailMask;
    static int FullTrailMask => (1 << (Trail.Length - 1)) - 1;
    // An edge is confirmed by walking through its full length, not by touching both endpoints.
    readonly byte[] footprints = new byte[Trail.Length - 1];
    public float TrailFraction
    {
        get
        {
            int visited = 0;
            foreach (byte edge in footprints)
                for (int bit = 0; bit < 8; bit++)
                    visited += (edge >> bit) & 1;
            return visited / (8f * footprints.Length);
        }
    }
    public bool SegmentSeen(int segment) => segment >= 0 && segment < footprints.Length &&
                                            (SeenMask & (1 << segment)) != 0;
    public void RestoreSeen(int mask)
    {
        SeenMask = mask & FullTrailMask;
        for (int i = 0; i < footprints.Length; ++i)
            footprints[i] = SegmentSeen(i) ? byte.MaxValue : (byte)0;
    }

    Vector2[] guidedPath;
    int guidedIndex;
    int guidedDirection;
    MapPlace worldAnchor = MapPlace.Town;
    Vector2 returnToWorld = Town;
    Vector2 ridgeLanding;
    Vector2 ridgeLaunch;
    bool overRidge;
    bool trackingTrip;
    double departed;
    float roadDistance, trailDistance, otherDistance, walkedDistance, flownDistance, ridgeDistance;
    public int CompletedTrips { get; private set; }
    public double LastTripMinutes { get; private set; }
    public string LastTripLabel { get; private set; } = "";
    public double TripMinutes => trackingTrip ? Minutes - departed : 0d;

    public static Rect[] Buildings(MapPlace place)
    {
        switch (place)
        {
            case MapPlace.Town: return TownBuildings;
            case MapPlace.Sect: return SectBuildings;
            case MapPlace.Pass: return PassBuildings;
            default: return Array.Empty<Rect>();
        }
    }

    static float Projection(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 line = b - a;
        return Mathf.Clamp01(Vector2.Dot(point - a, line) / line.sqrMagnitude);
    }

    static float DistanceToPath(Vector2 point, Vector2[] path)
    {
        float shortest = float.MaxValue;
        for (int i = 1; i < path.Length; i++)
        {
            Vector2 closest = Vector2.Lerp(path[i - 1], path[i], Projection(point, path[i - 1], path[i]));
            shortest = Mathf.Min(shortest, (point - closest).sqrMagnitude);
        }
        return Mathf.Sqrt(shortest);
    }

    static bool InBounds(Vector2 point) => point.x >= 20 && point.x <= 980 && point.y >= 20 && point.y <= 620;
    static bool InsideRidge(Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = Ridge.Length - 1; i < Ridge.Length; j = i++)
        {
            Vector2 a = Ridge[i], b = Ridge[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x-a.x) * (p.y-a.y) / (b.y-a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    public static bool Walkable(MapPlace place, Vector2 point)
    {
        if (!InBounds(point)) return false;
        if (place == MapPlace.World)
            return !InsideRidge(point) || DistanceToPath(point, Trail) <= TrailWidth ||
                   DistanceToPath(point, Road) <= RoadWidth;
        foreach (Rect rect in Buildings(place))
            if (rect.Contains(point)) return false;
        return true;
    }

    static float GroundRate(Vector2 point)
    {
        if (DistanceToPath(point, Road) <= RoadWidth) return RoadRate;
        return DistanceToPath(point, Trail) <= TrailWidth ? TrailRate : WildRate;
    }

    public string TerrainLabel
    {
        get
        {
            if (Place != MapPlace.World) return PlaceName(Place);
            if (Flying && !Walkable(Place, Position)) return "凌空越岭";
            float rate = GroundRate(Position);
            return rate == RoadRate ? "官道" : rate == TrailRate ? "山路" : "旷野";
        }
    }

    static string PlaceName(MapPlace place)
    {
        switch (place)
        {
            case MapPlace.Town: return "青溪镇";
            case MapPlace.Pass: return "松风隘";
            case MapPlace.Sect: return "栖霞宗";
            default: return "青岚山境";
        }
    }

    static Vector2 Endpoint(MapPlace place) => place == MapPlace.Town ? Town : Sect;
    static MapPlace Opposite(MapPlace place) => place == MapPlace.Town ? MapPlace.Sect : MapPlace.Town;

    MapPlace NearestPlace()
    {
        if (Vector2.Distance(Position, Town) <= PlaceRadius) return MapPlace.Town;
        if (Vector2.Distance(Position, Sect) <= PlaceRadius) return MapPlace.Sect;
        if (Vector2.Distance(Position, Pass) <= PlaceRadius) return MapPlace.Pass;
        return MapPlace.World;
    }

    public string InteractLabel
    {
        get
        {
            if (Place != MapPlace.World)
                return Vector2.Distance(Position, LocalGate) <= GateRadius ? "前往大地图" : "";
            MapPlace nearby = NearestPlace();
            return nearby == MapPlace.World ? "" : "进入" + PlaceName(nearby);
        }
    }

    public void Interact()
    {
        if (InteractLabel.Length == 0) return;
        CancelAuto();
        if (Place != MapPlace.World)
        {
            string name = PlaceName(Place);
            Place = MapPlace.World;
            Position = returnToWorld;
            Notice = "离开" + name + "，已展开舆图。";
            return;
        }

        MapPlace destination = NearestPlace();
        if (trackingTrip && destination == Opposite(worldAnchor)) CompleteTrip(destination);
        else if (trackingTrip && destination == worldAnchor) trackingTrip = false;
        returnToWorld = Position;
        Place = destination;
        Position = LocalArrival;
        Flying = false;
        overRidge = false;
        Notice = "抵达" + PlaceName(Place) + "。从南侧出口可返回原处。";
    }

    public void ToggleFlight()
    {
        if (Place != MapPlace.World) { Notice = "在大地图上可御剑赶路。"; return; }
        if (Flying && !Walkable(Place, Position)) { Notice = "下方是断崖，请先飞至可落脚处。"; return; }
        if (!Flying && Mana < 1f) { Notice = "法力不足，请先调息。"; return; }
        Flying = !Flying;
        Notice = Flying ? "御剑而起，飞行按距离耗时、耗法力。" : "收剑落地，改为步行。";
    }

    public void Notify(string message) => Notice = message;

    public bool CanRest => Mana < 99.999f && Walkable(Place, Position);
    public void Rest()
    {
        if (!CanRest)
        {
            Notice = Mana >= 99.999f ? "当前法力充沛。" : "此处无立足之地，无法调息。";
            return;
        }
        CancelAuto();
        Flying = false;
        Minutes += 120;
        Mana = 100;
        Notice = "静坐一个时辰，法力已复。";
    }

    public bool CanStartRoute(TravelRoute route)
    {
        if (Place != MapPlace.World || IsAuto || (route == TravelRoute.Trail && !TrailKnown)) return false;
        MapPlace nearby = NearestPlace();
        return nearby == MapPlace.Town || nearby == MapPlace.Sect;
    }

    public void StartRoute(TravelRoute route)
    {
        if (!CanStartRoute(route))
        {
            Notice = route == TravelRoute.Trail && !TrailKnown ? "先亲自步行探通山路，再循迹赶路。" : "请在大地图的镇口或山门启程。";
            return;
        }
        MapPlace origin = NearestPlace();
        if (trackingTrip && origin != worldAnchor) CompleteTrip(origin);
        if (!trackingTrip) BeginTrip();
        guidedPath = route == TravelRoute.Road ? Road : Trail;
        bool forward = origin == MapPlace.Town;
        guidedDirection = forward ? 1 : -1;
        guidedIndex = forward ? 0 : guidedPath.Length - 1;
        Notice = (forward ? "前往栖霞宗" : "返回青溪镇") + (route == TravelRoute.Road ? " · 沿官道赶路" : " · 沿已探明山路赶路");
    }

    public void CancelAuto()
    {
        if (!IsAuto) return;
        guidedPath = null;
        Notice = "已停下，可自行探路。";
    }

    float PixelsPerSecond()
    {
        if (Place != MapPlace.World) return 165f;
        return Flying ? 100f : 72f * RoadRate / GroundRate(Position);
    }

    public void TickAuto(float deltaSeconds)
    {
        while (IsAuto && deltaSeconds > 0.00001f)
        {
            Vector2 offset = guidedPath[guidedIndex] - Position;
            float remaining = offset.magnitude;
            if (remaining < 0.001f)
            {
                guidedIndex += guidedDirection;
                if (guidedIndex >= 0 && guidedIndex < guidedPath.Length) continue;
                guidedPath = null;
                MapPlace destination = NearestPlace();
                CompleteTrip(destination);
                Notice = "已到" + PlaceName(destination) + "，可进入局部地图。";
                return;
            }
            float speed = PixelsPerSecond();
            float step = Mathf.Min(remaining, Mathf.Min(speed * deltaSeconds, 3f));
            if (!Advance(offset / remaining, step))
            {
                CancelAuto();
                return;
            }
            deltaSeconds -= step / speed;
        }
    }

    public void Move(Vector2 direction, float deltaSeconds)
    {
        if (direction.sqrMagnitude < 0.000001f || deltaSeconds <= 0f) return;
        CancelAuto();
        direction.Normalize();
        while (deltaSeconds > 0.000001f)
        {
            float speed = PixelsPerSecond();
            float step = Mathf.Min(speed * deltaSeconds, 3f);
            if (!Advance(direction, step)) return;
            deltaSeconds -= step / speed;
        }
    }

    bool Advance(Vector2 direction, float distance)
    {
        Vector2 next = Position + direction * distance;
        if (!InBounds(next))
            next = new Vector2(Mathf.Clamp(next.x, 20, 980), Mathf.Clamp(next.y, 20, 620));

        if (Flying)
        {
            if (!PlanFlight(direction, ref next)) return false;
        }
        else if (!CanStep(next))
        {
            Vector2 across = new Vector2(next.x, Position.y);
            Vector2 down = new Vector2(Position.x, next.y);
            if (Mathf.Abs(direction.x) > 0.001f && CanStep(across)) next = across;
            else if (Mathf.Abs(direction.y) > 0.001f && CanStep(down)) next = down;
            else
            {
                if (Place == MapPlace.World) Notice = "山壁陡峭。寻找隘口，或备足法力御剑越岭。";
                return false;
            }
        }

        float actual = Vector2.Distance(Position, next);
        if (actual < 0.00001f) return false;
        if (Place != MapPlace.World)
        {
            Minutes += actual * RoadRate * LocalDistanceScale;
            Position = next;
            return true;
        }

        if (!trackingTrip && Vector2.Distance(next, Endpoint(worldAnchor)) > Vector2.Distance(Position, Endpoint(worldAnchor)))
            BeginTrip();
        float rate = GroundRate((Position + next) / 2f);
        Minutes += actual * (Flying ? FlightRate : rate);
        if (rate == RoadRate) roadDistance += actual;
        else if (rate == TrailRate) trailDistance += actual;
        else otherDistance += actual;
        if (Flying)
        {
            flownDistance += actual;
            if (!Walkable(Place, next)) ridgeDistance += actual;
            Mana = Mathf.Max(0, Mana - actual * ManaPerUnit);
        }
        else
        {
            walkedDistance += actual;
            RecordFootsteps(next);
        }
        Position = next;
        overRidge = Flying && !Walkable(Place, Position);
        if (Flying && !overRidge && Mana < 0.001f)
        {
            Flying = false;
            Notice = "法力耗尽，已就地落剑。";
        }
        MapPlace destination = Opposite(worldAnchor);
        if (!IsAuto && trackingTrip && Vector2.Distance(Position, Endpoint(destination)) < 1f)
            CompleteTrip(destination);
        return true;
    }

    bool CanStep(Vector2 point) => Walkable(Place, point) &&
        (Place == MapPlace.World || LocalObstruction == null || !LocalObstruction(point));

    // Enter only when the landing ahead can be reached. While airborne over rock, protect
    // enough mana for the nearer of the launch and landing points, even if the player turns.
    bool PlanFlight(Vector2 direction, ref Vector2 next)
    {
        bool canLand = Walkable(MapPlace.World, next);
        float available = Mana / ManaPerUnit;
        float step = Vector2.Distance(Position, next);
        if (!overRidge && !canLand)
        {
            Vector2 landing = next;
            while (InBounds(landing) && !Walkable(MapPlace.World, landing))
                landing += direction * 2f;
            if (!InBounds(landing) || available < Vector2.Distance(Position, landing) + 1f)
            {
                Notice = "法力不足以飞抵对面落脚点。请先调息或改走山口。";
                return false;
            }
            ridgeLaunch = Position;
            ridgeLanding = landing;
        }
        if (overRidge)
        {
            Vector2 escape = Vector2.Distance(Position, ridgeLaunch) < Vector2.Distance(Position, ridgeLanding) ? ridgeLaunch : ridgeLanding;
            // Keep a small margin for float rounding until a step can actually touch the ground.
            float required = canLand ? step : step + Vector2.Distance(next, escape) + 0.3f;
            if (available < required)
            {
                next = Vector2.MoveTowards(Position, escape, step);
                Notice = "余力仅够落地，正返向最近的落脚点。";
            }
        }
        else if (canLand && step > available)
            next = Vector2.MoveTowards(Position, next, available);
        return true;
    }

    void RecordFootsteps(Vector2 point)
    {
        for (int i = 0; i < footprints.Length; ++i)
        {
            if (SegmentSeen(i)) continue;
            float part = Projection(point, Trail[i], Trail[i + 1]);
            Vector2 nearest = Vector2.Lerp(Trail[i], Trail[i + 1], part);
            if ((point - nearest).sqrMagnitude > TrailWidth * TrailWidth) continue;
            int bit = Mathf.Min(7, (int)(part * 8));
            footprints[i] |= (byte)(1 << bit);
            if (footprints[i] != byte.MaxValue) continue;
            SeenMask |= 1 << i;
            Notice = TrailKnown ? "松风山路已全线探明，今后可沿旧迹赶路。" : "手记添了一段已走通的山径。";
        }
    }

    void BeginTrip()
    {
        trackingTrip = true;
        departed = Minutes;
        roadDistance = trailDistance = otherDistance = ridgeDistance = walkedDistance = flownDistance = 0f;
    }

    void CompleteTrip(MapPlace destination)
    {
        string path = ridgeDistance > 12f ? "越岭" : roadDistance >= Mathf.Max(trailDistance, otherDistance) ? "官道" :
            trailDistance >= otherDistance ? "山路" : "旷野";
        string mode = flownDistance < 1f ? "步行" : walkedDistance < 1f ? "御剑" : "步行与御剑";
        LastTripLabel = PlaceName(worldAnchor) + " → " + PlaceName(destination) + " · " + path + " · " + mode;
        LastTripMinutes = TripMinutes;
        CompletedTrips++;
        worldAnchor = destination;
        trackingTrip = false;
    }
}
