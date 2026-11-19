using UnityEngine;

public enum BattlePlaybackPhase { Idle, Movement, Windup, Impact, Closing, Opening }

public sealed partial class QingxiSparring
{
    const float StepPause = .11f;
    const float StrikeWindup = .28f;
    const float SpellWindup = .48f;
    const float ImpactPause = .50f;
    const float ClosePause = .18f;

    SparOpponentAction opponentCommand;
    OrderKind playerCast;
    SparUnit impactTarget;
    Vector2Int impactCell;
    int impactCarrySlot = -1;
    float playbackRemaining;

    public BattlePlaybackPhase PlaybackPhase { get; private set; }
    public SparUnit PlaybackActor { get; private set; }
    public bool PlayingAction => PlaybackPhase != BattlePlaybackPhase.Idle;
    public bool CanLeaveSpar => Finished && !PlayingAction;

    void ResetPlayback()
    {
        SetPlayback(BattlePlaybackPhase.Idle, null);
        opponentCommand = default;
        impactTarget = null;
        playerCast = OrderKind.None;
        impactCarrySlot = -1;
    }

    void SetPlayback(BattlePlaybackPhase phase, SparUnit actor, float delay = 0f)
    {
        PlaybackPhase = phase;
        PlaybackActor = actor;
        playbackRemaining = delay;
    }

    bool QueuePlayerAttack(OrderKind kind, SparUnit actor, Vector2Int cell, int carrySlot)
    {
        var target = kind == OrderKind.Carried ? Match.CarriedTarget(carrySlot) :
            kind == OrderKind.Bandage ? actor : Match.At(cell);
        bool legal = kind == OrderKind.Carried ? Match.CanUseCarried(actor, carrySlot) :
            kind == OrderKind.Bandage ? Match.CanUseAgreedHerb(actor) : Match.CanAttack(actor, cell);
        if (!legal || target == null) return false;

        playerCast = kind;
        impactTarget = target;
        impactCell = cell;
        impactCarrySlot = carrySlot;
        SetPlayback(BattlePlaybackPhase.Movement, actor);
        if (board.IsMoving(actor)) feedback = actor.Name + "正走向出招位置。";
        else StartPlayerWindup(actor);
        return true;
    }

    void StartPlayerWindup(SparUnit actor)
    {
        var spell = playerCast == OrderKind.Attack && Match.SpellSelected ? Match.PlayerSpell : null;
        var item = playerCast == OrderKind.Carried ? Match.Carried(impactCarrySlot) : null;
        string name = item?.Name ?? (playerCast == OrderKind.Bandage ? "止血散" : spell?.Name ?? "常击");
        if (Match.IsRelicSpell(spell)) name = "法宝 · " + name;
        float duration = playerCast == OrderKind.Attack && !spell ? StrikeWindup : SpellWindup;
        StartWindup(actor, impactTarget, spell, name, duration,
            playerCast == OrderKind.Bandage || item?.CarryEffect == CultivationCarryEffect.Heal);
    }

    void StartWindup(SparUnit actor, SparUnit target, QingxiSpellDefinition spell, string name, float duration, bool restorative = false)
    {
        SetPlayback(BattlePlaybackPhase.Windup, actor, duration);
        cursor = target.Cell;
        feedback = $"{actor.Name} · {name}  →  {target.Name}";
        board.BeginAttackCue(actor, target, spell, duration, restorative);
    }

    void StartOpponentStep()
    {
        if (!Match.TryPlanOpponentAction(out var command)) return;
        opponentCommand = command;
        if (command.Kind == SparActionKind.Hold)
        {
            SetPlayback(BattlePlaybackPhase.Closing, command.Actor, ClosePause);
            feedback = command.Actor.Name + "收势。";
        }
        else if (command.Kind == SparActionKind.Move)
        {
            if (!Match.ExecuteOpponentAction(command)) return;
            SetPlayback(BattlePlaybackPhase.Movement, command.Actor, StepPause);
            cursor = command.Destination;
            feedback = Match.Message;
        }
        else
        {
            impactTarget = command.Target;
            StartWindup(command.Actor, command.Target, command.Spell, command.Spell?.Name ?? "常击",
                command.Kind == SparActionKind.Spell ? SpellWindup : StrikeWindup);
        }
        Refresh();
    }

    void BeginTurnOpening()
    {
        var actor = Match.ActiveUnit;
        if (!Match.ResolveTurnOpening()) return;
        feedback = "";
        SetPlayback(BattlePlaybackPhase.Opening, actor, ImpactPause);
        ShowImpactFeedback();
        Refresh();
    }

    void TickPlayback()
    {
        var actor = PlaybackActor;
        if ((PlaybackPhase == BattlePlaybackPhase.Movement || PlaybackPhase == BattlePlaybackPhase.Closing) && board.IsMoving(actor))
            return;
        playbackRemaining -= Mathf.Min(Time.unscaledDeltaTime, .1f);
        if (playbackRemaining > 0f) return;

        bool saveCarried = false;
        switch (PlaybackPhase)
        {
            case BattlePlaybackPhase.Movement:
                if (playerCast != OrderKind.None) StartPlayerWindup(actor);
                else ResetPlayback();
                break;
            case BattlePlaybackPhase.Windup:
                saveCarried = ResolveImpact(actor);
                break;
            case BattlePlaybackPhase.Impact:
                ResetPlayback();
                break;
            case BattlePlaybackPhase.Opening:
                if (Match.FinishTurnOpening()) SyncActor();
                else ResetPlayback();
                break;
            case BattlePlaybackPhase.Closing:
                if (Match.Status == SparStatus.OpponentTurn) Match.FinishOpponentTurn();
                else Match.EndTurn();
                SyncActor();
                feedback = "";
                break;
        }
        Refresh();
        if (saveCarried) DemoRun.Save();
        if (CanLeaveSpar && Match.Party == FieldParty.Brigands && DemoRun.Slain)
            DemoRun.ShowEpitaph();
    }

    bool ResolveImpact(SparUnit actor)
    {
        bool applied;
        switch (playerCast)
        {
            case OrderKind.Attack: applied = Match.Attack(actor, impactCell); break;
            case OrderKind.Carried: applied = Match.UseCarried(actor, impactCarrySlot); break;
            case OrderKind.Bandage: applied = Match.UseAgreedHerb(actor); break;
            default: applied = Match.ExecuteOpponentAction(opponentCommand); break;
        }
        feedback = applied ? "" : "局势已变，这一招没有结算。";
        if (applied) ShowImpactFeedback();
        SetPlayback(BattlePlaybackPhase.Impact, actor, ImpactPause);
        return applied && playerCast == OrderKind.Carried && Match.InField;
    }

    void ShowImpactFeedback()
    {
        foreach (var change in Match.LastHealthChanges)
            board.ShowHealthChange(change.Target, change.Amount, change.Spell);
        foreach (var effect in Match.LastStatusEvents)
            if (effect.Kind == SparStatusEventKind.WardBlock)
                board.ShowBlockedDamage(effect.Target, effect.Amount);
    }
}
