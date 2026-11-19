using System;
using System.Collections.Generic;
using UnityEngine;

public enum SparStatus { PlayerTurn, OpponentTurn, Won, Lost, Yielded }
public enum FieldParty { Practice, Solo, Ally, Contract, Brigands }

public readonly struct SparHealthChange
{
    public SparUnit Target { get; }
    public int Amount { get; }
    public QingxiSpellDefinition Spell { get; }

    internal SparHealthChange(SparUnit target, int amount, QingxiSpellDefinition spell)
    {
        Target = target;
        Amount = amount;
        Spell = spell;
    }
}

public sealed class SparUnit
{
    public string Name { get; }
    public bool Friendly { get; }
    public bool Direct { get; }
    public Vector2Int Cell { get; internal set; }
    public IReadOnlyList<Vector2Int> WalkPath { get; internal set; }
    public int Hp { get; internal set; }
    public int MaxHp { get; }
    public float Mana { get; internal set; }
    public int MaxMana { get; }
    public int Range { get; }
    public int Power { get; }
    public int Steps { get; }
    public int Reaction { get; }
    public int TurnsStarted { get; internal set; }
    public int MaxActionPoints => Steps + 3;
    public int ActionPoints { get; internal set; }
    internal int StepsTaken { get; set; }
    internal int MovementPenalty { get; set; }
    public int MovementLeft => Math.Min(ActionPoints, Math.Max(0, MaxActionPoints - MovementPenalty - StepsTaken));
    public bool Moved => StepsTaken > 0;
    public bool Acted { get; internal set; }
    public bool Alive => Hp > 0;

    internal SparUnit(string name, bool friendly, Vector2Int cell, int hp, int range, int power, int steps,
        bool direct = true, int reaction = 10, int mana = 12, float? startingMana = null)
    {
        Name = name;
        Friendly = friendly;
        Direct = friendly && direct;
        Cell = cell;
        WalkPath = Array.AsReadOnly(new[] { cell });
        MaxHp = Hp = hp;
        MaxMana = mana;
        Mana = Mathf.Clamp(startingMana ?? mana, 0f, mana);
        Range = range;
        Power = power;
        Steps = steps;
        ActionPoints = MaxActionPoints;
        if (reaction < 1) throw new ArgumentOutOfRangeException(nameof(reaction));
        Reaction = reaction;
    }
}

public sealed partial class SparMatch
{
    public const int Width = 8;
    public const int Height = 6;
    public const int StrikeCost = 2;
    public const int ItemCost = 2;

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
    readonly List<SparHealthChange> healthChanges = new List<SparHealthChange>(2);

    public int Columns => terrain.GetLength(0);
    public int Rows => terrain.GetLength(1);
    public IReadOnlyList<SparUnit> Units { get; }
    public IReadOnlyList<SparHealthChange> LastHealthChanges { get; }
    public SparStatus Status { get; private set; } = SparStatus.PlayerTurn;
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
    int rescueTurns;
    public int RescueRounds => CompanionDied || CompanionCarried ? 0 : rescueTurns;

    public SparMatch() : this(ExampleTerrain(), exampleDeployment) { }

    // The exploration controller supplies collision samples and four legal starting cells.
    // Snapshot the samples so world updates cannot invalidate a turn's cached paths.
    public SparMatch(bool[,] blocked, IReadOnlyList<Vector2Int> deployment,
        Func<Vector2Int, Vector2Int, bool> canTraverse = null,
        Func<Vector2Int, Vector2Int, bool> clearShot = null, FieldParty party = FieldParty.Practice,
        string name = "药铺学徒", int cultivationBonus = 0, bool preparedWard = false, bool studiedWard = false,
        CultivatorAttributes? heroAttributes = null, float? heroMana = null,
        int practiceGearAttack = 0, int practiceGearHealth = 0)
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
        int fieldMana = heroAttributes?.ManaCapacity ?? 12;
        if (party == FieldParty.Practice)
        {
            units = new[]
            {
                new SparUnit("药铺学徒", true, deployment[0], 12 + practiceGearHealth, 3, 4 + practiceGearAttack, 3, reaction: 12),
                new SparUnit("练习傀儡", true, deployment[1], 14, 1, 3, 3, reaction: 9),
                new SparUnit("巡山弟子", false, deployment[2], 9, 2, 3, 2, reaction: 11),
                new SparUnit("护阵弟子", false, deployment[3], 11, 1, 4, 3, reaction: 10)
            };
        }
        else if (party == FieldParty.Brigands)
        {
            int rank = Mathf.Clamp(cultivationBonus, 0, 4);
            units = new[]
            {
                new SparUnit(name, true, deployment[0], heroAttributes?.MaxHealth ?? 12 + 3 * rank, 3,
                    heroAttributes?.Attack ?? 3 + rank, heroAttributes?.Movement ?? 3,
                    reaction: heroAttributes?.Reaction ?? 13, mana: fieldMana, startingMana: heroMana),
                new SparUnit("护行木傀", true, deployment[1], 13, 1, 3, 3, reaction: 9),
                new SparUnit("灰衣客 · 筑基初期", false, deployment[2], 31, 3, 8, 3, reaction: 16),
                new SparUnit("望风同伙", false, deployment[3], 8, 2, 3, 2, reaction: 11)
            };
            Message = "灰衣客祭起护身阵。你可随时弃财脱身。";
        }
        else
        {
            int bonus = Mathf.Clamp(cultivationBonus, 0, 3);
            var partyUnits = new List<SparUnit>
            {
                new SparUnit(name, true, deployment[0], heroAttributes?.MaxHealth ?? 12 + bonus * 2, 3,
                    heroAttributes?.Attack ?? 4 + bonus, heroAttributes?.Movement ?? 3,
                    reaction: heroAttributes?.Reaction ?? 13, mana: fieldMana, startingMana: heroMana),
                new SparUnit("护行木傀", true, deployment[1], 13, 1, 3, 3, reaction: 9),
                new SparUnit("青棘獾", false, deployment[2], 23, 2, 5, 3, reaction: 10)
            };
            if (party != FieldParty.Solo)
                partyUnits.Add(new SparUnit("采药客阿荇", true, deployment[3], 7, 2, 3, 3, party == FieldParty.Contract, reaction: 11));
            units = partyUnits.ToArray();
            Message = "青棘獾逼近。可驱退妖兽收取兽蜕，也可放弃兽蜕撤走。";
        }
        Units = Array.AsReadOnly(units);
        LastHealthChanges = healthChanges.AsReadOnly();
        LastStatusEvents = statusEvents.AsReadOnly();
        Journal = journal.AsReadOnly();
        AdvanceInitiative();
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
        Status == SparStatus.PlayerTurn && ActorReady && unit == ActiveUnit && unit.Direct;

    public bool CanMove(SparUnit unit, Vector2Int cell)
    {
        if (!CanCommand(unit) || unit.MovementLeft <= 0 || IsBlocked(cell) || At(cell) != null) return false;
        return PathsFrom(unit).Cost(cell) <= unit.MovementLeft;
    }

    public IReadOnlyList<Vector2Int> PreviewPath(SparUnit unit, Vector2Int cell) =>
        CanMove(unit, cell) ? BuildPath(unit, cell) : Array.Empty<Vector2Int>();

    internal IEnumerable<Vector2Int> MovesInRange(SparUnit unit)
    {
        if (!CanCommand(unit) || unit.MovementLeft <= 0) yield break;
        var paths = PathsFrom(unit);
        // The breadth-first queue is already sorted by the walking cost.
        for (int i = 1; i < paths.Reachable.Count; i++)
        {
            var cell = paths.Reachable[i];
            if (paths.Cost(cell) > unit.MovementLeft) yield break;
            yield return cell;
        }
    }

    public bool HasLineOfSight(Vector2Int a, Vector2Int b) => clearShot == null || clearShot(a, b);

    QingxiSpellDefinition SelectedSpellFor(SparUnit unit) => unit == units[0] ? selectedSpell : null;

    public int AttackRange(SparUnit unit) => SelectedSpellFor(unit)?.Range ?? unit.Range;
    public int CurrentAttackActionCost(SparUnit unit) => SelectedSpellFor(unit)?.ActionPointCost ?? StrikeCost;

    bool CanHitFrom(Vector2Int from, SparUnit target, int range) =>
        Distance(from, target.Cell) <= range && HasLineOfSight(from, target.Cell);

    bool CanStrikeFrom(SparUnit unit, Vector2Int from, SparUnit target) =>
        unit.ActionPoints >= StrikeCost && target != null && target.Alive && target.Friendly != unit.Friendly &&
        CanHitFrom(from, target, unit.Range);

    public bool CanAttack(SparUnit unit, Vector2Int cell)
    {
        if (!CanCommand(unit)) return false;
        var target = At(cell);
        var spell = SelectedSpellFor(unit);
        return spell == null ? CanStrikeFrom(unit, unit.Cell, target) : CanCastFrom(unit, unit.Cell, target, spell, paymentElement);
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
        if (!CanCommand(unit)) return false;
        var target = At(cell);
        var spell = SelectedSpellFor(unit);
        if (spell != null)
        {
            if (!TryCastDuringTurn(unit, target, spell, paymentElement)) return false;
        }
        else
        {
            if (!CanStrikeFrom(unit, unit.Cell, target)) return false;
            Cast(unit, target);
        }
        selectedSpell = null;
        return true;
    }

    public bool CanUseAgreedHerb(SparUnit unit) => Party == FieldParty.Contract && unit == Companion && CanCommand(unit) &&
        unit.ActionPoints >= ItemCost && !BandageUsed && unit.Hp < unit.MaxHp;

    public bool UseAgreedHerb(SparUnit unit)
    {
        if (!CanUseAgreedHerb(unit)) return false;
        ClearActionResults();
        Heal(unit, 4);
        unit.ActionPoints -= ItemCost;
        unit.Acted = true;
        BandageUsed = true;
        Message = "依约用了止血散，阿荇气血恢复。";
        RecordJournalAction("止血散", unit);
        return true;
    }

    public bool CanUseFieldWard(SparUnit unit) => Party == FieldParty.Brigands && WardReady && unit == units[0] &&
        CanCommand(unit) && unit.ActionPoints >= ItemCost && units[2].Alive;

    public bool UseFieldWard(SparUnit unit) => !HasCarriedSlots && ApplyFieldWard(unit);

    bool ApplyFieldWard(SparUnit unit)
    {
        if (!CanUseFieldWard(unit)) return false;
        WardReady = false;
        WardReleased = true;
        selectedSpell = null;
        FormationBroken = WardStudied;
        stunned = WardStudied;
        Cast(unit, units[2], WardStudied ? 13 : 6, actionCost: ItemCost, actionName: "破阵符");
        if (Status == SparStatus.PlayerTurn)
            Message = WardStudied ? "阵符击中右袖的阵眼，护身阵破去，灰衣客下一次出手受阻。" :
                "阵符惊动护阵，灰衣客损失六点气血，阵眼仍在。";
        return true;
    }

    IReadOnlyList<Vector2Int> BuildPath(SparUnit unit, Vector2Int cell)
    {
        var map = PathsFrom(unit);
        var route = new Vector2Int[map.Cost(cell) + 1];
        route[route.Length - 1] = cell;
        for (int i = route.Length - 1; i > 0; i--)
        {
            Vector2Int step = route[i];
            route[i - 1] = map.Previous[step.x, step.y];
        }
        return Array.AsReadOnly(route);
    }

    void Relocate(SparUnit unit, Vector2Int cell)
    {
        ClearActionResults();
        unit.WalkPath = BuildPath(unit, cell);
        int steps = unit.WalkPath.Count - 1;
        unit.ActionPoints -= steps;
        unit.StepsTaken += steps;
        unit.Cell = cell;
        paths.Clear();
        RecordJournalMove(steps);
    }

    public int PreviewAttackDamage(SparUnit unit, Vector2Int cell)
    {
        if (!CanAttack(unit, cell)) return 0;
        var spell = SelectedSpellFor(unit);
        return spell != null && spell.Target == FaShuTarget.Self ? 0 : HitPoints(unit, At(cell), spell, paymentElement);
    }

    public int PreviewAttackBlock(SparUnit unit, Vector2Int cell)
    {
        if (!CanAttack(unit, cell)) return 0;
        var spell = SelectedSpellFor(unit);
        if (spell != null && spell.Target == FaShuTarget.Self) return 0;
        var target = At(cell);
        return Math.Min(AttackPower(unit, target, 0, spell, paymentElement), WardStatusFor(target).Amount);
    }

    int AttackPower(SparUnit caster, SparUnit target, int fixedPower, QingxiSpellDefinition spell, WuXing payment)
    {
        bool protectedBoss = Party == FieldParty.Brigands && target == units[2] && !FormationBroken;
        int bonus = spell == null ? 0 : spell.ExtraPower;
        if (spell != null && spell.Effect == QingxiSpellEffect.Harmonize && spell.PaymentRelation(payment) < 0)
            bonus += spell.EffectAmount;
        return fixedPower > 0 ? fixedPower : Math.Max(1, caster.Power + bonus - (protectedBoss ? 3 : 0));
    }

    int HitPoints(SparUnit caster, SparUnit target, QingxiSpellDefinition spell, WuXing payment)
    {
        int power = AttackPower(caster, target, 0, spell, payment);
        return Math.Min(target.Hp, Math.Max(0, power - WardStatusFor(target).Amount));
    }

    void Cast(SparUnit caster, SparUnit target, int fixedPower = 0, QingxiSpellDefinition spell = null,
        int actionCost = StrikeCost, WuXing payment = default, string actionName = null)
    {
        ClearActionResults();
        int power = AttackPower(caster, target, fixedPower, spell, payment);
        int blocked = AbsorbHit(target, power);
        int hit = Math.Min(target.Hp, power - blocked);
        if (hit > 0)
        {
            target.Hp -= hit;
            healthChanges.Add(new SparHealthChange(target, -hit, spell));
        }
        caster.ActionPoints -= actionCost;
        caster.Acted = true;
        Message = caster.Name + (spell != null ? "以" + spell.Name : "") + "命中" + target.Name + "。";
        if (blocked > 0) Message += $"护体挡下 {blocked} 点伤害，";
        Message += hit > 0 ? $"气血减少 {hit}。" : "气血无损。";
        ApplyArtEffect(caster, target, spell, payment);
        if (!target.Alive) ResolveDefeat(target);
        RecordJournalAction(actionName ?? spell?.Name ?? "常击", target);
    }

    void ResolveDefeat(SparUnit target)
    {
        paths.Clear();
        forecastsDirty = true;
        if (target == Companion)
        {
            if (!TalismanUsed)
            {
                TalismanUsed = true;
                Heal(target, 3);
                Message = "阿荇自行捏碎保命符，撑住了这一击。";
            }
            else
            {
                rescueTurns = 2;
                Message = "阿荇重伤倒地。你还有两次本人行动可以救他。";
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
        else Message = target.Friendly ? "我方气血用尽，双方收势行礼。" : "对方收势认输，切磋取胜。";
    }

    int Heal(SparUnit target, int power, QingxiSpellDefinition spell = null)
    {
        int heal = Math.Min(target.MaxHp - target.Hp, power);
        if (heal > 0)
        {
            target.Hp += heal;
            healthChanges.Add(new SparHealthChange(target, heal, spell));
        }
        return heal;
    }

    public void EndTurn()
    {
        if (Status != SparStatus.PlayerTurn || !ActorReady) return;
        selectedSpell = null;
        if (ActiveUnit == units[0] && RescueRounds > 0 && --rescueTurns == 0)
        {
            CompanionDied = true;
            paths.Clear();
            Message = "你收势时已错过最后的救援机会，阿荇伤重身亡。";
        }
        AdvanceInitiative();
    }

    public void Yield()
    {
        if (Status != SparStatus.PlayerTurn && Status != SparStatus.OpponentTurn) return;
        OpeningPending = false;
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
