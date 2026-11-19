using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct SparBindStatus
{
    public int MovePenalty { get; }
    public int RemainingTurns => MovePenalty > 0 ? 1 : 0;
    public bool AppliesNow { get; }

    internal SparBindStatus(int movePenalty, bool appliesNow)
    {
        MovePenalty = movePenalty;
        AppliesNow = appliesNow;
    }
}

public sealed partial class SparMatch
{
    // TaiXu's order differs from the five pool slots in saved breakthrough attempts.
    static readonly SpiritElement[] rootOrder =
        { SpiritElement.Metal, SpiritElement.Wood, SpiritElement.Water, SpiritElement.Fire, SpiritElement.Earth };

    sealed class CombatArts
    {
        public readonly SpiritRoots Roots;
        public readonly CultivationManualDefinition Manual;
        public readonly IReadOnlyList<QingxiSpellDefinition> Spells;
        public readonly System.Random Random;
        public readonly int[] Reserves = new int[rootOrder.Length];
        public readonly int[] Intake = new int[rootOrder.Length];
        public readonly Dictionary<QingxiSpellDefinition, int> NextCast = new Dictionary<QingxiSpellDefinition, int>();

        public CombatArts(SpiritRoots roots, CultivationManualDefinition manual, List<QingxiSpellDefinition> spells, int seed)
        {
            Roots = roots;
            Manual = manual;
            Spells = spells.AsReadOnly();
            Random = new System.Random(seed);
        }
    }

    readonly Dictionary<SparUnit, CombatArts> arts = new Dictionary<SparUnit, CombatArts>();
    readonly Dictionary<SparUnit, int> binds = new Dictionary<SparUnit, int>();
    QingxiSpellDefinition relicSpell;
    QingxiSpellDefinition selectedSpell;
    WuXing paymentElement;

    public IReadOnlyList<QingxiSpellDefinition> PlayerSpells => PreparedSpells(units[0]);
    public QingxiSpellDefinition PlayerSpell => selectedSpell ?? (PlayerSpells.Count == 0 ? null : PlayerSpells[0]);
    public bool SpellSelected => selectedSpell != null;
    public int NextSpellTurn => PlayerSpell != null && arts[units[0]].NextCast.TryGetValue(PlayerSpell, out int next) ? next : 0;
    public bool HasDaoCang => arts.ContainsKey(units[0]);
    public int DaoCang(WuXing element) => DaoCang(units[0], element);
    public int Intake(WuXing element) => Intake(units[0], element);
    public int DaoCang(SparUnit unit, WuXing element) =>
        unit != null && arts.TryGetValue(unit, out var loadout) ? loadout.Reserves[(int)element] : 0;
    public int Intake(SparUnit unit, WuXing element) =>
        unit != null && arts.TryGetValue(unit, out var loadout) ? loadout.Intake[(int)element] : 0;
    public IReadOnlyList<QingxiSpellDefinition> PreparedSpells(SparUnit unit) =>
        unit != null && arts.TryGetValue(unit, out var loadout) ? loadout.Spells : Array.Empty<QingxiSpellDefinition>();

    public SparBindStatus BindStatusFor(SparUnit unit)
    {
        if (unit == null || !unit.Alive || !BattleRunning)
            return default;

        // A spent penalty is reset when the unit next starts a turn; only the current actor can still be using it.
        if (unit == ActiveUnit && unit.MovementPenalty > 0)
            return new SparBindStatus(Math.Min(unit.MaxActionPoints, unit.MovementPenalty), true);
        return binds.TryGetValue(unit, out int penalty)
            ? new SparBindStatus(Math.Min(unit.MaxActionPoints, penalty), false) : default;
    }

    public bool IsRelicSpell(QingxiSpellDefinition spell) => spell != null && spell == relicSpell;

    public void ConfigurePlayerLoadout(SpiritRoots roots, CultivationManualDefinition manual,
        IReadOnlyList<QingxiSpellDefinition> spells, int seed, QingxiSpellDefinition equippedArt = null)
    {
        if (!equippedArt)
        {
            ConfigureUnitLoadout(units[0], roots, manual, spells, seed);
            return;
        }
        if (!equippedArt.IsBattleSpell || equippedArt.FaShuType != FaShuType.法宝)
            throw new ArgumentException("随身法宝须配置可用于斗法的法宝术。", nameof(equippedArt));
        var available = new List<QingxiSpellDefinition>();
        if (spells != null) available.AddRange(spells);
        available.Add(equippedArt);
        ConfigureUnitLoadout(units[0], roots, manual, available, seed);
        relicSpell = equippedArt;
    }

    public void ConfigureUnitLoadout(SparUnit unit, SpiritRoots roots, CultivationManualDefinition manual,
        IReadOnlyList<QingxiSpellDefinition> spells, int seed)
    {
        if (Array.IndexOf(units, unit) < 0) throw new ArgumentException("棋手须在本场参战。", nameof(unit));
        if (arts.ContainsKey(unit) || TurnNumber != 1 || ActiveUnit.Moved || ActiveUnit.Acted)
            throw new InvalidOperationException("请在交手前确定灵根、主修功法与战斗术法。");
        if (roots == null || roots.TotalWeight <= 0) throw new ArgumentException("请指定有效的五行灵根。", nameof(roots));
        if (manual != null && manual.GongFaType != GongFaType.Main) throw new ArgumentException("此功法不能主修。", nameof(manual));
        var prepared = new List<QingxiSpellDefinition>();
        if (spells != null)
            foreach (var spell in spells)
            {
                if (spell == null || !spell.IsBattleSpell || prepared.Contains(spell)) continue;
                if (!spell.HasBattleLimit) throw new ArgumentException($"{spell.Name}须设战斗消耗或冷却。", nameof(spells));
                prepared.Add(spell);
            }
        var loadout = new CombatArts(roots, manual, prepared, seed);
        arts.Add(unit, loadout);
        // The constructor already selected the first actor. Other units draw on their own turns.
        if (ActiveUnit == unit) RefillDaoCang(loadout);
    }

    // Existing isolated encounter simulations can equip a fire-trained caster.
    public void PreparePlayerSpell(QingxiSpellDefinition spell)
    {
        if (spell == null || !spell.IsBattleSpell) return;
        ConfigurePlayerLoadout(SpiritRoots.For(false), QingxiArts.EmberManual, new[] { spell }, 31);
    }

    void OnArtsTurnStarted(SparUnit actor)
    {
        binds.TryGetValue(actor, out int boundSteps);
        actor.MovementPenalty = boundSteps;
        if (boundSteps > 0) binds.Remove(actor);
        if (arts.TryGetValue(actor, out var loadout)) RefillDaoCang(loadout);
    }

    static void RefillDaoCang(CombatArts loadout)
    {
        Array.Clear(loadout.Intake, 0, loadout.Intake.Length);
        var manual = loadout.Manual;
        int draws = 4 + (manual == null ? 0 : manual.ExtraRootDraws);
        for (int draw = 0; draw < draws; draw++)
        {
            int roll = loadout.Random.Next(loadout.Roots.TotalWeight);
            for (int i = 0; i < rootOrder.Length; i++)
            {
                roll -= loadout.Roots.Weight(rootOrder[i]);
                if (roll >= 0) continue;
                loadout.Intake[i]++;
                break;
            }
        }
        if (manual != null) loadout.Intake[(int)manual.BonusElement] += manual.BonusAmount;
        Array.Copy(loadout.Intake, loadout.Reserves, loadout.Intake.Length);
    }

    // A valid payment covers the fixed cost and the chosen same-element group together.
    static bool CanPay(CombatArts loadout, QingxiSpellDefinition spell, WuXing sameElement)
    {
        for (int i = 0; i < loadout.Reserves.Length; i++)
            if (loadout.Reserves[i] < PaymentAmount(spell, (WuXing)i, sameElement)) return false;
        return true;
    }

    static int PaymentAmount(QingxiSpellDefinition spell, WuXing element, WuXing sameElement) =>
        spell.FixedCost(element) + (element == sameElement ? spell.SameCost : 0);

    static bool FindPayment(CombatArts loadout, QingxiSpellDefinition spell, int start, int options, out WuXing element)
    {
        for (int i = 0; i < options; i++)
        {
            element = (WuXing)((start + i) % loadout.Reserves.Length);
            if (CanPay(loadout, spell, element)) return true;
        }
        element = default;
        return false;
    }

    static bool KnowsSpell(CombatArts loadout, QingxiSpellDefinition spell)
    {
        foreach (var known in loadout.Spells)
            if (known == spell) return true;
        return false;
    }

    bool CanAct(SparUnit unit) => unit == ActiveUnit && ActorReady;

    bool HasSpellResources(SparUnit unit, QingxiSpellDefinition spell, out CombatArts loadout) =>
        arts.TryGetValue(unit, out loadout) && KnowsSpell(loadout, spell) &&
        unit.ActionPoints >= spell.ActionPointCost && unit.Mana >= spell.ManaCost &&
        !IsCoolingDown(loadout, unit, spell, out _);

    bool TryChooseSpellPayment(SparUnit unit, QingxiSpellDefinition spell, out WuXing payment)
    {
        payment = default;
        return CanAct(unit) && HasSpellResources(unit, spell, out var loadout) &&
            FindPayment(loadout, spell, 0, loadout.Reserves.Length, out payment);
    }

    bool CanCastFrom(SparUnit unit, Vector2Int from, SparUnit target, QingxiSpellDefinition spell, WuXing payment) =>
        target != null && target.Alive && (spell.Target == FaShuTarget.Self ? target == unit :
            target.Friendly != unit.Friendly && CanHitFrom(from, target, spell.Range)) &&
        HasSpellResources(unit, spell, out var loadout) && CanPay(loadout, spell, payment);

    bool TryCastDuringTurn(SparUnit unit, SparUnit target, QingxiSpellDefinition spell, WuXing payment)
    {
        if (!CanAct(unit) || !CanCastFrom(unit, unit.Cell, target, spell, payment)) return false;
        var loadout = arts[unit];
        for (int i = 0; i < loadout.Reserves.Length; i++)
            loadout.Reserves[i] -= PaymentAmount(spell, (WuXing)i, payment);
        unit.Mana -= spell.ManaCost;
        if (spell.CooldownTurns > 0) loadout.NextCast[spell] = unit.TurnsStarted + spell.CooldownTurns + 1;
        if (spell.Effect == QingxiSpellEffect.Ward) CastWard(unit, spell, payment);
        else Cast(unit, target, spell: spell, actionCost: spell.ActionPointCost, payment: payment,
            actionName: IsRelicSpell(spell) ? "法宝 · " + spell.Name : null);
        return true;
    }

    public string SpellUnavailableReason(SparUnit unit, QingxiSpellDefinition spell)
    {
        if (spell == null || !CanAct(unit)) return "须在本人行动时施术";
        if (!arts.TryGetValue(unit, out var loadout) || !KnowsSpell(loadout, spell)) return "尚未备入此术";
        if (unit.ActionPoints < spell.ActionPointCost) return $"需要 {spell.ActionPointCost} 点行动力";
        if (unit.Mana < spell.ManaCost) return $"需要 {spell.ManaCost} 点法力";
        if (IsCoolingDown(loadout, unit, spell, out int next))
            return $"还需 {next - unit.TurnsStarted} 次本人行动";
        if (FindPayment(loadout, spell, 0, loadout.Reserves.Length, out _)) return "";
        for (int i = 0; i < loadout.Reserves.Length; i++)
        {
            var element = (WuXing)i;
            int missing = spell.FixedCost(element) - loadout.Reserves[i];
            if (missing > 0) return $"缺{element}{missing}";
        }
        return $"尚缺一组同类{spell.SameCost}";
    }

    public bool CanSelectSpell(SparUnit unit)
    {
        foreach (var spell in PreparedSpells(unit))
            if (TryChooseSpellPayment(unit, spell, out _)) return true;
        return false;
    }

    public bool SelectPlayerSpell(SparUnit unit, QingxiSpellDefinition spell)
    {
        if (unit != units[0] || !CanCommand(unit)) return false;
        WuXing payment = default;
        if (spell == null ? unit.ActionPoints < StrikeCost : !TryChooseSpellPayment(unit, spell, out payment)) return false;
        paymentElement = payment;
        selectedSpell = spell;
        return true;
    }

    public bool TogglePlayerSpell(SparUnit unit)
    {
        bool pastCurrent = selectedSpell == null;
        foreach (var spell in PlayerSpells)
        {
            if (pastCurrent && SelectPlayerSpell(unit, spell)) return true;
            if (spell == selectedSpell) pastCurrent = true;
        }
        return selectedSpell != null && SelectPlayerSpell(unit, null);
    }

    public bool HasPaymentChoice => selectedSpell != null && selectedSpell.SameCost > 0 &&
        HasSpellResources(units[0], selectedSpell, out var loadout) &&
        FindPayment(loadout, selectedSpell, (int)paymentElement + 1, loadout.Reserves.Length - 1, out _);

    public bool CyclePayment(SparUnit unit)
    {
        if (!CanCommand(unit) || unit != units[0] || !HasPaymentChoice) return false;
        var loadout = arts[unit];
        return FindPayment(loadout, selectedSpell, (int)paymentElement + 1, loadout.Reserves.Length - 1, out paymentElement);
    }

    public string CastCostText
    {
        get
        {
            if (selectedSpell == null) return "常击不耗道藏";
            string text = selectedSpell.CostText.Replace("同类", paymentElement.ToString());
            if (selectedSpell.Effect == QingxiSpellEffect.Burn)
                return text + $" · 灼烧 {BurnAmount(units[0], selectedSpell)} 点 × {selectedSpell.EffectTurns} 次";
            if (selectedSpell.Effect == QingxiSpellEffect.Ward)
            {
                string relationNote = selectedSpell.PaymentRelation(paymentElement) > 0 ? "相生，" : "";
                return text + $" · {relationNote}每击抵 {WardAmount(selectedSpell, paymentElement)} 点，余 {selectedSpell.EffectTurns} 次";
            }
            if (selectedSpell.Effect != QingxiSpellEffect.Harmonize) return text;
            int relation = selectedSpell.PaymentRelation(paymentElement);
            if (relation > 0)
            {
                var caster = units[0];
                int heal = Math.Min(caster.MaxHp - caster.Hp, selectedSpell.EffectAmount);
                return text + (heal == 0 ? " · 相生，气血已满" : $" · 相生，回气血{heal}");
            }
            if (relation < 0) return text + $" · 相克，增伤{selectedSpell.EffectAmount}";
            return text + " · 同行，无附效";
        }
    }

    static bool IsCoolingDown(CombatArts loadout, SparUnit caster, QingxiSpellDefinition spell, out int nextTurn) =>
        loadout.NextCast.TryGetValue(spell, out nextTurn) && caster.TurnsStarted < nextTurn;

    bool TargetHasPendingBind(SparUnit target) => binds.ContainsKey(target);

    void ApplyArtEffect(SparUnit caster, SparUnit target, QingxiSpellDefinition spell, WuXing payment)
    {
        if (spell == null) return;
        if (spell.Effect == QingxiSpellEffect.Bind && target.Alive)
        {
            binds[target] = spell.EffectAmount;
            statusEvents.Add(new SparStatusEvent(SparStatusEventKind.BindApplied, target,
                Math.Min(target.MaxActionPoints, spell.EffectAmount), 1));
            Message += $"青藤缠身，下次行动少走 {spell.EffectAmount} 格。";
        }
        if (spell.Effect == QingxiSpellEffect.Burn && target.Alive) ApplyBurn(caster, target, spell);
        if (spell.Effect != QingxiSpellEffect.Harmonize) return;
        int relation = spell.PaymentRelation(payment);
        if (relation < 0) Message += $"五行相克，此击术威增加 {spell.EffectAmount}。";
        if (relation > 0)
        {
            int heal = Heal(caster, spell.EffectAmount, spell);
            Message += $"五行相生，{caster.Name}恢复 {heal} 点气血。";
        }
    }
}
