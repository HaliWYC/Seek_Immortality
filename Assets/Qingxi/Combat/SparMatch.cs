using System;
using System.Collections.Generic;
using UnityEngine;

public enum SparStatus { PlayerTurn, OpponentTurn, Won, Lost, Yielded }
public enum FieldParty { Practice, Solo, Ally, Contract, Brigands }

public sealed class SparUnit
{
    public string Name { get; }
    public bool Friendly { get; }
    public bool Direct { get; }
    public Vector2Int Cell { get; internal set; }
    public IReadOnlyList<Vector2Int> WalkPath { get; internal set; }
    public int Hp { get; internal set; }
    public int MaxHp { get; }
    public int Range { get; }
    public int Power { get; }
    public int Steps { get; }
    public bool Moved { get; internal set; }
    public bool Acted { get; internal set; }
    public bool Alive => Hp > 0;

    internal SparUnit(string name, bool friendly, Vector2Int cell, int hp, int range, int power, int steps, bool direct = true)
    {
        Name = name;
        Friendly = friendly;
        Direct = friendly && direct;
        Cell = cell;
        WalkPath = Array.AsReadOnly(new[] { cell });
        MaxHp = Hp = hp;
        Range = range;
        Power = power;
        Steps = steps;
    }
}

public sealed class SparMatch
{
    public const int Width = 8;
    public const int Height = 6;

    static readonly Vector2Int[] directions =
        { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    static readonly Vector2Int[] exampleDeployment =
        { new Vector2Int(1, 1), new Vector2Int(1, 4), new Vector2Int(6, 1), new Vector2Int(6, 4) };

    readonly bool[,] terrain;
    readonly Func<Vector2Int, Vector2Int, bool> canTraverse;
    readonly Func<Vector2Int, Vector2Int, bool> clearShot;
    readonly SparUnit[] units;

    // Board queries and the opposing side share the same paths until occupancy changes.
    readonly Dictionary<SparUnit, PathMap> paths = new Dictionary<SparUnit, PathMap>();
    int nextOpponentIndex;

    public int Columns => terrain.GetLength(0);
    public int Rows => terrain.GetLength(1);
    public IReadOnlyList<SparUnit> Units { get; }
    public SparStatus Status { get; private set; } = SparStatus.PlayerTurn;
    public int Round { get; private set; } = 1;
    public string Message { get; private set; } = "切磋开始，双方点到为止。";
    public FieldParty Party { get; }
    public bool InField => Party != FieldParty.Practice;
    public SparUnit Companion => Party == FieldParty.Ally || Party == FieldParty.Contract ? units[3] : null;
    public bool TalismanUsed { get; private set; }
    public bool BandageUsed { get; private set; }
    public bool WardReady { get; private set; }
    public bool WardReleased { get; private set; }
    public bool WardStudied { get; }
    public bool FormationBroken { get; private set; }
    bool stunned;
    public bool CompanionDied { get; private set; }
    public bool CompanionCarried { get; private set; }
    int lastRescueRound;
    public int RescueRounds => lastRescueRound == 0 || CompanionDied || CompanionCarried ? 0 :
        Math.Max(0, Math.Min(2, lastRescueRound - Round + 1));

    public SparMatch() : this(ExampleTerrain(), exampleDeployment) { }

    // The exploration controller supplies collision samples and four legal starting cells.
    // Snapshot the samples so world updates cannot invalidate a turn's cached paths.
    public SparMatch(bool[,] blocked, IReadOnlyList<Vector2Int> deployment,
        Func<Vector2Int, Vector2Int, bool> canTraverse = null,
        Func<Vector2Int, Vector2Int, bool> clearShot = null, FieldParty party = FieldParty.Practice,
        string name = "药铺学徒", int cultivationBonus = 0, bool preparedWard = false, bool studiedWard = false)
    {
        if (blocked == null) throw new ArgumentNullException(nameof(blocked));
        if (deployment == null) throw new ArgumentNullException(nameof(deployment));
        if (deployment.Count != 4)
            throw new ArgumentException("A spar requires exactly four starting cells.", nameof(deployment));

        terrain = (bool[,])blocked.Clone();
        this.canTraverse = canTraverse;
        this.clearShot = clearShot;
        var occupied = new HashSet<Vector2Int>();
        foreach (var cell in deployment)
            if (IsBlocked(cell) || !occupied.Add(cell))
                throw new ArgumentException("Starting cells must be distinct and walkable.", nameof(deployment));

        Party = party;
        WardReady = preparedWard;
        WardStudied = studiedWard;
        if (party == FieldParty.Practice)
        {
            units = new[]
            {
                new SparUnit("药铺学徒", true, deployment[0], 12, 3, 4, 3),
                new SparUnit("练习傀儡", true, deployment[1], 14, 1, 3, 3),
                new SparUnit("巡山弟子", false, deployment[2], 9, 2, 3, 2),
                new SparUnit("护阵弟子", false, deployment[3], 11, 1, 4, 3)
            };
        }
        else if (party == FieldParty.Brigands)
        {
            int rank = Mathf.Clamp(cultivationBonus, 0, 4);
            units = new[]
            {
                new SparUnit(name, true, deployment[0], 12 + 3 * rank, 3, 3 + rank, 3),
                new SparUnit("护行木傀", true, deployment[1], 13, 1, 3, 3),
                new SparUnit("灰衣客 · 筑基初期", false, deployment[2], 31, 3, 8, 3),
                new SparUnit("望风同伙", false, deployment[3], 8, 2, 3, 2)
            };
            Message = "灰衣客祭起护身阵。你可随时弃财脱身。";
        }
        else
        {
            int bonus = Mathf.Clamp(cultivationBonus, 0, 3);
            var partyUnits = new List<SparUnit>
            {
                new SparUnit(name, true, deployment[0], 12 + bonus * 2, 3, 4 + bonus, 3),
                new SparUnit("护行木傀", true, deployment[1], 13, 1, 3, 3),
                new SparUnit("青棘獾", false, deployment[2], 23, 2, 5, 3)
            };
            if (party != FieldParty.Solo)
                partyUnits.Add(new SparUnit("采药客阿荇", true, deployment[3], 7, 2, 3, 3, party == FieldParty.Contract));
            units = partyUnits.ToArray();
            Message = "青棘獾逼近。可驱退妖兽收取兽蜕，也可放弃兽蜕撤走。";
        }
        Units = Array.AsReadOnly(units);
    }

    static bool[,] ExampleTerrain()
    {
        var blocked = new bool[Width, Height];
        blocked[3, 1] = blocked[4, 3] = blocked[3, 4] = true;
        return blocked;
    }

    public SparUnit At(Vector2Int cell)
    {
        foreach (var unit in units)
            if ((unit.Alive || (unit == Companion && RescueRounds > 0)) && unit.Cell == cell) return unit;
        return null;
    }

    public bool IsBlocked(Vector2Int cell) =>
        cell.x < 0 || cell.x >= Columns || cell.y < 0 || cell.y >= Rows || terrain[cell.x, cell.y];

    public bool CanCommand(SparUnit unit) =>
        Status == SparStatus.PlayerTurn && unit != null && unit.Direct && unit.Alive &&
        Array.IndexOf(units, unit) >= 0;

    public bool CanMove(SparUnit unit, Vector2Int cell)
    {
        if (!CanCommand(unit) || unit.Moved || IsBlocked(cell) || At(cell) != null) return false;
        return PathsFrom(unit).Cost(cell) <= unit.Steps;
    }

    public bool HasLineOfSight(Vector2Int a, Vector2Int b) => clearShot == null || clearShot(a, b);

    bool CanHitFrom(Vector2Int from, SparUnit target, int range) =>
        Distance(from, target.Cell) <= range && HasLineOfSight(from, target.Cell);

    public bool CanAttack(SparUnit unit, Vector2Int cell)
    {
        if (!CanCommand(unit) || unit.Acted) return false;
        var target = At(cell);
        return target != null && !target.Friendly && CanHitFrom(unit.Cell, target, unit.Range);
    }

    public bool Move(SparUnit unit, Vector2Int cell)
    {
        if (!CanMove(unit, cell)) return false;
        Relocate(unit, cell);
        Message = unit.Name + "已就位。";
        return true;
    }

    public bool Attack(SparUnit unit, Vector2Int cell)
    {
        if (!CanAttack(unit, cell)) return false;
        Cast(unit, At(cell));
        return true;
    }

    public bool UseAgreedHerb(SparUnit unit)
    {
        if (Party != FieldParty.Contract || unit != Companion || !CanCommand(unit) ||
            unit.Acted || BandageUsed || unit.Hp >= unit.MaxHp) return false;
        unit.Hp = Math.Min(unit.MaxHp, unit.Hp + 4);
        unit.Acted = true;
        BandageUsed = true;
        Message = "依约用了止血散，阿荇气力恢复。";
        return true;
    }

    public bool UseFieldWard(SparUnit unit)
    {
        if (Party != FieldParty.Brigands || !WardReady || unit != units[0] || !CanCommand(unit) || unit.Acted || !units[2].Alive)
            return false;
        WardReady = false;
        WardReleased = true;
        FormationBroken = WardStudied;
        stunned = WardStudied;
        Cast(unit, units[2], WardStudied ? 13 : 6);
        if (Status == SparStatus.PlayerTurn)
            Message = WardStudied ? "阵符击中右袖的阵眼，护身阵破去，灰衣客下一次出手受阻。" :
                "阵符惊动护阵，灰衣客损失六点气力，阵眼仍在。";
        return true;
    }

    void Relocate(SparUnit unit, Vector2Int cell)
    {
        var map = PathsFrom(unit);
        var route = new Vector2Int[map.Cost(cell) + 1];
        route[route.Length - 1] = cell;
        for (int i = route.Length - 1; i > 0; i--)
        {
            Vector2Int step = route[i];
            route[i - 1] = map.Previous[step.x, step.y];
        }
        unit.WalkPath = Array.AsReadOnly(route);
        unit.Cell = cell;
        unit.Moved = true;
        paths.Clear();
    }

    void Cast(SparUnit caster, SparUnit target, int fixedPower = 0)
    {
        bool protectedBoss = Party == FieldParty.Brigands && target == units[2] && !FormationBroken;
        int power = fixedPower > 0 ? fixedPower : Math.Max(1, caster.Power - (protectedBoss ? 3 : 0));
        int hit = Math.Min(target.Hp, power);
        target.Hp -= hit;
        caster.Acted = true;
        Message = caster.Name + "命中" + target.Name + "，气力减少 " + hit + "。";
        if (target.Alive) return;

        paths.Clear();
        if (target == Companion)
        {
            if (!TalismanUsed)
            {
                TalismanUsed = true;
                target.Hp = 3;
                Message = "阿荇自行捏碎保命符，撑住了这一击。";
            }
            else
            {
                lastRescueRound = Round + 2;
                Message = "阿荇重伤倒地。两回合内可在相邻格带他撤走。";
            }
            return;
        }
        if (InField && target == units[0])
        {
            Status = SparStatus.Lost;
            ResolveFieldExit(false);
            Message = Party == FieldParty.Brigands ? "你未能脱离杀阵，倒在了松风隘。" :
                "你负伤退回官道，兽蜕遗落在林中。" + CompanionResult();
            return;
        }
        Message += target.Name + "退场。";
        foreach (var unit in units)
            if (unit.Friendly == target.Friendly && unit.Alive) return;
        Status = target.Friendly ? SparStatus.Lost : SparStatus.Won;
        if (InField)
        {
            if (RescueRounds > 0) CompanionCarried = true;
            Message = Party == FieldParty.Brigands ? "灰衣客与同伙败走，隘口重新有了通路。" :
                "青棘獾退入深林，你收到了兽蜕。" + CompanionResult();
        }
        else Message = target.Friendly ? "我方气力用尽，双方收势行礼。" : "对方收势认输，切磋取胜。";
    }

    public void EndTurn()
    {
        if (Status != SparStatus.PlayerTurn) return;
        if (RescueRounds == 1)
        {
            CompanionDied = true;
            paths.Clear();
        }
        if (Party == FieldParty.Ally && Companion.Alive)
        {
            PlayActor(Companion);
            if (Status != SparStatus.PlayerTurn) return;
        }
        Status = SparStatus.OpponentTurn;
        nextOpponentIndex = 0;
        RefreshActions(false);
        Message = Party == FieldParty.Brigands ? "劫修逼近。" : InField ? "妖兽正在行动。" : "轮到对方出招。";
    }

    public void StepOpponent()
    {
        if (Status != SparStatus.OpponentTurn) return;
        var opponent = NextOpponent();
        if (opponent != null)
        {
            if (Party == FieldParty.Brigands && opponent == units[2] && stunned)
            {
                stunned = false;
                Message = "阵纹逆转，灰衣客这回合只能稳住气息。";
            }
            else PlayActor(opponent);
            nextOpponentIndex++;
        }
        if (Status != SparStatus.OpponentTurn || NextOpponent() != null) return;
        Round++;
        Status = SparStatus.PlayerTurn;
        RefreshActions(true);
    }

    SparUnit NextOpponent()
    {
        while (nextOpponentIndex < units.Length)
        {
            var unit = units[nextOpponentIndex];
            if (!unit.Friendly && unit.Alive) return unit;
            nextOpponentIndex++;
        }
        return null;
    }

    public void Yield()
    {
        if (Status != SparStatus.PlayerTurn && Status != SparStatus.OpponentTurn) return;
        Status = SparStatus.Yielded;
        if (InField)
        {
            ResolveFieldExit(CanCarryNow);
            Message = Party == FieldParty.Brigands ? "你从西坡甩开追兵。" : "你放弃兽蜕，撤回官道。" + CompanionResult();
        }
        else Message = "你拱手认输，双方收势行礼。";
    }

    public bool CanCarryNow => RescueRounds > 0 && (CanLift(units[0]) || CanLift(units[1]));

    bool CanLift(SparUnit unit) => unit.Alive && Distance(unit.Cell, Companion.Cell) == 1 &&
        (canTraverse == null || canTraverse(unit.Cell, Companion.Cell));

    void ResolveFieldExit(bool carry)
    {
        if (RescueRounds <= 0) return;
        CompanionCarried = carry;
        CompanionDied = !carry;
    }

    public string CompanionResult()
    {
        if (Companion == null) return "";
        if (CompanionDied) return "阿荇已身亡。";
        if (CompanionCarried) return "阿荇已获救。";
        return "阿荇平安同行。";
    }

    void RefreshActions(bool friendly)
    {
        foreach (var unit in units)
        {
            if (unit.Friendly != friendly) continue;
            unit.Moved = false;
            unit.Acted = false;
        }
    }

    // Choose the closest legal firing position. Downed units no longer obstruct the board.
    void PlayActor(SparUnit opponent)
    {
        var map = PathsFrom(opponent);
        var firingCell = opponent.Cell;
        int bestCost = int.MaxValue;
        SparUnit target = null;
        foreach (var candidate in units)
        {
            if (candidate.Friendly == opponent.Friendly || !candidate.Alive) continue;
            foreach (var cell in map.Reachable)
            {
                if (!CanHitFrom(cell, candidate, opponent.Range)) continue;
                int cost = map.Cost(cell);
                if (cost > bestCost || (cost == bestCost && target != null && target.Hp <= candidate.Hp))
                    continue;
                bestCost = cost;
                firingCell = cell;
                target = candidate;
            }
        }

        if (target == null)
        {
            Message = opponent.Name + "守在原地。";
            return;
        }
        // Trace the route back to the last square within this turn's walking allowance.
        while (map.Cost(firingCell) > opponent.Steps)
            firingCell = map.Previous[firingCell.x, firingCell.y];
        if (firingCell != opponent.Cell) Relocate(opponent, firingCell);
        if (CanHitFrom(opponent.Cell, target, opponent.Range)) Cast(opponent, target);
        else Message = opponent.Name + "向前移步。";
    }

    static int Distance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    sealed class PathMap
    {
        readonly int[,] depth;
        public readonly Vector2Int[,] Previous;
        public readonly List<Vector2Int> Reachable;

        public PathMap(int columns, int rows)
        {
            depth = new int[columns, rows];
            Previous = new Vector2Int[columns, rows];
            Reachable = new List<Vector2Int>(columns * rows);
        }

        // Store cost + 1, keeping an unvisited cell at zero.
        public int Cost(Vector2Int cell) =>
            depth[cell.x, cell.y] == 0 ? int.MaxValue : depth[cell.x, cell.y] - 1;

        public void Visit(Vector2Int cell, Vector2Int from, int cost)
        {
            depth[cell.x, cell.y] = cost + 1;
            Previous[cell.x, cell.y] = from;
            Reachable.Add(cell);
        }
    }

    PathMap PathsFrom(SparUnit mover)
    {
        if (paths.TryGetValue(mover, out var map)) return map;
        map = new PathMap(Columns, Rows);
        map.Visit(mover.Cell, mover.Cell, 0);
        for (int i = 0; i < map.Reachable.Count; i++)
        {
            var cell = map.Reachable[i];
            int nextCost = map.Cost(cell) + 1;
            foreach (var direction in directions)
            {
                var next = cell + direction;
                if (IsBlocked(next) || map.Cost(next) != int.MaxValue || At(next) != null) continue;
                if (canTraverse != null && !canTraverse(cell, next)) continue;
                map.Visit(next, cell, nextCost);
            }
        }
        paths[mover] = map;
        return map;
    }
}
