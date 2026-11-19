using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class QingxiSparring
{
    QingxiBattlefield practiceTerrain;
    SparLoadout activeLoadout;
    SparLoadout[] options;
    int practiceIndex, chosenIndex;
    CanvasGroup practiceHudGroup;
    bool wasInteractable, wasRaycastTarget;

    public bool PracticeMenuOpen { get; private set; }
    public string CurrentLoadoutName => activeLoadout?.Name;

    public void TogglePracticeMenu()
    {
        if (practiceTerrain == null || !InMatch || Match.InField || JournalOpen || DemoRun.Modal) return;
        SetPracticeMenu(!PracticeMenuOpen);
    }

    void SetPracticeMenu(bool open)
    {
        if (PracticeMenuOpen == open) return;
        PracticeMenuOpen = open;
        board.PlaybackPaused = open || confirmingYield;
        if (open)
        {
            chosenIndex = practiceIndex;
            options = SparLoadout.PracticeOptions();
            if (!practiceHudGroup && !hud.TryGetComponent(out practiceHudGroup))
                practiceHudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            wasInteractable = practiceHudGroup.interactable;
            wasRaycastTarget = practiceHudGroup.blocksRaycasts;
            practiceHudGroup.interactable = false;
            practiceHudGroup.blocksRaycasts = false;
        }
        else
        {
            if (practiceHudGroup)
            {
                practiceHudGroup.interactable = wasInteractable;
                practiceHudGroup.blocksRaycasts = wasRaycastTarget;
            }
            nextOpponentAction = Time.unscaledTimeAsDouble + opponentInterval;
            options = null;
        }
    }

    bool HandlePracticeMenu(Keyboard keys)
    {
        if (practiceTerrain == null) return false;
        if (keys != null && keys.pKey.wasPressedThisFrame) { TogglePracticeMenu(); return true; }
        if (!PracticeMenuOpen) return false;
        if (keys == null) return true;
        if (keys.escapeKey.wasPressedThisFrame) SetPracticeMenu(false);
        else if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) RestartPractice(chosenIndex);
        else
        {
            for (int i = 0; i < Mathf.Min(9, options.Length); i++)
                if (keys[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { chosenIndex = i; break; }
        }
        return true;
    }

    public bool RestartPractice(int index)
    {
        if (!PracticeMenuOpen || practiceTerrain == null || index < 0 || index >= options.Length) return false;
        var loadout = options[index];
        var fresh = practiceTerrain.ReplayPractice(loadout.GearAttack, loadout.GearHealth);
        if (fresh == null) return false;
        fresh.DeferTurnOpening = true;
        loadout.ApplyTo(fresh);
        Match = fresh;
        board.ClearActionCues();
        activeLoadout = loadout;
        practiceIndex = index;
        confirmingYield = false;
        feedback = loadout.Name + "　·　" + loadout.Equipment;
        SetPracticeMenu(false);
        SyncActor();
        ResetBattleFocus();
        Refresh();
        return true;
    }

    void DrawPracticeMenu()
    {
        GUI.color = new Color32(24, 44, 37, 255);
        GUI.DrawTexture(new Rect(27, 22, 796, 502), Texture2D.whiteTexture);
        GUI.color = new Color32(189, 158, 94, 255);
        GUI.DrawTexture(new Rect(27, 22, 796, 3), Texture2D.whiteTexture);
        GUI.color = new Color32(228, 200, 138, 255);
        GUI.Label(new Rect(57, 40, 666, 53), "同场切磋 · 配招重试", fieldTitle);
        GUI.color = Color.white;
        GUI.Label(new Rect(58, 99, 730, 44), "重开复原站位、法力和携带物。选择「本人所学」可试用自己的装备。", fieldBody);
        for (int i = 0; i < options.Length; i++)
        {
            var kit = options[i];
            string caption = $"{i + 1}　{kit.Name}　　 {kit.Equipment}";
            if (PracticeButton(new Rect(57, 144 + i * 45, 736, 38), caption, chosenIndex == i)) chosenIndex = i;
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(59, 365, 727, 58), options[chosenIndex].Tactic, fieldBody);
        if (PracticeButton(new Rect(58, 443, 340, 48), "按此搭配从头切磋　Enter", true)) RestartPractice(chosenIndex);
        else if (PracticeButton(new Rect(421, 443, 299, 48), "回到当前对局　Esc", false)) SetPracticeMenu(false);
    }

    bool PracticeButton(Rect area, string caption, bool highlighted)
    {
        bool hover = area.Contains(Event.current.mousePosition);
        GUI.color = highlighted ? new Color32(227, 199, 143, 255) :
            hover ? new Color32(64, 91, 76, 255) : new Color32(39, 63, 52, 255);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = highlighted ? new Color32(23, 39, 32, 255) : new Color32(234, 226, 208, 255);
        GUI.Label(new Rect(area.x + 12, area.y + (area.height - 28) / 2, area.width - 21, 30), caption, fieldBody);
        return GUI.Button(area, GUIContent.none, GUIStyle.none);
    }
}
