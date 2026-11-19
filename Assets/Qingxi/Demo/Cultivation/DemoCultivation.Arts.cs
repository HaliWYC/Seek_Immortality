using System.Collections.Generic;
using UnityEngine;

public sealed partial class DemoCultivation
{
    public const int SpellSlots = 2;
    public const int ManualTrainingDays = 3;
    [SerializeField] string mainManualId;
    [SerializeField] List<string> preparedIds;
    [SerializeField] bool preparedInitialized;

    public CultivationManualDefinition MainManual
    {
        get
        {
            if (KnowsArt(mainManualId) && QingxiArts.Find(mainManualId) is CultivationManualDefinition equipped &&
                equipped.GongFaType == GongFaType.Main)
                return equipped;
            // First-volume saves stored possession of 归元篇 before main-manual selection existed.
            return Manual ? QingxiArts.Guiyuan : null;
        }
    }

    List<string> PreparedIds
    {
        get
        {
            if (preparedIds == null) preparedIds = new List<string>(SpellSlots);
            if (!preparedInitialized)
            {
                preparedInitialized = true;
                if (KnowsFirebolt && !preparedIds.Contains(QingxiArts.FireboltId)) preparedIds.Add(QingxiArts.FireboltId);
            }
            return preparedIds;
        }
    }

    public bool IsPrepared(string id) => PreparedIds.Contains(id);

    public IReadOnlyList<QingxiSpellDefinition> PreparedSpells
    {
        get
        {
            var ready = new List<QingxiSpellDefinition>(SpellSlots);
            foreach (var id in PreparedIds)
            {
                var spell = QingxiArts.Find(id) as QingxiSpellDefinition;
                if (spell != null && spell.IsLearnableBattleSpell && KnowsArt(id) && !ready.Contains(spell)) ready.Add(spell);
                if (ready.Count == SpellSlots) break;
            }
            return ready;
        }
    }

    public bool CanStudyAtGate(QingxiArtDefinition entry, bool inSect) =>
        entry != null && inSect && Free && Manual && !KnowsArt(entry.Id) &&
        (entry is CultivationManualDefinition || entry is QingxiSpellDefinition spell && spell.IsLearnableBattleSpell);

    public bool StudyAtGate(QingxiArtDefinition entry, bool inSect)
    {
        if (!CanStudyAtGate(entry, inSect)) return false;
        var slots = PreparedIds; // Migrate the old slots before adding a newly learned spell.
        KnownArts.Add(entry.Id);
        var spell = entry as QingxiSpellDefinition;
        int duration = spell != null ? spell.TrainingDays : ManualTrainingDays;
        if (spell != null && slots.Count < SpellSlots) slots.Add(entry.Id);
        Advance(duration);
        note = $"你在栖霞山门研习{duration}日，习得《{entry.Name}》。";
        return true;
    }

    public bool EquipMain(CultivationManualDefinition book)
    {
        if (!Free || book == null || book.GongFaType != GongFaType.Main || !KnowsArt(book.Id) || MainManual == book) return false;
        mainManualId = book.Id;
        return true;
    }

    public bool TogglePrepared(QingxiSpellDefinition spell)
    {
        if (!Free || spell == null || !spell.IsLearnableBattleSpell || !KnowsArt(spell.Id)) return false;
        var ids = PreparedIds;
        if (ids.Remove(spell.Id)) return true;
        if (ids.Count >= SpellSlots) return false;
        ids.Add(spell.Id);
        return true;
    }
}
