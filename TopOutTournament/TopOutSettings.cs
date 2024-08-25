public struct TopOutSettings
{
    public bool useThreshold;
    public bool firstFinishStartsTimer;
    public bool setPrivateOnStart;
    public int rounds;
    public int threshold;
    public int winners;
    public int roundTime;
    public int timeFromFirstFinish;

    public void toggleUseThreshold(bool using_threshold)
    {
        useThreshold = using_threshold;
    }

    public void toggleFirstFinishTimer(bool first_finish)
    {
        firstFinishStartsTimer = first_finish;
    }

    public void togglePrivateRoomOption(bool set_private)
    {
        setPrivateOnStart = set_private;
    }

    public void setRounds(int min_rounds)
    {
        rounds = min_rounds;
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
}
