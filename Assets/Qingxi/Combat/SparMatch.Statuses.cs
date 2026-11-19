using System;
using System.Collections.Generic;

public readonly struct SparStatusCounter
{
    public int Amount { get; }
    public int Remaining { get; }

    internal SparStatusCounter(int amount, int remaining)
    {
        Amount = amount;
        Remaining = remaining;
    }
}

public enum SparStatusEventKind { BindApplied, BurnApplied, BurnTick, WardApplied, WardBlock }

public readonly struct SparStatusEvent
{
    public SparStatusEventKind Kind { get; }
    public SparUnit Target { get; }
    public int Amount { get; }
    public int Remaining { get; }

    internal SparStatusEvent(SparStatusEventKind kind, SparUnit target, int amount, int remaining)
    {
        Kind = kind;
        Target = target;
        Amount = amount;
        Remaining = remaining;
    }
}

public sealed partial class SparMatch
{
    readonly struct Burning
    {
        public readonly SparStatusCounter Effect;
        public readonly QingxiSpellDefinition Spell;

        public Burning(int power, int ticks, QingxiSpellDefinition spell)
        {
            Effect = new SparStatusCounter(power, ticks);
            Spell = spell;
        }
    }

    readonly Dictionary<SparUnit, Burning> burns = new Dictionary<SparUnit, Burning>();
    readonly Dictionary<SparUnit, SparStatusCounter> wards = new Dictionary<SparUnit, SparStatusCounter>();
    readonly List<SparStatusEvent> statusEvents = new List<SparStatusEvent>(2);

    public IReadOnlyList<SparStatusEvent> LastStatusEvents { get; }

    // The visible battle pauses here while the incoming fighter finishes charging.
    // Synchronous simulations settle the same opening immediately.
    public bool DeferTurnOpening { get; set; }
    public bool OpeningPending { get; private set; }

    bool BattleRunning => Status == SparStatus.PlayerTurn || Status == SparStatus.OpponentTurn;
    bool ActorReady => BattleRunning && !OpeningPending && ActiveUnit.Alive;

    public SparStatusCounter BurnStatusFor(SparUnit unit) =>
        BattleRunning && unit != null && unit.Alive && burns.TryGetValue(unit, out var burn) ? burn.Effect : default;

    public SparStatusCounter WardStatusFor(SparUnit unit) =>
        BattleRunning && unit != null && unit.Alive && wards.TryGetValue(unit, out var ward) ? ward : default;

    int BurnAmount(SparUnit caster, QingxiSpellDefinition spell) =>
        spell.EffectAmount + (arts.TryGetValue(caster, out var loadout) && loadout.Manual ? loadout.Manual.BurnBonus : 0);

    static int WardAmount(QingxiSpellDefinition spell, WuXing payment) =>
        spell.EffectAmount + (spell.PaymentRelation(payment) > 0 ? spell.GeneratingBonus : 0);

    void ApplyBurn(SparUnit caster, SparUnit target, QingxiSpellDefinition spell)
    {
        int amount = BurnAmount(caster, spell);
        int ticks = spell.EffectTurns;
        if (amount <= 0 || ticks <= 0) return;
        burns[target] = new Burning(amount, ticks, spell);
        statusEvents.Add(new SparStatusEvent(SparStatusEventKind.BurnApplied, target, amount, ticks));
        Message += $"灼烧将在{target.Name}出手前扣除 {amount} 点气血，共 {ticks} 次。";
    }

    void CastWard(SparUnit caster, QingxiSpellDefinition spell, WuXing payment)
    {
        ClearActionResults();
        caster.ActionPoints -= spell.ActionPointCost;
        caster.Acted = true;
        int amount = WardAmount(spell, payment);
        int charges = spell.EffectTurns;
        wards[caster] = new SparStatusCounter(amount, charges);
        statusEvents.Add(new SparStatusEvent(SparStatusEventKind.WardApplied, caster, amount, charges));
        Message = $"{caster.Name}施展{spell.Name}，接下来 {charges} 次直接命中各抵挡至多 {amount} 点伤害。";
        RecordJournalAction(spell.Name, caster);
    }

    int AbsorbHit(SparUnit target, int power)
    {
        var ward = WardStatusFor(target);
        int absorbed = Math.Min(power, ward.Amount);
        if (absorbed <= 0) return 0;
        int remaining = ward.Remaining - 1;
        if (remaining == 0) wards.Remove(target);
        else wards[target] = new SparStatusCounter(ward.Amount, remaining);
        statusEvents.Add(new SparStatusEvent(SparStatusEventKind.WardBlock, target, absorbed, remaining));
        return absorbed;
    }

    public bool ResolveTurnOpening()
    {
        if (!OpeningPending || !BattleRunning) return false;
        OpeningPending = false;
        var actor = ActiveUnit;
        var burn = burns[actor];
        int remaining = burn.Effect.Remaining - 1;
        if (remaining == 0) burns.Remove(actor);
        else burns[actor] = new Burning(burn.Effect.Amount, remaining, burn.Spell);

        ClearActionResults();
        int damage = Math.Min(actor.Hp, burn.Effect.Amount);
        actor.Hp -= damage;
        healthChanges.Add(new SparHealthChange(actor, -damage, burn.Spell));
        statusEvents.Add(new SparStatusEvent(SparStatusEventKind.BurnTick, actor, damage, remaining));
        Message = $"{actor.Name}行动前灼烧发作，气血减少 {damage}。";
        if (!actor.Alive) ResolveDefeat(actor);
        RecordJournalAction("灼烧触发", actor);
        return true;
    }

    public bool FinishTurnOpening()
    {
        if (OpeningPending || !BattleRunning || ActiveUnit.Alive) return false;
        AdvanceInitiative();
        return true;
    }

    void ClearActionResults()
    {
        healthChanges.Clear();
        statusEvents.Clear();
    }
}
