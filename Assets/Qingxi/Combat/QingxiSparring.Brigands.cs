using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class QingxiSparring
{
    BrigandTerms brigandTerms;
    bool confirmDuel;
    int parleyFrame;
    string parleyNote;
    SpriteRenderer brigandSprite, lookoutSprite;

    void SetBrigandParticipantsVisible(bool visible)
    {
        SetParticipantVisible(brigandSprite, visible);
        SetParticipantVisible(lookoutSprite, visible);
    }

    static void SetParticipantVisible(SpriteRenderer sprite, bool visible)
    {
        if (!sprite) return;
        sprite.enabled = visible;
        if (sprite.TryGetComponent<Collider2D>(out var body)) body.enabled = visible;
    }

    public void OpenBrigandOffer()
    {
        if (!regionalBody || DemoRun.Modal || InMatch || InEncounterOffer || !DemoRun.AtPass || DemoRun.Slain) return;
        brigandTerms = BrigandTerms.Load();
        InEncounterOffer = true;
        confirmDuel = false;
        parleyFrame = Time.frameCount;
        parleyNote = "";
    }

    void HandleBrigandInput(Keyboard keys)
    {
        if (keys == null || parleyFrame == Time.frameCount) return;
        if (keys.escapeKey.wasPressedThisFrame || keys.gKey.wasPressedThisFrame)
        {
            if (confirmDuel) confirmDuel = false; else CloseFieldOffer();
            return;
        }
        if (keys.bKey.wasPressedThisFrame && !confirmDuel) OpenParleyBag();
        else if (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) BrigandChoice(1);
        else if (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) BrigandChoice(2);
        else if (keys.digit3Key.wasPressedThisFrame || keys.numpad3Key.wasPressedThisFrame) BrigandChoice(3);
        else if (keys.digit4Key.wasPressedThisFrame || keys.numpad4Key.wasPressedThisFrame) BrigandChoice(4);
    }

    public void BrigandChoice(int choice)
    {
        if (!InEncounterOffer || brigandTerms == null || brigandTerms.Settled) return;
        if (confirmDuel)
        {
            if (choice == 2) { confirmDuel = false; return; }
            if (choice != 1) return;
            Vector2 opponent = regionalBody.position + new Vector2(2.8f, .45f);
            var gray = regionalBody.transform.parent.Find("灰衣客");
            brigandSprite = gray ? gray.GetComponent<SpriteRenderer>() : null;
            var lookout = regionalBody.transform.parent.Find("望路人");
            lookoutSprite = lookout ? lookout.GetComponent<SpriteRenderer>() : null;
            if (gray) opponent = gray.position;
            InEncounterOffer = false;
            confirmDuel = false;
            // The live actors become units on the sampled terrain. Their former
            // footprints must also move, including for later line-of-sight checks.
            SetBrigandParticipantsVisible(false);
            StartHere(opponent, false, FieldParty.Brigands);
            if (InMatch)
            {
                brigandTerms.fighting = true;
                brigandTerms.Save();
            }
            else
            {
                SetBrigandParticipantsVisible(true);
                InEncounterOffer = true;
                parleyNote = "两旁的岩石太密。可先退开几步，再与他们交涉。";
            }
            return;
        }
        switch (choice)
        {
            case 1: brigandTerms.Examine(); break;
            case 2:
                if (!brigandTerms.PayToll()) parleyNote = "袋中灵石不足，西边石阶仍能退回。";
                else if (!brigandTerms.Settled) parleyNote = "灰衣客接了石头，却冲身后的人使眼色。西面还有一条陡坡能绕行。";
                break;
            case 3:
                if (brigandTerms.wardReady && !brigandTerms.WardPacked) OpenParleyBag();
                else if (brigandTerms.PrepareWard()) parleyNote = "破阵符已收入行囊。按 B 放进战斗携带槽，再决定是否应战。";
                else parleyNote = "制符需要三枚灵石，每场仅可备一张。";
                break;
            case 4: confirmDuel = true; break;
        }
    }

    void OpenParleyBag()
    {
        parleyNote = "";
        DemoRun.OpenEncounterBag();
    }

    void RecordBrigandOutcome()
    {
        if (fieldSettled || brigandTerms == null) return;
        if (Match.WardReleased && !brigandTerms.wardSpent)
        {
            brigandTerms.wardReady = false;
            brigandTerms.wardSpent = true;
            brigandTerms.Save();
        }
        if (!Finished) return;
        fieldSettled = true;
        switch (Match.Status)
        {
            case SparStatus.Yielded:
                brigandTerms.Escape();
                break;
            case SparStatus.Won:
                brigandTerms.fighting = false;
                brigandTerms.result = $"灰衣客与同伙败退。你收得匣中 {BrigandTerms.Bounty} 枚灵石，松风隘重开。";
                DemoRun.State.Earn(BrigandTerms.Bounty);
                brigandTerms.Save();
                break;
            case SparStatus.Lost:
                brigandTerms.fighting = false;
                brigandTerms.result = "你在松风隘与筑基劫修的死斗中陨落。";
                DemoRun.AmbushData = JsonUtility.ToJson(brigandTerms);
                DemoRun.EndLife(brigandTerms.result, showEpitaph: !PlayingAction);
                break;
        }
    }

    void DrawBrigandOffer()
    {
        GUI.DrawTexture(new Rect(36, 37, 778, 480), Texture2D.whiteTexture);
        GUI.color = new Color32(222, 192, 125, 255);
        GUI.Label(new Rect(65, 53, 590, 56), confirmDuel ? "拔剑应战" : "松风隘 · 灰衣客", fieldTitle);
        GUI.color = Color.white;
        if (GUI.Button(new Rect(637, 61, 144, 38), confirmDuel ? "再想想  Esc" : "退回石阶  Esc", fieldAction))
        {
            if (confirmDuel) confirmDuel = false; else CloseFieldOffer();
            return;
        }
        if (brigandTerms.Settled)
        {
            GUI.Label(new Rect(69, 139, 709, 117), brigandTerms.result, fieldBody);
            return;
        }
        if (confirmDuel)
        {
            GUI.Label(new Rect(69, 127, 709, 95), "对方是筑基初期修士。开打之后，你一旦气血耗尽，今生即告终。尚有行动机会时可按 Esc 弃财脱身，折一日、损至多两枚灵石。", fieldBody);
            GUI.Label(new Rect(69, 218, 709, 62), "战中退出，下次读档按弃财脱身结算，已用法力一并记入存档。", fieldBody);
            GUI.Label(new Rect(69, 265, 709, 32), $"以现有法力 {TravelModel.DisplayMana(DemoRun.Mana)}/{DemoRun.ManaCapacity} 入阵。", fieldBody);
            DrawBrigandChoice(308, "1　拔剑入阵，承担生死", 1, true);
            DrawBrigandChoice(366, "2　还剑入鞘，重新谈判", 2, true);
            if (brigandTerms.wardReady && !brigandTerms.WardPacked)
                GUI.Label(new Rect(69, 424, 707, 52), "破阵符还在行囊。可返回谈判，按 B 把它放入携带槽。", fieldBody);
            return;
        }
        string posture = BrigandTerms.CutsOffRoad ? "瘦高个挪到西南岔口，袖口朝着你的背影。石阶外侧还有一线陡坡。"
            : "瘦高个仍守在东口，侧身让开了通往西南石阶的路。";
        string clue = brigandTerms.studied ? posture + " 灰衣客运转护阵时右袖会亮起。朝阵眼祭符可伤十三、破阵并阻他一回合。" :
            "灰衣客拢起长袖，筑基威压隐隐透出。他说：「留两枚灵石，我们便让路。」";
        GUI.Label(new Rect(69, 121, 711, 79), clue, fieldBody);
        GUI.Label(new Rect(69, 202, 710, 53), string.IsNullOrEmpty(parleyNote) ?
            $"行囊 {DemoRun.State.Stones} 灵石　·　" + (brigandTerms.WardPacked ? "携带槽已装破阵符" :
                brigandTerms.wardReady ? "破阵符在行囊，按 B 装入携带槽" : "随时可以退回石阶，择日再过。") : parleyNote, fieldBody);
        DrawBrigandChoice(263, "1　察看同伙的站位与护阵纹路（半个时辰）", 1, !brigandTerms.studied);
        DrawBrigandChoice(315, "2　依言交出两枚灵石，看看他是否放行", 2,
            !brigandTerms.paid && DemoRun.State.Stones >= BrigandTerms.Toll);
        DrawBrigandChoice(367, brigandTerms.WardPacked ? "破阵符已装入携带槽" : brigandTerms.wardReady ?
            "3 / B　打开行囊，装入破阵符" : "3　用三枚灵石备一张破阵符（未识阵眼伤六）", 3,
            !brigandTerms.wardSpent && !brigandTerms.WardPacked && (brigandTerms.wardReady || DemoRun.State.Stones >= BrigandTerms.WardPrice));
        DrawBrigandChoice(419, "4　掂量胜算，准备生死交锋", 4, true);
        GUI.Label(new Rect(69, 472, 710, 36), brigandTerms.paid ? "灵石已交，若折返西坡需绕行一日。" : "酬物或性命由你权衡。先行退开可再备足灵石。", fieldBody);
    }

    void DrawBrigandChoice(float y, string label, int choice, bool enabled)
    {
        bool previous = GUI.enabled;
        GUI.enabled = enabled;
        if (GUI.Button(new Rect(68, y, 711, 42), label, fieldAction)) BrigandChoice(choice);
        GUI.enabled = previous;
    }
}
