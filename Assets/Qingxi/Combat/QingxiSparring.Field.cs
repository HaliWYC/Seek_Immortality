using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class QingxiSparring
{
    public bool InEncounterOffer { get; private set; }
    public bool CanOpenOffer => CanChallenge && DemoRun.Active && DemoRun.Sensed;

    bool fieldSettled;
    Font fieldFont;
    GUIStyle fieldTitle, fieldBody, fieldAction;

    public void OpenFieldOffer()
    {
        if (!CanOpenOffer) return;
        InEncounterOffer = true;
        walker.SetExplorationPaused(true);
        nearby = false;
        hud.SetNearby(false);
        hud.SetFieldNearby(false);
    }

    public void CloseFieldOffer()
    {
        if (!InEncounterOffer) return;
        if (brigandTerms != null) brigandTerms.Escape();
        InEncounterOffer = false;
        brigandTerms = null;
        if (walker) walker.SetExplorationPaused(false);
        if (regionalBody) RegionalPractice.Finish();
    }

    public void AcceptField(FieldParty party)
    {
        if (!InEncounterOffer || !DemoRun.Active || !DemoRun.Sensed || !string.IsNullOrEmpty(DemoRun.FieldData)) return;
        if (party != FieldParty.Ally && party != FieldParty.Contract && party != FieldParty.Solo) return;
        CloseFieldOffer();
        Vector2 beast = (Vector2)transform.position + new Vector2(2.1f, 0.55f);
        StartHere(beast, true, party);
    }

    void HandleOfferInput(Keyboard keys)
    {
        if (keys == null) return;
        if (keys.escapeKey.wasPressedThisFrame || keys.gKey.wasPressedThisFrame) { CloseFieldOffer(); return; }
        if (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) AcceptField(FieldParty.Ally);
        else if (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) AcceptField(FieldParty.Contract);
        else if (keys.digit3Key.wasPressedThisFrame || keys.numpad3Key.wasPressedThisFrame) AcceptField(FieldParty.Solo);
    }

    public void UseFieldHerb()
    {
        if (!InMatch || confirmingYield) return;
        if (Match.Party == FieldParty.Brigands)
        {
            feedback = Match.UseFieldWard(selected) ? "" : "破阵符需由你在本回合用一次施术机会祭出。";
            Refresh();
            return;
        }
        if (Match.Party != FieldParty.Contract) return;
        feedback = Match.UseAgreedHerb(selected) ? "" : "止血散只可由负伤的阿荇用一次，消耗他的施术机会。";
        Refresh();
    }

    public static int FieldPay(SparMatch fight)
    {
        if (!fight.InField || fight.Status != SparStatus.Won) return 0;
        if (fight.Party == FieldParty.Brigands) return BrigandTerms.Bounty;
        bool gift = fight.Companion != null && !fight.CompanionDied && !fight.TalismanUsed;
        return gift ? 4 : 3;
    }

    void RecordFieldOutcome()
    {
        if (fieldSettled || !DemoRun.Active) return;
        if (!Finished)
        {
            // A death already witnessed must survive a restart even before the hunt ends.
            if (Match.CompanionDied && string.IsNullOrEmpty(DemoRun.FieldData))
            {
                DemoRun.FieldData = "阿荇没能走出山林。山脚的护送委托已经结束。";
                DemoRun.Save();
            }
            return;
        }
        fieldSettled = true;
        int pay = FieldPay(Match);
        string result = Match.Message;
        if (pay > 0)
        {
            DemoRun.State.Earn(pay);
            result += pay == 4 ? " 酬三枚灵石，阿荇另谢一枚。" : " 兽蜕兑得三枚灵石。";
        }
        else result += " 此行未领兽蜕酬金。";
        DemoRun.FieldData = result;
        DemoRun.Save();
    }

    void OnGUI()
    {
        if (!InEncounterOffer || DemoRun.Modal) return;
        if (fieldTitle == null)
        {
            fieldFont = Resources.Load<Font>("NotoSansSC-Regular");
            fieldTitle = new GUIStyle(GUI.skin.label) { font = fieldFont, fontSize = 30, fontStyle = FontStyle.Bold };
            fieldBody = new GUIStyle(GUI.skin.label) { font = fieldFont, fontSize = 18, wordWrap = true };
            fieldAction = new GUIStyle(GUI.skin.button) { font = fieldFont, fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 8, 0, 0) };
        }
        Matrix4x4 old = GUI.matrix;
        var oldColor = GUI.color;
        float scale = Mathf.Min(Screen.width / 850f, Screen.height / 550f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 850f * scale) * 0.5f,
            (Screen.height - 550f * scale) * 0.5f), Quaternion.identity, new Vector3(scale, scale, 1));
        GUI.color = new Color32(29, 49, 43, 250);
        if (brigandTerms != null)
        {
            DrawBrigandOffer();
            GUI.color = oldColor;
            GUI.matrix = old;
            return;
        }
        GUI.DrawTexture(new Rect(36, 60, 778, 433), Texture2D.whiteTexture);
        GUI.color = new Color32(222, 192, 125, 255);
        GUI.Label(new Rect(68, 80, 620, 53), "松林护药", fieldTitle);
        GUI.color = Color.white;
        if (GUI.Button(new Rect(684, 85, 96, 38), "离开 Esc", fieldAction)) CloseFieldOffer();
        bool resolved = !string.IsNullOrEmpty(DemoRun.FieldData);
        string detail = resolved ? DemoRun.FieldData :
            "巡山弟子指向林缘。青棘獾蹲守着药篓，采药客阿荇正在等人。驱走妖兽，兽蜕可兑三枚灵石。若阿荇保住了护身符，他会另谢一枚。";
        GUI.Label(new Rect(68, 147, 710, 72), detail, fieldBody);
        if (!resolved)
        {
            GUI.Label(new Rect(68, 221, 714, 60), "此行有性命之忧。阿荇倒地可撑两个我方回合。你或木傀可在相邻格带他撤走，兽蜕留在林中。", fieldBody);
            DrawFieldChoice(290, "1　结伴同行　·　阿荇自行行动，保命符由他掌握", FieldParty.Ally);
            DrawFieldChoice(342, "2　订临时指挥契　·　可指挥他并用一包止血散，保命符自留", FieldParty.Contract);
            DrawFieldChoice(394, "3　让他退到官道　·　只带自己的木傀进林", FieldParty.Solo);
            GUI.Label(new Rect(68, 449, 716, 36), "此地仅有一单。可以稍后再来，开战后撤离也会结案。", fieldBody);
        }
        GUI.color = oldColor;
        GUI.matrix = old;
    }

    void DrawFieldChoice(float y, string label, FieldParty party)
    {
        if (GUI.Button(new Rect(68, y, 711, 43), label, fieldAction)) AcceptField(party);
    }
}
