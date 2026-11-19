using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class DemoRun
{
    static readonly Color Ink = new Color32(29, 45, 40, 255);
    static readonly Color Pine = new Color32(46, 75, 62, 255);
    static readonly Color Paper = new Color32(239, 232, 208, 255);
    static readonly Color Gold = new Color32(222, 187, 121, 255);
    static readonly Color Soft = new Color32(198, 209, 185, 255);
    Font font;
    bool editingName;
    Texture2D shade;
    GUIStyle label, input;

    string NextStep()
    {
        if (State.Foundation) return "已筑道基。可继续访友、行旅，或另开一卷。";
        if (!Sensed)
        {
            var h = apprentice ? apprentice.Study : null;
            if (h == null) return "回青溪镇，从药铺柜台的药谱开始。";
            if (h.Delivered) return "到药铺东侧的蒲团读《听息诀》，试着辨出第一缕灵气。";
            if (h.Harvested) return "带鲜药回青溪药铺，在柜上交货。";
            if (h.Mastered) return "已识得青纹草。回到活株前，留根剪取一份鲜药。";
            if (h.CanConfirm) return "手里已经有了两条亲验笔录，可以对照药谱落笔。";
            return h.ReadBook ? "从药谱和沿途的地形判断青纹草长在哪里。也可以向东门外的散修问路。" :
                "在药铺门前按 E 翻《山行药谱》，记下自己的生境猜测。";
        }
        if (!State.Manual) return InSect ? "已到栖霞山门。在修行簿中向守阁人求《归元篇》。" :
            "去栖霞宗借一卷入门功法。穿过青溪东门时按 M 启程，路由你选。";
        if (State.Qi < DemoCultivation.Bottleneck) return "在青溪镇或栖霞宗找一处静地，安排修习；也可赴松风隘寻药。";
        return "修为已至瓶颈。备妥丹药与功法后，自择时日冲击筑基。";
    }

    void MenuKeys(Keyboard keys)
    {
        if (page == Page.Epitaph)
        {
            if (keys.escapeKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame) Show(Page.Title);
            return;
        }
        if (keys.escapeKey.wasPressedThisFrame)
        {
            if (page == Page.Cultivate) CultivationWindow.Escape();
            else if (page == Page.Menu || page == Page.Guide) CloseWindow();
            else if (page == Page.Confirm) Show(replaceSandbox ? Page.Sandbox : Page.Story);
            else if (page == Page.Sandbox || page == Page.Story) Show(Page.Title);
            else if (Active) CloseWindow();
            return;
        }
        if (page == Page.Play) return;
        if (page == Page.Title)
        {
            if (keys.digit1Key.wasPressedThisFrame) Show(Page.Story);
            if (keys.digit2Key.wasPressedThisFrame) Show(Page.Sandbox);
            if (keys.enterKey.wasPressedThisFrame) ContinueLast();
        }
        else if ((page == Page.Story || page == Page.Sandbox) && keys.enterKey.wasPressedThisFrame)
        {
            bool sandbox = page == Page.Sandbox;
            if (sandbox && editingName) return;
            var saved = ReadSlot(sandbox);
            if (saved != null) SwitchTo(saved); else Begin(sandbox, false);
        }
        else if (page == Page.Character) CharacterKeys(keys);
        else if ((page == Page.Menu || page == Page.Guide) && keys.cKey.wasPressedThisFrame) OpenCharacter();
        else if ((page == Page.Menu || page == Page.Guide) && keys.bKey.wasPressedThisFrame) OpenCharacter(true);
        else if (page == Page.Menu && keys.f2Key.wasPressedThisFrame) CloseWindow();
        else if (page == Page.Guide && keys.f1Key.wasPressedThisFrame) CloseWindow();
        else if (page == Page.Cultivate && keys.f1Key.wasPressedThisFrame) CultivationWindow.Escape();
        else if (page == Page.Cultivate) CultivationWindow.KeyboardInput(keys);
    }

    void ContinueLast()
    {
        if (Active)
        {
            if (Slain) Show(Page.Epitaph);
            else if (State.Expired) { CultivationWindow.Open(); Show(Page.Cultivate); }
            else CloseWindow();
            return;
        }
        bool last = PlayerPrefs.GetInt(Archive + "LastSlot", PlayerPrefs.GetInt(PreviousArchive + "LastSlot", 0)) == 1;
        var saved = ReadSlot(last) ?? ReadSlot(!last);
        if (saved != null) SwitchTo(saved); else Show(Page.Story);
    }

    void RequestNew(bool sandbox)
    {
        if (ReadSlot(sandbox) == null) { Begin(sandbox, false); return; }
        replaceSandbox = sandbox;
        Show(Page.Confirm);
    }

    void SetupGui()
    {
        if (label != null) return;
        font = Resources.Load<Font>("NotoSansSC-Regular");
        label = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, alignment = TextAnchor.MiddleLeft };
        input = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 23, padding = new RectOffset(12, 8, 4, 4) };
        shade = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(.055f, .11f, .095f, Mathf.Lerp(.95f, .08f, i / 63f));
        shade.SetPixels(pixels); shade.Apply();
    }

    static void Block(Rect area, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    void Words(float x, float y, float w, float h, string value, int size, Color color, bool bold = false)
    {
        label.fontSize = size;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.normal.textColor = color;
        GUI.Label(new Rect(x, y, w, h), value, label);
    }

    bool Action(float x, float y, float w, float h, string value, bool primary = false, bool selected = false)
    {
        Rect r = new Rect(x, y, w, h);
        bool hover = r.Contains(Event.current.mousePosition);
        Block(r, primary || selected ? (hover ? Paper : Gold) : hover ? Pine : Ink);
        if (!primary && !selected) Block(new Rect(x, y + h - 1, w, 1), Pine);
        Words(x + 14, y + 1, w - 22, h - 2, value, h <= 34 ? 15 : 19, primary || selected ? Ink : Paper, primary);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    void OnGUI()
    {
        if (loading) return;
        SetupGui();
        var matrix = GUI.matrix;
        var skinFont = GUI.skin.font;
        int depth = GUI.depth;
        GUI.depth = -900;
        GUI.skin.font = font;
        bool overlay = page != Page.Play && page != Page.Character;
        if (overlay) Block(new Rect(0, 0, Screen.width, Screen.height), new Color(.05f, .105f, .085f, page == Page.Title ? .18f : .63f));
        float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 700f);
        Vector3 offset = page == Page.Character ? Vector3.zero :
            new Vector3((Screen.width - 1100f * scale) / 2f, (Screen.height - 700f * scale) / 2f, 0);
        GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
        if (page == Page.Cultivate) CultivationWindow.Draw();
        else if (page == Page.Character) DrawCharacter(Screen.width / scale, Screen.height / scale);
        else if (page == Page.Play) WorldButtons();
        else if (page == Page.Title) Title();
        else if (page == Page.Story || page == Page.Sandbox) CharacterPage(page == Page.Sandbox);
        else if (page == Page.Confirm) ConfirmNew();
        else if (page == Page.Epitaph) Epitaph();
        else GuideOrMenu();
        GUI.skin.font = skinFont;
        GUI.matrix = matrix;
        GUI.depth = depth;
    }

    void WorldButtons()
    {
        if (!FreeToOpen || !walker || !walker.CanUseExplorationInput) return;
        if (Action(568, 15, 112, 34, "行囊  B")) OpenCharacter(true);
        if (Action(688, 15, 134, 34, "人物志  C")) OpenCharacter();
        if (Action(830, 15, 134, 34, "修行簿  F1")) OpenCultivation();
        if (Action(970, 15, 108, 34, "卷册  F2")) OpenMenu();
        if (AtGate && Action(830, 58, 248, 40, "从东门启程  M", true)) DemoRegion.Depart();
    }

    void Title()
    {
        GUI.DrawTexture(new Rect(0, 0, 1100, 700), shade);
        Words(68, 310, 600, 58, "青 岚 山 境", 27, Paper);
        Words(62, 355, 640, 133, "寻长生", 94, Paper, true);
        Block(new Rect(71, 518, 52, 3), Gold);
        Words(68, 536, 540, 66, "第一卷　从青溪一介凡人，到亲手筑下道基。", 20, Paper);
        Block(new Rect(678, 186, 348, 347), new Color(.08f, .15f, .13f, .96f));
        Words(710, 207, 280, 48, "启 卷", 28, Paper, true);
        if (Action(710, 281, 284, 48, "剧情　·　药铺学徒    1", true)) Show(Page.Story);
        if (Action(710, 345, 284, 48, "沙盒　·　自创修士    2")) Show(Page.Sandbox);
        if (Active || HasSlot(false) || HasSlot(true))
        {
            if (Action(710, 423, 284, 40, "继续上次游历   Enter")) ContinueLast();
        }
        else Words(711, 410, 280, 72, "两种开局分别存档，途中自动记录。", 17, Soft);
        if (!Application.isEditor && Action(710, 479, 124, 31, "离开游戏")) Application.Quit();
    }

    void CharacterPage(bool sandbox)
    {
        Block(new Rect(106, 65, 888, 566), Ink);
        Block(new Rect(106, 65, 9, 566), Gold);
        Words(154, 90, 690, 65, sandbox ? "沙盒 · 自立身世" : "剧情 · 青溪药徒", 38, Paper, true);
        if (Action(840, 98, 112, 34, "返回  Esc")) Show(Page.Title);
        bool exists = HasSlot(sandbox);
        if (sandbox)
        {
            Words(155, 171, 230, 38, "姓名", 19, Soft);
            GUI.SetNextControlName("修士姓名");
            createName = GUI.TextField(new Rect(307, 171, 373, 40), createName, 12, input);
            editingName = GUI.GetNameOfFocusedControl() == "修士姓名";
            Words(155, 242, 150, 42, "灵根", 19, Soft);
            if (Action(309, 241, 209, 44, "金木水火土 · 杂灵根", false, !createSingle)) createSingle = false;
            if (Action(532, 241, 208, 44, "木 · 天灵根", false, createSingle)) createSingle = true;
            Words(155, 320, 150, 40, "出身", 19, Soft);
            if (Action(309, 318, 209, 44, "药铺寄学", false, !createTraveller)) createTraveller = false;
            if (Action(532, 318, 208, 44, "游方散修", false, createTraveller)) createTraveller = true;
            Words(155, 389, 724, 76, createTraveller ?
                "已初感灵气，携两块灵石，从青溪东门出发。可以直接访宗门，也可以在山脚接险差。" :
                "携三块灵石，在药铺落脚。从辨药、卖药开始，自己换取第一次修习的机会。", 20, Paper);
        }
        else
        {
            Words(155, 181, 700, 70, "沈砚　｜　十六岁　｜　五行杂灵根", 25, Gold);
            Words(155, 271, 730, 147, "你替青溪镇的掌柜分拣干药。铺里收过一种青纹草，药谱上只抄了残缺的几句。今天可以自己出镇，认一回鲜草。", 24, Paper);
            Words(155, 419, 740, 45, "何时入山、求谁指路、要不要拜入山门，都由你自己安排。", 18, Soft);
        }
        if (exists)
        {
            if (Action(155, 515, 280, 54, "继续这一卷", true)) SwitchTo(ReadSlot(sandbox));
            if (Action(451, 515, 222, 54, "重新开卷")) RequestNew(sandbox);
        }
        else
        {
            if (Action(155, 515, 280, 54, sandbox ? "以此身世启程" : "从青溪药铺启程", true)) RequestNew(sandbox);
            if (!sandbox && LegacyJournal().Length > 0 && Action(452, 515, 280, 54, "接续旧版学徒手记")) Begin(false, true);
        }
    }

    void ConfirmNew()
    {
        Block(new Rect(243, 191, 614, 312), Ink);
        Words(283, 222, 527, 57, "重开这一卷？", 31, Paper, true);
        Words(283, 289, 520, 83, "会替换此模式已有的人物、手记、行程与修为。另一种模式的存档独立保留。", 19, Soft);
        if (Action(283, 407, 223, 48, "保留旧档", true)) Show(replaceSandbox ? Page.Sandbox : Page.Story);
        if (Action(524, 407, 252, 48, "替换并重新开始")) Begin(replaceSandbox, false);
    }

    void Epitaph()
    {
        Block(new Rect(210, 145, 680, 401), Ink);
        Words(255, 178, 570, 72, "青岚遗事", 43, Paper, true);
        Words(257, 268, 566, 48, $"{Name}　·　{State.Age} 岁　·　{State.Realm}", 21, Gold);
        Words(257, 328, 566, 97, character.death, 22, Paper);
        if (Action(258, 450, 263, 47, "归卷　Enter", true)) Show(Page.Title);
    }

    void GuideOrMenu()
    {
        Block(new Rect(112, 91, 876, 512), Ink);
        Words(152, 117, 732, 59, Name + "　·　" + (Sensed ? State.Realm : "凡人"), 33, Paper, true);
        Words(154, 187, 760, 42, (Sandbox ? "沙盒" : "剧情") + "　｜　" + (IsDisciple ? "栖霞外门" : "青溪散人") + "　｜　" + (SingleRoot ? "木天灵根" : "五行杂灵根") + "　｜　灵石 " + State.Stones + "　｜　" + State.Age + " 岁", 18, Gold);
        Words(154, 253, 760, 92, NextStep(), 23, Paper);
        Words(154, 361, 776, 96, "WASD / 方向键行走，E 就近互动，J 翻草木手记。东门 M 去舆图，F1 打开修行簿。C 查人物与所学，B 查行囊。初感灵气后可找山脚巡山弟子谈护药委托。", 18, Soft);
        if (Action(154, 495, 241, 49, "回到游历  Esc", true)) CloseWindow();
        if (page == Page.Menu && Action(416, 495, 253, 49, "保存并返回卷首")) { Save(); Show(Page.Title); }
        if (Action(693, 495, 222, 49, "翻开人物志")) OpenCharacter();
    }
}
