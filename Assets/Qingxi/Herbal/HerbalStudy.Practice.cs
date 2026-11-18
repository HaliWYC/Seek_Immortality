using UnityEngine;

public enum KeeperCallState { Waiting, Calling, Available, Missed, Paid }

public sealed partial class HerbalStudy
{
    public const int PracticeGoal = 6;
    public const int LessonPrice = 2;
    [SerializeField] int practiceTicks;
    [SerializeField] int practiceProgress;
    [SerializeField] bool taught;
    [SerializeField] bool quietPractice;
    [SerializeField] KeeperCallState keeperCall;

    public int PracticeTicks => practiceTicks;
    public int ElapsedShifts => practiceTicks + (keeperCall == KeeperCallState.Paid ? 1 : 0);
    public int PracticeProgress => practiceProgress;
    public bool FirstSense => practiceProgress >= PracticeGoal;
    public bool Taught => taught;
    public bool ListenForKeeper => !quietPractice;
    public KeeperCallState KeeperCall => keeperCall;
    public int PracticeRate => taught ? 2 : 1;
    public int PurseCoins => EarnedCoins - (taught ? LessonPrice : 0) + (keeperCall == KeeperCallState.Paid ? 1 : 0);

    public bool TakeLesson()
    {
        if (!Delivered || taught || FirstSense || PurseCoins < LessonPrice) return false;
        taught = true;
        return true;
    }

    public bool ToggleKeeperMessages()
    {
        if (!Delivered || FirstSense || keeperCall != KeeperCallState.Waiting) return false;
        quietPractice = !quietPractice;
        return true;
    }

    public bool PracticeOneShift()
    {
        if (!Delivered || FirstSense || keeperCall == KeeperCallState.Calling) return false;
        practiceTicks++;
        practiceProgress = Mathf.Min(PracticeGoal, practiceProgress + PracticeRate);
        if (practiceTicks == 1) keeperCall = ListenForKeeper ? KeeperCallState.Calling : KeeperCallState.Missed;
        else if (keeperCall == KeeperCallState.Available) keeperCall = KeeperCallState.Missed;
        return true;
    }

    public bool AnswerKeeper(bool leave)
    {
        if (keeperCall != KeeperCallState.Calling) return false;
        keeperCall = leave ? KeeperCallState.Available : KeeperCallState.Missed;
        return true;
    }

    public bool DrySample()
    {
        if (keeperCall != KeeperCallState.Available) return false;
        keeperCall = KeeperCallState.Paid;
        return true;
    }

    void RestorePractice()
    {
        taught &= Delivered;
        quietPractice &= Delivered;
        practiceTicks = Delivered ? Mathf.Clamp(practiceTicks, 0, PracticeGoal) : 0;
        practiceProgress = Mathf.Clamp(practiceProgress, practiceTicks, Mathf.Min(PracticeGoal, practiceTicks * PracticeRate));
        if (practiceTicks == 0) keeperCall = KeeperCallState.Waiting;
        else if (keeperCall == KeeperCallState.Calling && practiceTicks == 1)
            keeperCall = KeeperCallState.Available; // Reopening the scene ends the sitting at the last complete shift.
        else if (keeperCall != KeeperCallState.Paid && !(keeperCall == KeeperCallState.Available && practiceTicks == 1))
            keeperCall = KeeperCallState.Missed;
    }
}
