using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum SparActionKind { Hold, Move, Strike, Spell }

public readonly struct SparOpponentAction
{
    public SparActionKind Kind { get; }
    public SparUnit Actor { get; }
    public Vector2Int Destination { get; }
    public SparUnit Target { get; }
    public QingxiSpellDefinition Spell { get; }
    internal WuXing Payment { get; }
    internal int Turn { get; }
    internal int Step { get; }

    internal SparOpponentAction(SparActionKind kind, SparUnit actor, Vector2Int destination, int turn, int step,
        SparUnit target = null, QingxiSpellDefinition spell = null, WuXing payment = default)
    {
        Kind = kind;
        Actor = actor;
        Destination = destination;
        Target = target;
        Spell = spell;
        Payment = payment;
        Turn = turn;
        Step = step;
    }
}

public sealed partial class SparMatch
{
    readonly HashSet<SparUnit> sweptTargets = new HashSet<SparUnit>();
    SparOpponentAction? followUp;
    int opponentStep;
    bool closedDistance;

    readonly struct AttackChoice
    {
        public readonly SparUnit Target;
        public readonly Vector2Int Position;
        public readonly QingxiSpellDefinition Spell;
        public readonly WuXing Payment;
        public readonly int MoveCost;
        public readonly int Priority;

        public AttackChoice(SparUnit target, Vector2Int position, QingxiSpellDefinition spell, WuXing payment,
            int moveCost, int priority)
        {
            Target = target;
            Position = position;
            Spell = spell;
            Payment = payment;
            MoveCost = moveCost;
            Priority = priority;
        }
    }

    void ResetOpponentActions()
    {
        sweptTargets.Clear();
        followUp = null;
        opponentStep = 0;
        closedDistance = false;
    }

    bool OpponentStunned => Party == FieldParty.Brigands && ActiveUnit == units[2] && stunned;

    SparOpponentAction OpponentAction(SparActionKind kind, Vector2Int cell, SparUnit target = null,
        QingxiSpellDefinition spell = null, WuXing payment = default) =>
        new SparOpponentAction(kind, ActiveUnit, cell, TurnNumber, opponentStep, target, spell, payment);

    bool CanExecuteOpponentAttack(SparOpponentAction action) =>
        action.Spell == null ? CanStrikeFrom(action.Actor, action.Actor.Cell, action.Target) :
            CanCastFrom(action.Actor, action.Actor.Cell, action.Target, action.Spell, action.Payment);

    public bool TryPlanOpponentAction(out SparOpponentAction action)
    {
        action = default;
        if (Status != SparStatus.OpponentTurn || !ActorReady) return false;

        var actor = ActiveUnit;
        action = OpponentAction(SparActionKind.Hold, actor.Cell);
        if (OpponentStunned || closedDistance) return true;
        if (followUp.HasValue && CanExecuteOpponentAttack(followUp.Value))
        {
            action = followUp.Value;
            return true;
        }
        if (WardStatusFor(actor).Remaining == 0)
            foreach (var spell in PreparedSpells(actor))
                if (spell.Target == FaShuTarget.Self && TryChooseSpellPayment(actor, spell, out var payment))
                {
                    action = OpponentAction(SparActionKind.Spell, actor.Cell, actor, spell, payment);
                    return true;
                }

        var paths = PathsFrom(actor);
        var choice = PickAutonomousAttack(actor, paths, reachableThisTurn: true);
        if (choice.Target != null)
        {
            var kind = choice.MoveCost > 0 ? SparActionKind.Move :
                choice.Spell == null ? SparActionKind.Strike : SparActionKind.Spell;
            action = OpponentAction(kind, kind == SparActionKind.Move ? choice.Position : choice.Target.Cell,
                choice.Target, choice.Spell, choice.Payment);
            return true;
        }

        // Close once if there was no opening. After striking, end the turn if
        // the remaining points cannot create another useful attack.
        if (actor.Acted || actor.MovementLeft <= 0) return true;
        var distant = PickAutonomousAttack(actor, paths, reachableThisTurn: false);
        if (distant.Target == null) return true;
        var destination = distant.Position;
        while (paths.Cost(destination) > actor.MovementLeft)
            destination = paths.Previous[destination.x, destination.y];
        if (destination != actor.Cell) action = OpponentAction(SparActionKind.Move, destination);
        return true;
    }

    public bool ExecuteOpponentAction(SparOpponentAction action)
    {
        if (Status != SparStatus.OpponentTurn || !ActorReady || action.Actor != ActiveUnit || OpponentStunned ||
            action.Turn != TurnNumber || action.Step != opponentStep) return false;

        var actor = ActiveUnit;
        if (action.Kind == SparActionKind.Move)
        {
            var destination = action.Destination;
            if (IsBlocked(destination) || At(destination) != null || PathsFrom(actor).Cost(destination) > actor.MovementLeft)
                return false;
            Relocate(actor, destination);
            opponentStep++;
            Message = $"{actor.Name}移步 {actor.WalkPath.Count - 1} 格。";
            closedDistance = action.Target == null;
            // A chosen attack can require walking to a firing position. Keep that
            // same attack after the movement is presented before planning anew.
            followUp = closedDistance ? (SparOpponentAction?)null : OpponentAction(
                action.Spell == null ? SparActionKind.Strike : SparActionKind.Spell,
                action.Target.Cell, action.Target, action.Spell, action.Payment);
            return true;
        }

        if (action.Kind == SparActionKind.Spell)
        {
            if (!TryCastDuringTurn(actor, action.Target, action.Spell, action.Payment)) return false;
        }
        else if (action.Kind == SparActionKind.Strike)
        {
            if (!CanExecuteOpponentAttack(action)) return false;
            Cast(actor, action.Target);
        }
        else return false;

        opponentStep++;
        followUp = null;
        // The beast sweeps across the group. Other combatants can focus strikes.
        if (InField && Party != FieldParty.Brigands && actor == units[2]) sweptTargets.Add(action.Target);
        return true;
    }

    public void FinishOpponentTurn()
    {
        if (Status != SparStatus.OpponentTurn || !ActorReady) return;
        if (OpponentStunned)
        {
            stunned = false;
            Message = "阵纹逆转，灰衣客这次行动只能稳住气息。";
        }
        else if (opponentStep == 0) Message = ActiveUnit.Name + "守在原地。";
        AdvanceInitiative();
    }

    // Synchronous callers use the same decisions as the paced battle view.
    public void StepOpponent()
    {
        if (Status != SparStatus.OpponentTurn || !ActorReady) return;
        var actor = ActiveUnit;
        var report = new StringBuilder();
        var route = new List<Vector2Int> { actor.Cell };
        while (TryPlanOpponentAction(out var action) && ExecuteOpponentAction(action))
        {
            if (action.Kind == SparActionKind.Move)
                for (int step = 1; step < actor.WalkPath.Count; step++) route.Add(actor.WalkPath[step]);
            report.Append(Message);
        }
        if (route.Count > 1) actor.WalkPath = route.AsReadOnly();
        if (report.Length > 0) Message = report.ToString();
        FinishOpponentTurn();
    }

    AttackChoice PickAutonomousAttack(SparUnit actor, PathMap paths, bool reachableThisTurn)
    {
        var best = default(AttackChoice);
        ConsiderAttack(StrikeCost, null, default);
        foreach (var spell in PreparedSpells(actor))
            if (spell.Target == FaShuTarget.Enemy && TryChooseSpellPayment(actor, spell, out var payment))
                ConsiderAttack(spell.ActionPointCost, spell, payment);
        return best;

        void ConsiderAttack(int actionCost, QingxiSpellDefinition spell, WuXing payment)
        {
            if (actionCost > actor.ActionPoints) return;
            int walkingAllowance = System.Math.Min(actor.MovementLeft, actor.ActionPoints - actionCost);
            foreach (var position in paths.Reachable)
            {
                int cost = paths.Cost(position);
                if (reachableThisTurn && cost > walkingAllowance) break;
                if (best.Target != null && cost > best.MoveCost) break;
                foreach (var target in units)
                {
                    if (sweptTargets.Contains(target)) continue;
                    if (spell == null ? !CanStrikeFrom(actor, position, target) : !CanCastFrom(actor, position, target, spell, payment))
                        continue;
                    int damage = HitPoints(actor, target, spell, payment);
                    bool defeat = damage >= target.Hp;
                    bool bind = spell != null && spell.Effect == QingxiSpellEffect.Bind;
                    if (bind && !defeat && TargetHasPendingBind(target)) continue;
                    int priority = damage + (defeat ? 8 : 0) + (bind && !defeat ? 3 : 0);
                    if (best.Target != null && cost == best.MoveCost &&
                        (priority < best.Priority || priority == best.Priority && target.Hp >= best.Target.Hp))
                        continue;
                    best = new AttackChoice(target, position, spell, payment, cost, priority);
                }
            }
        }
    }
}
