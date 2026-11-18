using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class DemoCharacter
{
    public string id, name, regionData, fieldData, ambushData, death, place;
    public bool sandbox, singleRoot, wanderer, regional, sensed, trailKnown, disciple;
    public float x, y;
    public int area, herbalShifts;
    public DemoCultivation cultivation = new DemoCultivation();
}

[DefaultExecutionOrder(-160)]
public sealed partial class DemoRun : MonoBehaviour
{
    public const string TownScene = "QingxiTrail";
    public const string RegionScene = "SampleScene";
    const string Archive = "Qinglan.FirstVolume.1.";
    enum Page { Title, Story, Sandbox, Confirm, Play, Guide, Cultivate, Menu, Epitaph }
    static DemoRun host;
    static DemoCharacter character;
    Page page;
    bool loading, createTraveller = true, createSingle, replaceSandbox;
    int releaseFrame;
    string createName = "云舒";
    string legacyJournal;
    QingxiWalker walker;
    HerbalStudyController apprentice;
    QingxiSparring field;
    Collider2D gate;
    Vector2 lastFeet, writtenFeet;
    float nextSave;
    readonly List<Canvas> hidden = new List<Canvas>();

    public static bool Active => character != null;
    public static bool Modal => host && (host.page != Page.Play || host.loading || Time.frameCount == host.releaseFrame);
    public static bool Sandbox => Active && character.sandbox;
    public static bool SingleRoot => Active && character.singleRoot;
    public static bool Sensed => Active && character.sensed;
    public static bool Wanderer => Active && character.wanderer;
    public static bool IsDisciple => Active && character.disciple;
    public static string Name => Active ? character.name : "沈砚";
    public static DemoCultivation State => character?.cultivation;
    public static bool InSect => Active && character.regional && DemoRegion.InSect;
    public static bool AtPass => Active && character.regional && DemoRegion.AtPass;
    public static bool InTown => Active && (character.regional ? DemoRegion.InTown :
        host.walker && host.walker.CurrentAreaKind <= (int)QingxiArea.Gate);
    public static string RegionData { get => character?.regionData ?? ""; set { if (Active) character.regionData = value; } }
    public static string FieldData { get => character?.fieldData ?? ""; set { if (Active) character.fieldData = value; } }
    public static string AmbushData { get => character?.ambushData ?? ""; set { if (Active) character.ambushData = value; } }
    public static bool Slain => Active && !string.IsNullOrEmpty(character.death);
    public static bool SpendStones(int amount) => Active && !Slain && State.Spend(amount);
    public static void EndLife(string cause)
    {
        if (!Active || Slain) return;
        character.death = string.IsNullOrEmpty(cause) ? "命殒青岚山境。" : cause;
        Save();
        host.Show(Page.Epitaph);
    }
    public static string Key(string leaf) => Archive + (character?.id ?? "standalone") + "." + leaf;
    internal static string Slot(bool sandbox) => Archive + (sandbox ? "Sandbox" : "Story");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntime() { host = null; character = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (host || SceneManager.GetActiveScene().name != TownScene) return;
        host = new GameObject("青岚山境 · 卷册").AddComponent<DemoRun>();
        DontDestroyOnLoad(host.gameObject);
        SceneManager.sceneLoaded += host.EnteredScene;
        host.BindScene();
        host.Show(Page.Title);
    }

    void BindScene()
    {
        walker = FindAnyObjectByType<QingxiWalker>();
        apprentice = FindAnyObjectByType<HerbalStudyController>();
        field = FindAnyObjectByType<QingxiSparring>();
        gate = null;
        if (!walker) return;
        foreach (var landmark in FindObjectsByType<QingxiLandmark>())
            if (landmark.kind == QingxiArea.Gate) { gate = landmark.GetComponent<Collider2D>(); break; }
        if (Active)
            walker.RestorePosition(new Vector2(character.x, character.y), character.trailKnown, character.area, character.place);
        lastFeet = writtenFeet = walker.transform.position;
    }

    void EnteredScene(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        hidden.Clear();
        BindScene();
        loading = false;
        if (!Active) { Show(Page.Title); return; }
        if (Slain) Show(Page.Epitaph);
        else if (State.InTrial || State.RetreatLeft > 0 || State.Expired)
        {
            CultivationWindow.Open();
            Show(Page.Cultivate);
        }
        else Show(Page.Play);
    }

    static DemoCharacter ReadSlot(bool sandbox)
    {
        string json = PlayerPrefs.GetString(Slot(sandbox), "");
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var item = JsonUtility.FromJson<DemoCharacter>(json);
            return item != null && !string.IsNullOrEmpty(item.id) && item.cultivation != null ? item : null;
        }
        catch (ArgumentException) { return null; }
    }

    string LegacyJournal()
    {
        if (legacyJournal != null) return legacyJournal;
        legacyJournal = PlayerPrefs.GetString(HerbalStudyController.SaveKey, "");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        // The macOS prototype used Unity's original preferences domain. Read only its
        // journal. New characters and both save slots belong to this game's own domain.
        if (legacyJournal.Length == 0)
        {
            foreach (string domain in new[] { "com.Unity-Technologies.com.unity.template.urp-blank", "unity.DefaultCompany.Seek_Immortality" })
            {
                var info = new System.Diagnostics.ProcessStartInfo("/usr/bin/defaults", "read " + domain + " " + HerbalStudyController.SaveKey)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                };
                try
                {
                    using (var process = System.Diagnostics.Process.Start(info))
                    {
                        string value = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();
                        if (process.ExitCode != 0 || !value.StartsWith("{", StringComparison.Ordinal)) continue;
                        legacyJournal = value;
                        break;
                    }
                }
                catch (System.ComponentModel.Win32Exception) { break; }
            }
        }
#endif
        return legacyJournal;
    }

    void Begin(bool sandbox, bool import)
    {
        var previous = ReadSlot(sandbox);
        var item = new DemoCharacter {
            id = Guid.NewGuid().ToString("N"), sandbox = sandbox,
            name = sandbox ? createName.Trim() : "沈砚", singleRoot = sandbox && createSingle,
            wanderer = sandbox && createTraveller, place = "青溪镇"
        };
        if (item.name.Length == 0) item.name = "无名";
        if (item.wanderer)
        {
            item.sensed = true;
            item.x = 13f;
            item.y = 1.25f;
            item.place = "青溪东门";
            item.area = (int)QingxiArea.Gate;
        }
        if (sandbox) item.cultivation.Earn(item.wanderer ? 2 : 3);
        string journal = import ? LegacyJournal() : "";
        if (journal.Length > 0)
        {
            var old = HerbalStudy.Restore(journal);
            item.sensed = old.FirstSense;
            item.herbalShifts = old.ElapsedShifts;
            item.cultivation.AddTravelMinutes(item.herbalShifts * 120d);
            PlayerPrefs.SetString(Archive + item.id + ".herbal", journal);
        }
        SwitchTo(item);
        if (previous != null) PlayerPrefs.DeleteKey(Archive + previous.id + ".herbal");
        PlayerPrefs.Save();
    }

    void SwitchTo(DemoCharacter next)
    {
        if (next == null || loading) return;
        if (Active) { DemoRegion.Flush(); Save(); }
        loading = true;
        character = next;
        BrigandTerms.ResolveInterruptedDuel();
        PlayerPrefs.SetInt(Archive + "LastSlot", next.sandbox ? 1 : 0);
        Save();
        SceneManager.LoadScene(next.regional ? RegionScene : TownScene);
    }

    public static void RecordHerbs(string key, HerbalStudy journal)
    {
        if (!Active || key != Key("herbal")) return;
        character.sensed |= journal.FirstSense;
        int newShifts = journal.ElapsedShifts - character.herbalShifts;
        if (newShifts > 0) State.AddTravelMinutes(newShifts * 120d);
        character.herbalShifts = journal.ElapsedShifts;
        Save();
    }

    public static void Save()
    {
        if (!Active) return;
        if (host && !host.loading)
        {
            if (character.regional) DemoRegion.Capture();
            else if (host.walker)
            {
                Vector2 p = host.walker.transform.position;
                character.x = p.x;
                character.y = p.y;
                character.area = host.walker.CurrentAreaKind;
                character.place = host.walker.CurrentPlace;
                character.trailKnown |= host.walker.TrailDiscovered;
                host.writtenFeet = p;
            }
        }
        PlayerPrefs.SetString(Slot(Sandbox), JsonUtility.ToJson(character));
        PlayerPrefs.Save();
    }

    public static void MarkRegion()
    {
        if (!Active || host.loading) return;
        Save();
        host.loading = true;
        character.regional = true;
        Save();
    }

    public static void ReturnTown()
    {
        if (!Active || host.loading) return;
        DemoRegion.Flush();
        host.loading = true;
        character.regional = false;
        character.x = 13f; character.y = 1.25f;
        character.area = (int)QingxiArea.Gate;
        character.place = "青溪东门";
        Save();
        SceneManager.LoadScene(TownScene);
    }

    bool FreeToOpen => Active && !loading && page == Page.Play && !DemoRegion.Busy &&
        (!apprentice || apprentice.Panel == HerbPanel.Closed) && (!field || (!field.InMatch && !field.InEncounterOffer));
    bool AtGate => walker && gate &&
        (gate.ClosestPoint(walker.transform.position) - (Vector2)walker.transform.position).sqrMagnitude <= 0.09f;
    public static void OpenCultivation()
    {
        if (!host || !host.FreeToOpen) return;
        CultivationWindow.Open();
        host.Show(Sensed ? Page.Cultivate : Page.Guide);
    }
    public static void OpenMenu() { if (host && host.FreeToOpen) host.Show(Page.Menu); }
    public static void CloseWindow() { if (host && Active) { Save(); host.Show(Slain || State.Expired ? Page.Title : Page.Play); } }

    public static bool JoinSect()
    {
        if (!InSect || !Sensed || IsDisciple || !State.Manual || !State.Free || State.Foundation) return false;
        character.disciple = true;
        State.AddTravelMinutes(30d * 1440d);
        State.Earn(2);
        Save();
        return true;
    }

    void Show(Page next)
    {
        if (next == Page.Play)
        {
            foreach (var canvas in hidden) if (canvas) canvas.enabled = true;
            hidden.Clear();
            releaseFrame = Time.frameCount;
        }
        else if (hidden.Count == 0)
            foreach (var canvas in FindObjectsByType<Canvas>())
                if (canvas.enabled) { hidden.Add(canvas); canvas.enabled = false; }
        page = next;
    }

    void Update()
    {
        if (loading) return;
        var keys = Keyboard.current;
        if (keys == null) return;
        if (page != Page.Play) { MenuKeys(keys); return; }
        if (!FreeToOpen || Modal) return;
        if (State.Expired) { OpenCultivation(); return; }
        if (keys.f1Key.wasPressedThisFrame) OpenCultivation();
        else if (keys.f2Key.wasPressedThisFrame) OpenMenu();
        else if (keys.mKey.wasPressedThisFrame && AtGate) DemoRegion.Depart();
    }

    void LateUpdate()
    {
        if (!Active || loading || !walker || character.regional) return;
        Vector2 feet = walker.transform.position;
        float distance = Vector2.Distance(feet, lastFeet);
        lastFeet = feet;
        if (page != Page.Play) return;
        if (distance > 0 && distance < 2f) State.AddTravelMinutes(distance * 1.5);
        if (Time.unscaledTime < nextSave || (feet - writtenFeet).sqrMagnitude < 0.04f) return;
        nextSave = Time.unscaledTime + 1.5f;
        Save();
    }

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() => Save();
    void OnDestroy()
    {
        if (host != this) return;
        SceneManager.sceneLoaded -= EnteredScene;
        if (shade) Destroy(shade);
        host = null;
    }
}
