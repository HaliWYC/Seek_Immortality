using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class DemoRun
{
    enum CharacterLeaf { Attributes, Arts, Bag }
    static readonly Color DossierFrame = new Color32(47, 66, 56, 255);
    static readonly Color DossierRecess = new Color32(21, 35, 31, 255);
    CharacterLeaf characterLeaf;
    HerbalStudy characterJournal;
    bool confirmDiscard;
    int selectedArt;
    // 听息诀属于药铺入门经历，其他行使用能跨存档版本读取的典籍 ID。
    static readonly string[] artIds = { null, QingxiArts.GuiyuanId, QingxiArts.ReturnFlowId, QingxiArts.FireboltId,
        QingxiArts.EmberManualId, QingxiArts.SpringManualId, QingxiArts.VineId, QingxiArts.PulseId };
    string selectedItem;
    const string FreshGrass = "quest.qingwen_grass";

    public static void OpenCharacter(bool bag = false) => OpenCharacter(bag, false);

    internal static void OpenEncounterBag() => OpenCharacter(true, true);

    static void OpenCharacter(bool bag, bool preparingEncounter)
    {
        if (!host || !Active || (!preparingEncounter && !host.FreeToOpen && host.page != Page.Menu && host.page != Page.Guide)) return;
        host.characterJournal = host.apprentice ? host.apprentice.Study :
            HerbalStudy.Restore(PlayerPrefs.GetString(Key("herbal"), ""));
        host.selectedArt = 0;
        host.ResetBagPanel();
        host.SelectLeaf(bag ? CharacterLeaf.Bag : CharacterLeaf.Attributes);
        host.Show(Page.Character);
    }

    void SelectLeaf(CharacterLeaf next)
    {
        characterLeaf = next;
        confirmDiscard = false;
    }

    void CharacterKeys(Keyboard keys)
    {
        if (keys.cKey.wasPressedThisFrame)
        {
            if (characterLeaf == CharacterLeaf.Attributes) CloseWindow();
            else SelectLeaf(CharacterLeaf.Attributes);
            return;
        }
        if (keys.bKey.wasPressedThisFrame)
        {
            if (characterLeaf == CharacterLeaf.Bag) CloseWindow();
            else SelectLeaf(CharacterLeaf.Bag);
            return;
        }
        if (keys.digit1Key.wasPressedThisFrame) SelectLeaf(CharacterLeaf.Attributes);
        if (keys.digit2Key.wasPressedThisFrame) SelectLeaf(CharacterLeaf.Arts);
        if (keys.digit3Key.wasPressedThisFrame) SelectLeaf(CharacterLeaf.Bag);
        if (characterLeaf == CharacterLeaf.Arts)
        {
            if (keys.upArrowKey.wasPressedThisFrame) selectedArt = (selectedArt + artIds.Length - 1) % artIds.Length;
            else if (keys.downArrowKey.wasPressedThisFrame) selectedArt = (selectedArt + 1) % artIds.Length;
            else if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) LearnSelectedArt();
        }
    }

    void DrawCharacter(float width, float height)
    {
        float left = (width - 950f) / 2f;
        Block(new Rect(0, 0, width, height), DossierRecess);
        Block(new Rect(0, 0, width, 157), Ink);
        Block(new Rect(0, 0, width, 3), Gold);
        Words(left, 21, 350, 70, "人物志", 41, Paper, true);
        if (characterLeaf != CharacterLeaf.Attributes)
            Words(left + 174, 39, 480, 44, Name, 22, Soft);
        if (Action(width - left - 171, 36, 171, 43, "返回游历   Esc")) { CloseWindow(); return; }
        DossierTab(left, 242, "身世与境界", "1 / C", CharacterLeaf.Attributes);
        DossierTab(left + 245, 224, "功法与法术", "2", CharacterLeaf.Arts);
        DossierTab(left + 472, 210, "随身行囊", "3 / B", CharacterLeaf.Bag);
        Block(new Rect(0, 156, width, 1), DossierFrame);
        Block(new Rect(0, height - 50, width, 50), Ink);
        Block(new Rect(0, height - 50, width, 1), DossierFrame);
        Words(left, height - 41, 650, 34, character.place + "  ·  " + State.Attributes(Sensed).Realm.Name, 15, Soft);
        var screenMatrix = GUI.matrix;
        GUI.matrix = screenMatrix * Matrix4x4.Translate(new Vector3((width - 1100f) * .5f, (height - 700f) * .5f - 14f, 0));
        if (characterLeaf == CharacterLeaf.Attributes) DrawAttributes();
        else if (characterLeaf == CharacterLeaf.Arts) DrawArts();
        else DrawBag();
        GUI.matrix = screenMatrix;
    }

    void DossierTab(float x, float width, string text, string key, CharacterLeaf leaf)
    {
        var bounds = new Rect(x, 108, width, 48);
        bool current = characterLeaf == leaf;
        if (current || bounds.Contains(Event.current.mousePosition))
            Block(bounds, current ? Pine : DossierFrame);
        if (current) Block(new Rect(x, 153, width, 3), Gold);
        Words(x + 15, 108, width - 72, 45, text, 19, current ? Paper : Soft, current);
        Words(x + width - 57, 109, 54, 45, key, 15, current ? Gold : Soft);
        if (GUI.Button(bounds, GUIContent.none, GUIStyle.none)) SelectLeaf(leaf);
    }

    void OpenGearFromSheet(CultivationGearSlot slot)
    {
        SelectLeaf(CharacterLeaf.Bag);
        FocusGearSlot(slot);
    }

    void DrawArts()
    {
        Block(new Rect(75, 211, 349, 417), DossierRecess);
        Block(new Rect(454, 211, 571, 417), DossierRecess);
        var main = State.MainManual;
        var equipped = State.PreparedSpells;
        for (int i = 0; i < artIds.Length; i++)
        {
            var entry = QingxiArts.Find(artIds[i]);
            string name = entry != null ? entry.Name : "听息诀";
            string status;
            if (i == 0) status = characterJournal.Delivered ? "已借阅" : "药铺入门";
            else if (entry == main) status = "当前主修";
            else if (State.IsPrepared(entry.Id)) status = "已备战";
            else if (State.KnowsArt(entry.Id)) status = "已习得";
            else status = entry is CultivationManualDefinition ? "功法" : "法术";
            if (Action(86, 222 + 42 * i, 322, 37, name + "　·　" + status, false, selectedArt == i)) selectedArt = i;
        }
        Words(86, 566, 335, 66, $"↑ ↓ 选卷　·　备战 {equipped.Count}/{DemoCultivation.SpellSlots}\n主修　{(main != null ? main.Name : "尚未修习")}", 16, Soft);
        var selectedEntry = QingxiArts.Find(artIds[selectedArt]);
        if (selectedEntry == null)
        {
            Words(474, 225, 525, 60, "听息诀", 32, Gold, true);
            Words(475, 302, 517, 78, "药铺入门吐纳　·　自修六个时辰，受指点可加快。", 19, Paper);
            Words(475, 400, 507, 99, characterJournal.Delivered ? "已经向掌柜借阅。药铺东侧静席可修习。" :
                "找到青纹草活株、亲手核验并回铺交药后，可向掌柜借读。", 18, Soft);
            return;
        }

        var book = selectedEntry as CultivationManualDefinition;
        var spell = selectedEntry as QingxiSpellDefinition;
        string category = book != null ? (book.GongFaType == GongFaType.Main ? "主修功法" : "辅修功法") : spell.FaShuType.ToString();
        Words(474, 223, 524, 48, selectedEntry.Name, 30, Gold, true);
        Words(475, 272, 520, 39, $"{category}　·　{selectedEntry.ShuXing}　·　{selectedEntry.Rarity}{selectedEntry.MiniRarity}", 18, Paper, true);
        string rules;
        if (book != null)
        {
            string intake = book.ExtraRootDraws > 0 ? $"依灵根额外引气 {book.ExtraRootDraws} 缕" :
                $"额外引入 {book.BonusElement} {book.BonusAmount} 缕";
            rules = $"每季修为 +{book.QiPerSeason}　·　可修至{book.Jingjie}{book.MiniJingjie}\n本人行动开始时，{intake}。";
        }
        else if (spell.IsBattleSpell) rules = $"耗气　{spell.CostText}　　射程 {spell.Range} 格\n冷却 {spell.CooldownTurns} 次本人行动　·　{spell.Level}阶　{spell.Proficiency}";
        else rules = $"冲关时每转可调动 {spell.ConversionAmount} 缕异气。";
        Words(475, 314, 515, 73, rules, 17, Paper);
        Words(475, 391, 510, 88, selectedEntry.Description, 18, Paper);

        bool known = State.KnowsArt(selectedEntry.Id);
        string detail = selectedEntry.Source;
        if (!known && selectedEntry == QingxiArts.ReturnFlow) detail += $"　·　{State.CurrentArtPrice} 灵石";
        else if (!known && selectedEntry != QingxiArts.Guiyuan)
            detail += $"　·　入门后研习 {(book != null ? DemoCultivation.ManualTrainingDays : spell.TrainingDays)} 日";
        else if (known && spell != null && spell.IsBattleSpell)
            detail = State.IsPrepared(spell.Id) ? "已列入斗法备招，轮到本人时可选用。" :
                equipped.Count >= DemoCultivation.SpellSlots ? "备招已满，先选一门已备法术并将它撤下。" : "列入备招后，切磋与野外斗法均可选用。";
        Words(475, 487, 515, 63, detail, 17, Soft);
        string action = ArtAction(selectedEntry);
        if (action != null && Action(475, 565, 400, 46, action + "   Enter", true)) LearnSelectedArt();
    }

    string ArtAction(QingxiArtDefinition entry)
    {
        if (entry == null || !State.Free) return null;
        var book = entry as CultivationManualDefinition;
        var spell = entry as QingxiSpellDefinition;
        if (State.KnowsArt(entry.Id))
        {
            if (book != null) return book.GongFaType == GongFaType.Main && State.MainManual != book ? "改为主修" : null;
            if (!spell.IsBattleSpell) return null;
            if (State.IsPrepared(spell.Id)) return "从斗法备招撤下";
            return State.PreparedSpells.Count < DemoCultivation.SpellSlots ? "列入斗法备招" : null;
        }
        if (!InSect || !Sensed) return null;
        if (entry == QingxiArts.Guiyuan) return "向知客借《归元篇》";
        if (entry == QingxiArts.ReturnFlow) return State.Manual && State.Stones >= State.CurrentArtPrice ? "付灵石习归流术" : null;
        return State.CanStudyAtGate(entry, InSect) ? "向山门知客研习" : null;
    }

    void LearnSelectedArt()
    {
        var entry = QingxiArts.Find(artIds[selectedArt]);
        if (ArtAction(entry) == null) return;
        bool changed;
        if (State.KnowsArt(entry.Id))
            changed = entry is CultivationManualDefinition book ? State.EquipMain(book) : State.TogglePrepared((QingxiSpellDefinition)entry);
        else if (entry == QingxiArts.Guiyuan) changed = State.AcceptManual();
        else if (entry == QingxiArts.ReturnFlow) changed = State.LearnArt();
        else changed = State.StudyAtGate(entry, InSect);
        if (changed) Save();
    }
}
