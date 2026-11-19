using UnityEngine;
using UnityEngine.SceneManagement;

public static class DemoRegion
{
    internal static TravelPrototype Current { get; private set; }
    public static bool InSect => Current && Current.Place == MapPlace.Sect;
    public static bool AtPass => Current && Current.Place == MapPlace.Pass;
    public static bool InTown => !Current || Current.Place == MapPlace.Town;
    public static bool Busy => Current && (Current.LogOpen || RegionalPractice.Busy);
    internal static float? LiveMana => Current && Current.MatchesCampaign ? Current.Mana : (float?)null;

    internal static void ApplyMana(float mana)
    {
        if (Current && Current.MatchesCampaign) Current.ApplyMana(mana);
    }

    public static void Depart()
    {
        if (!DemoRun.Active || Current) return;
        DemoRun.MarkRegion();
        SceneManager.LoadScene("SampleScene");
    }

    // DemoRun.Save may call Capture before serializing the character's single save record.
    public static void Capture()
    {
        if (Current) Current.CaptureCampaign();
    }

    public static void Flush()
    {
        if (!Current || !Current.MatchesCampaign) return;
        Capture();
        DemoRun.Save();
    }

    internal static void Bind(TravelPrototype current) => Current = current;
    internal static void Release(TravelPrototype current)
    {
        if (Current == current) Current = null;
    }
}
