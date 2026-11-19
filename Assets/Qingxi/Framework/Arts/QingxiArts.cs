using System;
using System.Collections.Generic;
using UnityEngine;

public static class QingxiArts
{
    public const string GuiyuanId = "manual.guiyuan";
    public const string EmberManualId = "manual.ember";
    public const string SpringManualId = "manual.spring";
    public const string ReturnFlowId = "spell.return-flow";
    public const string FireboltId = "spell.firebolt";
    public const string VineId = "spell.vine";
    public const string PulseId = "spell.pulse";
    public const string WardId = "spell.ward";
    public const string JadeRayId = "spell.jade-ray";

    static Dictionary<string, QingxiArtDefinition> entries;
    static IReadOnlyList<CultivationManualDefinition> manuals;
    static IReadOnlyList<QingxiSpellDefinition> spells;

    public static IReadOnlyList<CultivationManualDefinition> Manuals { get { Load(); return manuals; } }
    public static IReadOnlyList<QingxiSpellDefinition> Spells { get { Load(); return spells; } }
    public static CultivationManualDefinition Guiyuan => (CultivationManualDefinition)Find(GuiyuanId);
    public static CultivationManualDefinition EmberManual => (CultivationManualDefinition)Find(EmberManualId);
    public static CultivationManualDefinition SpringManual => (CultivationManualDefinition)Find(SpringManualId);
    public static QingxiSpellDefinition ReturnFlow => (QingxiSpellDefinition)Find(ReturnFlowId);
    public static QingxiSpellDefinition Firebolt => (QingxiSpellDefinition)Find(FireboltId);
    public static QingxiSpellDefinition Vine => (QingxiSpellDefinition)Find(VineId);
    public static QingxiSpellDefinition Pulse => (QingxiSpellDefinition)Find(PulseId);
    public static QingxiSpellDefinition Ward => (QingxiSpellDefinition)Find(WardId);
    public static QingxiSpellDefinition JadeRay => (QingxiSpellDefinition)Find(JadeRayId);

    public static QingxiArtDefinition Find(string id)
    {
        Load();
        return id != null && entries.TryGetValue(id, out var found) ? found : null;
    }

    static void Load()
    {
        if (entries != null) return;
        var catalog = new Dictionary<string, QingxiArtDefinition>(StringComparer.Ordinal);
        var books = new List<CultivationManualDefinition>();
        var arts = new List<QingxiSpellDefinition>();
        var resources = Resources.LoadAll<QingxiArtDefinition>("QingxiArts");
        Array.Sort(resources, (a, b) => string.CompareOrdinal(a.Id, b.Id));
        foreach (var entry in resources)
        {
            if (string.IsNullOrWhiteSpace(entry.Id)) throw new InvalidOperationException($"典籍 {entry.name} 缺少存档 ID。");
            catalog.Add(entry.Id, entry);
            if (entry is CultivationManualDefinition book) books.Add(book);
            if (entry is QingxiSpellDefinition art) arts.Add(art);
        }
        manuals = books.AsReadOnly();
        spells = arts.AsReadOnly();
        entries = catalog;
    }
}
