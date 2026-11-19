using System;
using UnityEngine;

// An effect describes what this demo resolves. The spell's classification uses the TaiXu enums.
public enum QingxiSpellEffect { Strike, ConvertSpirit, Bind, Harmonize, Burn, Ward }

[Serializable]
public struct DaoCangCost
{
    public WuXing element;
    [Min(1)] public int amount;
}

[CreateAssetMenu(menuName = "青岚/法术", fileName = "NewSpell")]
public sealed class QingxiSpellDefinition : QingxiArtDefinition
{
    [SerializeField] FaShuType faShuType;
    [SerializeField] FaShuSourceType sourceType;
    [SerializeField] FaShuTarget target;
    [SerializeField] FaShuDuration duration;
    [SerializeField] FaShuLevel level;
    [SerializeField] FaShuProficiency proficiency;
    [SerializeField] QingxiSpellEffect effect;
    [SerializeField, Min(0)] int range;
    [SerializeField, Min(0)] int extraPower;
    [SerializeField, Min(0)] int cooldownTurns;
    [SerializeField, Min(0)] int conversionAmount;
    [SerializeField, Min(0)] int trainingDays;
    [SerializeField, Min(0)] int manaCost;
    [SerializeField, Min(0), Tooltip("普通术法不消耗行动力。仅在该术另有行动要求时配置。")] int actionPointCost;
    [SerializeField] DaoCangCost[] fixedCosts = Array.Empty<DaoCangCost>();
    [SerializeField, Min(0), Tooltip("另付同一行道藏的数量，玩家可切换使用哪一行")] int sameCost;
    [SerializeField, Min(0), Tooltip("减步、相生回血、相克增伤、灼烧伤害或每击抵挡量")] int effectAmount;
    [SerializeField, Min(0), Tooltip("灼烧剩余发作次数，或护体可抵挡的次数")] int effectTurns;
    [SerializeField, Min(0), Tooltip("相生付气提升的每击抵挡量")] int generatingBonus;

    public FaShuType FaShuType => faShuType;
    public FaShuSourceType SourceType => sourceType;
    public FaShuTarget Target => target;
    public FaShuDuration Duration => duration;
    public FaShuLevel Level => level;
    public FaShuProficiency Proficiency => proficiency;
    public QingxiSpellEffect Effect => effect;
    public bool IsBattleSpell => effect == QingxiSpellEffect.Ward ? target == FaShuTarget.Self :
        target == FaShuTarget.Enemy && effect != QingxiSpellEffect.ConvertSpirit;
    public bool IsLearnableBattleSpell => IsBattleSpell && sourceType == FaShuSourceType.法术;
    public int Range => range;
    public int ExtraPower => extraPower;
    // Number of full personal turns between two casts, excluding the casting turn.
    public int CooldownTurns => cooldownTurns;
    public int ConversionAmount => conversionAmount;
    public int TrainingDays => trainingDays;
    public int ManaCost => manaCost;
    public int ActionPointCost => actionPointCost;
    public int SameCost => sameCost;
    public int EffectAmount => effectAmount;
    public int EffectTurns => effectTurns;
    public int GeneratingBonus => generatingBonus;

    internal bool HasBattleLimit
    {
        get
        {
            if (manaCost > 0 || actionPointCost > 0 || cooldownTurns > 0 || sameCost > 0) return true;
            foreach (var cost in fixedCosts)
                if (cost.amount > 0) return true;
            return false;
        }
    }

    public int FixedCost(WuXing element)
    {
        int amount = 0;
        foreach (var cost in fixedCosts)
            if (cost.element == element) amount += cost.amount;
        return amount;
    }

    public string CostText
    {
        get
        {
            string text = "";
            foreach (var cost in fixedCosts)
                text += (text.Length > 0 ? "＋" : "") + cost.element + cost.amount;
            if (sameCost > 0) text += (text.Length > 0 ? "＋" : "") + "同类" + sameCost;
            return text.Length > 0 ? text : "无道藏消耗";
        }
    }

    // For two different spent elements TaiXu checks either direction. More slots
    // must form an ordered generating/overcoming chain. A repeated slot is merged.
    public int PaymentRelation(WuXing chosen)
    {
        int seen = 0, count = 0, direction = 0;
        WuXing first = default, previous = default;
        for (int i = 0; i <= fixedCosts.Length; i++)
        {
            if (i == fixedCosts.Length && sameCost == 0) break;
            WuXing element = i < fixedCosts.Length ? fixedCosts[i].element : chosen;
            int bit = 1 << (int)element;
            if ((seen & bit) != 0) continue;
            seen |= bit;
            if (count == 0) first = element;
            else
            {
                int step = FivePhaseRelation(previous, element);
                direction = count == 1 ? step : (direction == step ? direction : 0);
            }
            previous = element;
            count++;
        }
        if (count == 2 && direction == 0) return FivePhaseRelation(previous, first);
        return count >= 2 ? direction : 0;
    }

    static int FivePhaseRelation(WuXing from, WuXing to)
    {
        switch (from)
        {
            case WuXing.锐金: return to == WuXing.弱水 ? 1 : to == WuXing.灵木 ? -1 : 0;
            case WuXing.灵木: return to == WuXing.离火 ? 1 : to == WuXing.厚土 ? -1 : 0;
            case WuXing.弱水: return to == WuXing.灵木 ? 1 : to == WuXing.离火 ? -1 : 0;
            case WuXing.离火: return to == WuXing.厚土 ? 1 : to == WuXing.锐金 ? -1 : 0;
            case WuXing.厚土: return to == WuXing.锐金 ? 1 : to == WuXing.弱水 ? -1 : 0;
            default: throw new ArgumentOutOfRangeException(nameof(from));
        }
    }
}
