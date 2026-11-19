using UnityEngine;

public abstract class QingxiArtDefinition : ScriptableObject
{
    [SerializeField] string id;
    [SerializeField] string displayName;
    [SerializeField, TextArea] string description;
    [SerializeField] string source;

    [SerializeField] Rarity rarity = Rarity.凡级;
    [SerializeField] MiniRarity miniRarity;
    [SerializeField] ShuXing shuXing;

    public Rarity Rarity => rarity;
    public MiniRarity MiniRarity => miniRarity;
    public ShuXing ShuXing => shuXing;
    public string Id => id;
    public string Name => displayName;
    public string Description => description;
    public string Source => source;
}
