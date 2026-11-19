using UnityEngine;

[CreateAssetMenu(menuName = "青岚/功法", fileName = "NewCultivationManual")]
public sealed class CultivationManualDefinition : QingxiArtDefinition
{
    [SerializeField] GongFaType gongFaType;
    [SerializeField, Min(1)] int qiPerSeason = 3;
    [SerializeField] JingjieLevel jingjie = JingjieLevel.炼气;
    [SerializeField] MiniJingjieLevel miniJingjie = MiniJingjieLevel.大圆满;
    [SerializeField, Min(0), Tooltip("本人每次行动开始时，依五行灵根额外引气的次数")] int extraRootDraws;
    [SerializeField] WuXing bonusElement;
    [SerializeField, Min(0), Tooltip("依根引气后，由主修功法额外生成的此行道藏")] int bonusAmount;
    [SerializeField, Min(0), Tooltip("每次灼烧发作时，由此主修增加的伤害")] int burnBonus;

    public GongFaType GongFaType => gongFaType;
    public JingjieLevel Jingjie => jingjie;
    public MiniJingjieLevel MiniJingjie => miniJingjie;
    public int QiPerSeason => qiPerSeason;
    public int ExtraRootDraws => extraRootDraws;
    public WuXing BonusElement => bonusElement;
    public int BonusAmount => bonusAmount;
    public int BurnBonus => burnBonus;

    // The local progression table also contains None and FirstSense before its five cultivation phases.
    public int MaximumQi => CultivationRealms.Table.Get((RealmMajor)jingjie,
        jingjie == JingjieLevel.凡人 ? RealmPhase.None : RealmPhase.Early + (int)miniJingjie).MinimumQi;
}
