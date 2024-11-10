public struct TopOutSettings
{
    public bool firstFinishStartsTimer;
    public bool setPrivateOnStart;
    public int threshold;
    public int winners;
    public int roundTime;
    public int timeFromFirstFinish;
    public bool allowNuisances;
    public bool winnerIsNuisance;

    public void toggleFirstFinishTimer(bool first_finish)
    {
        firstFinishStartsTimer = first_finish;
    }

    public void togglePrivateRoomOption(bool set_private)
    {
        setPrivateOnStart = set_private;
    }

    public void setThreshold(int threshold_val)
    {
        threshold = threshold_val;
    }

    public void setWinnerCount(int winners_cnt)
    {
        winners = winners_cnt;
    }

    public void setRoundTimer(int round_time)
    {
        roundTime = round_time;
    }

    public void setTimerFromFirstFinish(int countdown_time)
    {
        timeFromFirstFinish = countdown_time;
    }

    public void toggleNuisances(bool use_nuisance)
    {
        allowNuisances = use_nuisance;
    }

    public void toggleWinnerNuisance(bool winner_nuisance)
    {
        winnerIsNuisance = winner_nuisance;
    }
}
