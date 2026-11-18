using System;
using UnityEngine;

[Serializable]
public sealed partial class HerbalStudy
{
    public const string Background = "我在青溪药铺分拣干药，惯用白布衬叶、竹签拨土。今天想出镇认一回鲜草。";
    public const string BookTitle = "《山行药谱》 · 青纹草";
    public const int HerbPrice = 6;
    public const int SharedPayment = HerbPrice / 2;
    public const string BookText =
        "旧货单只在收药处写了「山脚」。有一回雨歇，背篓从石隘那边送来，绑根的布里裹着湿苔和青石屑，几乎不见河沙。\n\n" +
        "夹页画着一片翻过来的叶子，旁注：「晒干则纹隐。取枝上鲜叶，润其背，可见双青纹。」落款已被水洇去。";

    public static readonly string[] Habitats = { "山下河滩的细沙地", "山中背阴的渗泉边", "向阳干燥的碎石坡" };
    public static readonly string[] FieldActions = { "润水翻看叶背", "拨苔察看根土" };
    public static readonly string[] Findings =
    {
        "用白布托起仍连着茎的鲜叶。沾水一翻，叶背显出两道并行的青纹。",
        "用竹签拨开湿苔，细根扎在混有青石屑的黑泥里。石缝正渗水，这株草长在崖石的阴影下。"
    };

    const int AllObservations = 3;
    [SerializeField] bool readBook;
    [SerializeField] int hypothesis = -1;
    [SerializeField] int observations;
    [SerializeField] bool mastered;
    [SerializeField] int disclosedHypothesis = -1;
    [SerializeField] bool promisedShare;
    [SerializeField] bool harvested;
    [SerializeField] bool delivered;

    public bool ReadBook => readBook;
    public int Hypothesis => hypothesis;
    public int Observations => observations;
    public bool Mastered => mastered;
    public bool CanConfirm => !mastered && readBook && observations == AllObservations;
    public bool ToldHypothesis => disclosedHypothesis >= 0;
    public bool PromisedShare => promisedShare;
    public bool HasDirections => ToldHypothesis || promisedShare;
    public bool Harvested => harvested;
    public bool Delivered => delivered;
    public int SaleProceeds => promisedShare ? SharedPayment : HerbPrice;
    public int EarnedCoins => delivered ? SaleProceeds : 0;
    public string RouteHint => HasDirections ? "散修口述：官道到山脚分岔，循石隘间的小径进山。见到细水，沿水往来处找。" : "";

    public string TradeNote
    {
        get
        {
            string agreement = ToldHypothesis ? "已把「" + Habitats[disclosedHypothesis] + "」的猜测告诉散修，换了路讯。" :
                promisedShare ? "与散修约定：他指路，这趟卖药的钱两人平分。" : "此行未与散修订约。";
            if (delivered)
                return agreement + (promisedShare ? $"\n药铺结清 {HerbPrice} 文，分给散修 {HerbPrice - EarnedCoins} 文，自己实收 {EarnedCoins} 文。" :
                    $"\n药铺已收药，自己实收 {EarnedCoins} 文。");
            if (harvested) return agreement + "\n药囊里有一份鲜药，回镇可交给药铺。";
            return agreement;
        }
    }

    public string HypothesisNote => hypothesis >= 0 ? "当时推测：" + Habitats[hypothesis] + "。依据是药谱夹页和旧货单。" :
        readBook ? "已读药谱，尚未写下自己的生境猜测。" : "尚无猜测，可以从铺里的药谱入手。";

    public string EvidenceNote
    {
        get
        {
            if (observations == AllObservations) return Findings[0] + "\n" + Findings[1];
            if (observations == 1) return Findings[0];
            if (observations == 2) return Findings[1];
            return "尚未察验活株。";
        }
    }

    public string StatusNote
    {
        get
        {
            if (mastered)
            {
                string comparison = hypothesis == 1 ? "这次所见与猜测相符。" :
                    hypothesis >= 0 ? "先前的猜测与实地所见不同，原猜留作对照。" : "";
                return "已掌握 · 青纹草。亲验活株长于背阴泉畔，鲜叶背面润水显双纹。" + comparison;
            }
            if (CanConfirm) return "两项亲验已有记录，可以对照药谱落笔确认。";
            if (observations == AllObservations) return "叶纹与根土都已记下。回铺查对药谱，给这株草定名。";
            return observations == 0 ? "待核验。亲眼见到鲜草后，再验叶纹与根土。" : "待核验。还缺一项现场观察。";
        }
    }

    public bool Read()
    {
        if (readBook) return false;
        readBook = true;
        return true;
    }

    public bool RecordHypothesis(int choice)
    {
        if (!readBook || mastered || choice < 0 || choice >= Habitats.Length || hypothesis == choice) return false;
        hypothesis = choice;
        return true;
    }

    public bool Observe(int action)
    {
        if (action < 0 || action >= FieldActions.Length) return false;
        int bit = 1 << action;
        if ((observations & bit) != 0) return false;
        observations |= bit;
        return true;
    }

    public bool Confirm()
    {
        if (!CanConfirm) return false;
        mastered = true;
        return true;
    }

    public bool TellHypothesis()
    {
        if (!readBook || hypothesis < 0 || HasDirections || mastered || harvested) return false;
        disclosedHypothesis = hypothesis;
        return true;
    }

    public bool PromiseShare()
    {
        if (HasDirections || mastered || harvested) return false;
        promisedShare = true;
        return true;
    }

    public bool Harvest()
    {
        if (!mastered || harvested) return false;
        harvested = true;
        return true;
    }

    public bool Deliver()
    {
        if (!harvested || delivered) return false;
        delivered = true;
        return true;
    }

    public static HerbalStudy Restore(string json)
    {
        var result = new HerbalStudy();
        if (string.IsNullOrEmpty(json)) return result;
        try { JsonUtility.FromJsonOverwrite(json, result); }
        catch (ArgumentException) { return new HerbalStudy(); }
        result.observations &= AllObservations;
        if (!result.readBook || result.hypothesis < -1 || result.hypothesis >= Habitats.Length) result.hypothesis = -1;
        result.mastered &= result.readBook && result.observations == AllObservations;
        if (!result.readBook || result.disclosedHypothesis < -1 || result.disclosedHypothesis >= Habitats.Length)
            result.disclosedHypothesis = -1;
        if (result.ToldHypothesis) result.promisedShare = false;
        result.harvested &= result.mastered;
        result.delivered &= result.harvested;
        result.RestorePractice();
        return result;
    }
}
