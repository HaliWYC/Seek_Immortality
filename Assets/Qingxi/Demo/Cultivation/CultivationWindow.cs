using UnityEngine;
using UnityEngine.InputSystem;

public static class CultivationWindow
{
    enum Leaf { Practice, Supplies, Friends, Foundation }
    static Leaf leaf;
    static int element, dose;
    static bool suppress, listen = true, confirmAbort, readEnding, roster;
    static DemoCultivation current;
    static Font font;
    static readonly Color Ink = new Color32(43, 52, 43, 255);
    static readonly Color Faint = new Color32(101, 102, 80, 255);
    static readonly Color Paper = new Color32(237, 229, 202, 255);
    static readonly Color Field = new Color32(224, 216, 187, 255);
    static readonly Color Pine = new Color32(49, 79, 64, 255);
    static readonly Color Rust = new Color32(141, 76, 50, 255);
    static readonly Color Light = new Color32(247, 239, 213, 255);
    static readonly Color[] ElementColors = {
        new Color32(67, 108, 78, 255), new Color32(156, 75, 60, 255), new Color32(153, 119, 64, 255),
        new Color32(109, 111, 107, 255), new Color32(69, 101, 127, 255)
    };

    static DemoCultivation S => DemoRun.State;
    static bool QuietPlace => DemoRun.InTown || DemoRun.InSect;
    static string Place => DemoRun.InSect ? "栖霞宗" : DemoRun.AtPass ? "松风隘" : DemoRun.InTown ? "青溪镇" : "山野途中";

    public static void Open()
    {
        current = S; confirmAbort = false; element = 0; dose = 0; suppress = false;
        readEnding = false; roster = false; listen = S == null || S.ReceiveLetters;
        leaf = DemoRun.AtPass && S != null && S.Manual ? Leaf.Supplies : Leaf.Practice;
    }

    public static void Escape()
    {
        if (S != null && S.InTrial) { confirmAbort = !confirmAbort; return; }
        if (S != null && (S.ReadingLetter || S.RetreatLeft > 0)) { S.EndRetreat(); DemoRun.Save(); }
        DemoRun.CloseWindow();
    }

    public static void KeyboardInput(Keyboard k)
    {
        if (S == null || k == null) return;
        bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
        if (S.InTrial)
        {
            if (confirmAbort)
            {
                if (k.yKey.wasPressedThisFrame) { Apply(S.AbortTrial()); confirmAbort = false; leaf = Leaf.Foundation; }
                else if (enter) confirmAbort = false;
                return;
            }
            if (k.leftArrowKey.wasPressedThisFrame) element = (element + 4) % 5;
            if (k.rightArrowKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame) element = (element + 1) % 5;
            if (k.gKey.wasPressedThisFrame) Apply(S.ConvertTo(element));
            else if (k.spaceKey.wasPressedThisFrame) Apply(S.Hold());
            else if (enter) Apply(S.Condense(element, S.Spirit(element) >= 5));
            return;
        }
        if (S.Expired || (S.Foundation && !readEnding)) { if (enter) DemoRun.CloseWindow(); return; }
        if (!DemoRun.Sensed) return;
        if (S.ReadingLetter)
        {
            if (enter) { S.AnswerLetter(false); DemoRun.Save(); }
            return;
        }
        if (S.RetreatLeft > 0) { if (enter) Apply(S.TrainSeason()); return; }
        if (k.digit1Key.wasPressedThisFrame) leaf = Leaf.Practice;
        else if (k.digit2Key.wasPressedThisFrame) leaf = Leaf.Supplies;
        else if (k.digit3Key.wasPressedThisFrame) leaf = Leaf.Friends;
        else if (k.digit4Key.wasPressedThisFrame) leaf = Leaf.Foundation;
        if (leaf == Leaf.Foundation)
        {
            if (k.rightArrowKey.wasPressedThisFrame) dose = Mathf.Min(dose + 1, Mathf.Min(S.Pills, 3));
            if (k.leftArrowKey.wasPressedThisFrame) dose = Mathf.Max(0, dose - 1);
            if (k.hKey.wasPressedThisFrame && S.Dew && S.Injuries > 0) suppress = !suppress;
            if (enter && QuietPlace && S.StartTrial(DemoRun.SingleRoot, dose, suppress))
            { element = 0; confirmAbort = false; DemoRun.Save(); }
        }
        else if (leaf == Leaf.Practice && enter)
        {
            if (!S.Manual && DemoRun.InSect) Apply(S.AcceptManual());
            else if (QuietPlace) Apply(S.StartRetreat(k.shiftKey.isPressed ? 3 : 1, listen));
        }
        else if (leaf == Leaf.Supplies && enter)
        {
            if (DemoRun.AtPass) Apply(S.Gather());
            else if (QuietPlace) Apply(S.Work());
        }
        else if (leaf == Leaf.Friends && enter && DemoRun.InSect)
        {
            if (!S.Pact) Apply(S.TakePact());
            else Apply(S.DeliverPact());
        }
    }

    public static void Draw()
    {
        if (S == null) return;
        if (!ReferenceEquals(current, S)) Open();
        if (!font) font = Resources.Load<Font>("NotoSansSC-Regular");
        var oldColor = GUI.color;
        GUI.color = Color.white;
        Fill(0, 0, 1100, 700, new Color(.07f, .12f, .1f, .85f));
        Fill(40, 30, 1020, 640, Paper);
        Fill(40, 30, 1020, 81, Pine);
        Label(70, 39, 650, 35, S.InTrial ? "筑基 · 七转归元" : "修行簿", 29, Light, true);
        string age = S.Foundation ? $"{S.Age} 岁" : $"{S.Age} 岁 / 寿限 80";
        Label(72, 78, 810, 22, $"{DemoRun.Name}    ·    {Place}    ·    {(DemoRun.Sensed ? S.Realm : "凡人")}    ·    {age}    ·    灵石 {S.Stones}", 16, Light);
        if (Button(898, 49, 135, 39, S.InTrial ? "强行出关" : "收卷  Esc", true, false)) Escape();
        if (S.InTrial) DrawTrial();
        else if (S.Expired || (S.Foundation && !readEnding)) DrawEnding();
        else if (!DemoRun.Sensed) DrawBeforeSense();
        else if (S.RetreatLeft > 0 || S.ReadingLetter) DrawRetreat();
        else DrawBook();
        GUI.color = oldColor;
    }

    static void DrawBeforeSense()
    {
        Label(84, 165, 900, 48, "凡人求道", 32, Ink, true);
        Label(84, 233, 852, 150, "先在青溪镇办妥药铺的差事，再向掌柜借《听息诀》。在药铺东侧静席辨出第一缕灵气之后，才可亲往栖霞山门求取炼气法门。", 23, Ink);
        if (Button(84, 488, 250, 50, "收起修行簿")) DemoRun.CloseWindow();
    }

    static void DrawBook()
    {
        string[] names = { "1   行功与境界", "2   丹房与寻材", "3   山友往还", "4   准备筑基" };
        for (int i = 0; i < names.Length; i++)
            if (Button(68 + i * 240, 129, 232, 42, names[i], true, (int)leaf == i)) leaf = (Leaf)i;
        switch (leaf)
        {
            case Leaf.Practice: DrawPractice(); break;
            case Leaf.Supplies: DrawSupply(); break;
            case Leaf.Friends: DrawFriends(); break;
            case Leaf.Foundation: DrawPreparation(); break;
        }
        Fill(70, 589, 958, 1, Faint);
        Label(72, 599, 952, 55, S.Note.Length == 0 ? "宗门向有缘修士借阅《归元篇》，散修也可上山求法。" : S.Note, 17, Pine);
    }

    static void DrawPractice()
    {
        Label(80, 195, 680, 42, S.Foundation ? S.QualityName : S.Manual ? "归元篇 · 行功" : "初识灵气，山门求法", 28, Ink, true);
        if (!S.Manual)
        {
            Label(80, 252, 880, 100, "栖霞宗山门存有一册《归元篇》。从青溪镇出发，沿官道或山径抵达山门，向知客借卷。宗门准许散修抄录，不收束脩。", 21, Ink);
            if (Button(80, 399, 320, 52, DemoRun.InSect ? "向知客借《归元篇》   Enter" : "到栖霞宗山门借卷", DemoRun.InSect, true))
                Apply(S.AcceptManual());
            return;
        }
        Label(80, 248, 600, 30, $"炼气修为   {S.Qi} / {DemoCultivation.Bottleneck}     ·     岁历 {S.Days} 日", 20, Ink);
        Meter(82, 299, 887, S.Qi, DemoCultivation.Bottleneck, Pine);
        string info = S.Foundation ? "筑基已成。山路仍可再走，故人也可重访。" :
            S.Qi == DemoCultivation.Bottleneck ? "气海抵达瓶颈。可到丹房备药、向名医学护脉，再选冲关的时日。" :
            "在镇中静处或宗门静室，每坐满一季，历时九十日，累积三份修为。炼气中期开始可御剑赶路。";
        Label(80, 325, 890, 68, info, 19, Ink);
        bool train = QuietPlace && !S.Foundation && S.Qi < DemoCultivation.Bottleneck;
        if (Button(80, 412, 228, 51, "闭关一季", train, true)) Apply(S.StartRetreat(1, listen));
        if (Button(324, 412, 228, 51, "闭关至多三季", train)) Apply(S.StartRetreat(3, listen));
        if (Button(569, 412, 345, 51, listen ? "姜遥传音   听取" : "姜遥传音   暂屏", train)) listen = !listen;
        if (S.Qi >= DemoCultivation.Bottleneck && Button(80, 488, 272, 48, "翻到冲关页")) leaf = Leaf.Foundation;
        else if (!QuietPlace) Label(80, 491, 880, 45, "此地缺少安稳的静处，可回青溪镇或栖霞宗闭关。", 17, Faint);
    }

    static void DrawRetreat()
    {
        Label(82, 160, 920, 45, S.ReadingLetter ? "洞门之外，有人传音" : "静室周天", 30, Ink, true);
        Label(82, 225, 920, 32, $"已修 {S.Qi} / {DemoCultivation.Bottleneck}    ·    本次尚余 {S.RetreatLeft} 季    ·    已历 {S.Days} 日", 20, Pine);
        Meter(84, 282, 865, S.Qi, DemoCultivation.Bottleneck, Pine);
        if (S.ReadingLetter)
        {
            Label(82, 323, 865, 92, $"姜遥催问隘口药讯，距她离开还有 {S.PactDays} 日。你可以带着已修成的周天出关，或继续这次闭关。", 20, Ink);
            if (Button(82, 459, 302, 54, "收功出关，去见姜遥", true, true)) { S.AnswerLetter(true); DemoRun.Save(); DemoRun.CloseWindow(); }
            if (Button(402, 459, 276, 54, "回音婉拒，继续修习")) { S.AnswerLetter(false); DemoRun.Save(); }
        }
        else
        {
            Label(82, 322, 870, 90, "每行功一季结算九十日。你可以在任何一季结束后出关，修成的周天会留在气海。", 20, Ink);
            if (Button(82, 459, 300, 54, "行功一季   ·   九十日   Enter", true, true)) Apply(S.TrainSeason());
            if (Button(385, 459, 240, 54, "现在收功出关")) { S.EndRetreat(); DemoRun.Save(); DemoRun.CloseWindow(); }
        }
    }

    static void DrawSupply()
    {
        Label(80, 198, 935, 45, "备药与生计", 28, Ink, true);
        Label(80, 248, 944, 46, $"灵石 {S.Stones}     玉髓芝 {S.Herbs}     筑基丹 {S.Pills}     护脉露 {(S.Dew ? 1 : 0)}     旧伤 {S.Injuries} 层", 20, Pine);
        if (DemoRun.AtPass)
        {
            Label(80, 311, 890, 66, "散修在松风隘守芝、凝露。头茬玉髓芝可得两株，被人先取后每次只能找到一株。护脉露须按自己的旧伤调配。", 19, Ink);
            if (Button(80, 406, 298, 52, "守芝十五日", S.Manual, true)) Apply(S.Gather());
            if (Button(395, 406, 320, 52, "采护脉露   ·   三十日", S.Manual && !S.Dew && S.Injuries > 0)) Apply(S.CollectDew());
            return;
        }
        Label(80, 300, 904, 56, QuietPlace ? "药房收炉工，也售冲关丹药。栖霞宗传授五行归流术，松风隘可自行采芝。" :
            "此地正在途中。到镇上或宗门可置办丹药，到松风隘可寻草木与护脉露。", 18, Ink);
        if (Button(80, 379, 286, 50, "照看丹炉一月   +3 灵石", QuietPlace && !S.Foundation, true)) Apply(S.Work());
        if (Button(383, 379, 286, 50, "买筑基丹   4 灵石", QuietPlace && S.Manual && S.Stones >= 4)) Apply(S.BuyPill());
        if (Button(686, 379, 294, 50, "两株芝合丹   十日", QuietPlace && S.Manual && S.Herbs >= 2)) Apply(S.MakePill());
        if (Button(80, 451, 286, 50, "医者配护脉露   3 灵石", QuietPlace && S.Injuries > 0 && !S.Dew && S.Stones >= 3)) Apply(S.SeekDoctor());
        if (Button(383, 451, 370, 50, S.Art ? "已习得五行归流术" : $"山门学归流术   {S.CurrentArtPrice} 灵石", DemoRun.InSect && S.Manual && !S.Art && S.Stones >= S.CurrentArtPrice)) Apply(S.LearnArt());
    }

    static void DrawFriends()
    {
        if (Button(756, 198, 245, 42, roster ? "翻回姜遥的约定" : "翻看山门名簿")) roster = !roster;
        if (roster)
        {
            Label(80, 198, 635, 45, "栖霞宗 · 外门名笺", 28, Ink, true);
            string report = S.CopiedRoll ? $"第 {S.RollDay} 日抄录\n{S.RollEntry(true)}\n{S.RollEntry(false)}" :
                "两位外门弟子近况记在山门值事簿。亲到栖霞宗即可抄录，离开后仍可查看手中这一页。";
            Label(80, 263, 910, 125, report, 20, Ink);
            if (Button(80, 418, 292, 53, "在山门抄录今日名簿", DemoRun.InSect && S.Manual && (!S.CopiedRoll || S.RollDay != S.Days), true)) Apply(S.CopySectRoll());
            string admission = DemoRun.IsDisciple ? "身份　栖霞宗外门弟子" : S.Foundation ? "已筑基，外门招录已过" : "登记外门　当值三十日，得两块灵石";
            if (Button(395, 418, 488, 53, admission, DemoRun.InSect && S.Manual && !S.Foundation && !DemoRun.IsDisciple)) DemoRun.JoinSect();
            Label(80, 486, 900, 52, DemoRun.IsDisciple ? "门中已为你另立名籍。" : "若愿继续做散修，可直接收卷。山门不催你入籍。", 17, Faint);
            return;
        }
        Label(80, 198, 645, 45, "姜遥 · 寻药散修", 28, Ink, true);
        Label(80, 254, 918, 100, S.FellowNote, 21, Ink);
        Label(80, 374, 905, 58, S.Fulfilled ? "交情   守信之交" : "约定   相识后一百八十日内，带一株玉髓芝回栖霞山门，一枚筑基丹、两块灵石作酬。", 18, Pine);
        if (Button(80, 465, 295, 50, "山门应下寻芝之约", DemoRun.InSect && S.CanAcceptPact)) Apply(S.TakePact());
        if (Button(394, 465, 290, 50, "山门交芝，按约换丹", DemoRun.InSect && S.CanDeliverPact, true)) Apply(S.DeliverPact());
    }

    static void DrawPreparation()
    {
        dose = Mathf.Clamp(dose, 0, Mathf.Min(3, S.Pills)); suppress &= S.Dew && S.Injuries > 0;
        Label(80, 194, 920, 43, "七转凝基", 28, Ink, true);
        if (S.Foundation)
        {
            Label(80, 261, 870, 94, $"筑基初期，{S.QualityName}。七转共凝 {S.Force} 份，第 {S.FormedOn} 转初定。", 22, Pine);
            if (Button(80, 414, 260, 52, "重阅筑基记")) readEnding = false;
            return;
        }
        Label(80, 244, 938, 88, "七转之内积满十二份道基。每转吸入四缕灵力，旧伤每层少一缕，至少得一缕。三缕同气凝二份，五缕同气凝四份。达到门槛后可用余转继续温养。", 19, Ink);
        Label(80, 336, 940, 49, $"修为 {S.Qi}/12   ·   每枚丹药开局添两缕木气，至多三枚   ·   本次旧伤 {(suppress ? 0 : S.Injuries)} 层", 18, Pine);
        if (Button(80, 400, 126, 44, "少服一枚", dose > 0)) dose--;
        Label(218, 401, 205, 43, $"拟服 {dose} / 现有 {S.Pills}", 18, Ink);
        if (Button(416, 400, 126, 44, "多服一枚", dose < Mathf.Min(3, S.Pills))) dose++;
        if (Button(572, 400, 350, 44, suppress ? "已选护脉露，按本次无伤结算" : "本次加用护脉露", S.Dew && S.Injuries > 0)) suppress = !suppress;
        bool ready = QuietPlace && S.Manual && S.Qi >= DemoCultivation.Bottleneck;
        if (Button(80, 479, 334, 53, ready ? "启关七转，服用所选丹药" : "须在镇 / 山门，且炼气圆满", ready, true))
        {
            if (S.StartTrial(DemoRun.SingleRoot, dose, suppress)) { element = 0; confirmAbort = false; DemoRun.Save(); }
        }
        Label(436, 475, 552, 64, "药一入口即耗去。强行出关与冲关失利均会损去一季修为，旧伤再加一层。", 17, Rust);
    }

    static void DrawTrial()
    {
        if (confirmAbort)
        {
            Label(88, 187, 900, 56, "此时强行出关？", 31, Rust, true);
            Label(88, 274, 862, 100, $"本次用去筑基丹 {S.UsedPills} 枚。强退将损三份修为，旧伤由 {S.Injuries} 层增为 {S.Injuries+1} 层，须重回瓶颈。", 22, Ink);
            if (Button(88, 455, 286, 58, "留在关中", true, true)) confirmAbort = false;
            if (Button(395, 455, 325, 58, "接受损伤，强行出关   Y")) { Apply(S.AbortTrial()); confirmAbort = false; leaf = Leaf.Foundation; }
            return;
        }
        Label(78, 138, 875, 45, $"第 {S.Turn} 转 / 七转     道基 {S.Force} / {DemoCultivation.FoundationGoal}", 28, Ink, true);
        Meter(80, 199, 935, S.Force, 20, Pine);
        Label(80, 225, 942, 52, S.FormedOn > 0 ? $"第 {S.FormedOn} 转已经凝基。继续温养至七转，十六份为敦厚，二十份为澄明。" :
            "点选一脉，以同色灵力凝基。暂存的五行余气会留到下一转。", 19, Pine);
        for (int i = 0; i < 5; i++)
        {
            float x = 80 + 189 * i;
            if (Button(x, 299, 173, 69, $"{DemoCultivation.Elements[i]}      {S.Spirit(i)} 缕", true, i == element)) element = i;
            Fill(x, 366, 173, 5, ElementColors[i]);
        }
        int energy = S.Spirit(element);
        if (Button(80, 405, 278, 53, "小周天   用三缕，凝二份", energy >= 3)) Apply(S.Condense(element, false));
        if (Button(377, 405, 278, 53, "大周天   用五缕，凝四份", energy >= 5, true)) Apply(S.Condense(element, true));
        if (Button(674, 405, 278, 53, "蓄气，守到下一转")) Apply(S.Hold());
        int other = 0;
        for (int i = 0; i < 5; i++) if (i != element) other += S.Spirit(i);
        if (Button(80, 483, 365, 49, S.Art ? $"归流术   导两缕异气入{DemoCultivation.Elements[element]}脉" : "归流术须先向山门修习", S.Art && !S.Converted && other >= 2))
            Apply(S.ConvertTo(element));
        Label(462, 478, 546, 64, S.Art ? "每转可归流一次，不占凝基动作。" : "灵根影响每转所得的五行分布。丹药和术法可在关前自行筹备。", 17, Faint);
        Label(80, 550, 935, 58, S.Note, 17, Pine);
        Label(80, 613, 940, 35, "左右选灵脉   ·   Enter 凝基   ·   空格蓄气   ·   G 归流   ·   Esc 强行出关", 15, Faint);
        if (!S.InTrial) { leaf = Leaf.Foundation; readEnding = false; }
    }

    static void DrawEnding()
    {
        Label(82, 160, 925, 55, S.Expired ? "卷终 · 寿尽归山" : "第一卷终 · 青岚筑基", 32, Ink, true);
        string text = S.Expired ? "八十年期满，寻道的足迹仍留在青岚。你可回到标题，另开一卷。" :
            $"{DemoRun.Name}在第 {S.FormedOn} 转凝起道基，守足七转，筑成{S.QualityName}。旧伤随气海重塑而消。";
        Label(82, 248, 902, 95, text, 23, Pine);
        Label(82, 370, 940, 82, $"道龄所历   {S.Days} 日     ·     启关 {S.Attempts} 次     ·     七转道基 {S.Force}\n山友   {(S.Fulfilled ? "与姜遥守约分丹" : S.Pact ? "曾与姜遥订下寻药之约" : "独自筹措机缘")}", 19, Ink);
        if (!S.Expired && Button(82, 496, 280, 55, "再阅道途", true, true)) { readEnding = true; leaf = Leaf.Practice; }
        if (Button(S.Expired ? 82 : 382, 496, 300, 55, S.Expired ? "归卷   ·   返回卷首" : "收卷   ·   返回探索")) DemoRun.CloseWindow();
    }

    static void Apply(bool changed) { if (changed) DemoRun.Save(); }

    static void Fill(float x, float y, float w, float h, Color color)
    {
        Color previous = GUI.color; GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture); GUI.color = previous;
    }

    static void Meter(float x, float y, float width, int amount, int maximum, Color color)
    {
        Fill(x, y, width, 9, Field);
        Fill(x, y, width * Mathf.Clamp01((float)amount / maximum), 9, color);
    }

    static void Label(float x, float y, float w, float h, string text, int size, Color color, bool bold = false)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) {font = font, fontSize = size, wordWrap = true,
            alignment = TextAnchor.MiddleLeft, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal};
        style.normal.textColor = color;
        GUI.Label(new Rect(x,y,w,h), text, style);
    }

    static bool Button(float x, float y, float w, float h, string label, bool enabled = true, bool primary = false)
    {
        Rect rect = new Rect(x, y, w, h);
        bool hover = enabled && rect.Contains(Event.current.mousePosition);
        Fill(x, y, w, h, enabled ? (primary || hover ? Pine : new Color32(192, 197, 169, 255)) : Field);
        Label(x + 12, y, w - 24, h, label, 17, enabled ? (primary || hover ? Light : Ink) : Faint, primary);
        return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }
}
