using System;
using System.Collections.Generic;

using ZeepkistClient;

public class TopOutGameplayLeaderboard
{
	public TopOutGameplayLeaderboard()
	{
	}

	public List<GUI_OnlineLeaderboardPosition> mainLeaderboard { get; set; }
    public GUI_OnlineLeaderboardPosition yourLeaderboard { get; set; }

    public void resetPlayerLeaderboard()
    {
        if (mainLeaderboard == null) return;

        int numberOnLeaderboard = Math.Min(mainLeaderboard.Count, ZeepkistNetwork.PlayerList.Count);

        for (int index = 0; index < numberOnLeaderboard; ++index)
        {
            mainLeaderboard[index].position.color = PlayerManager.Instance.GetColorFromPosition(index + 1);
            mainLeaderboard[index].position.text = (index + 1).ToString();
            if (index < ZeepkistNetwork.Leaderboard.Count)
            {
                mainLeaderboard[index].position.gameObject.SetActive(true);

                if (yourLeaderboard != null && ZeepkistNetwork.Leaderboard[index].SteamID == ZeepkistNetwork.LocalPlayer.SteamID)
                {
                    yourLeaderboard.position.color = PlayerManager.Instance.GetColorFromPosition(index + 1);
                    yourLeaderboard.position.text = (index + 1).ToString();
                    yourLeaderboard.position.gameObject.SetActive(true);
                }
            }
            else
            {
                mainLeaderboard[index].position.gameObject.SetActive(false);
            }
        }
    }

    public void displayLeaderboardPoints()
    {
        if (mainLeaderboard == null) return;

        try
        {
            int numberOnLeaderboard = Math.Min(mainLeaderboard.Count, ZeepkistNetwork.PlayerList.Count);

            for (int index = 0; index < numberOnLeaderboard; ++index)
            {
                foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
                {
                    if (player.SteamID == mainLeaderboard[index].steamID)
                    {
                        updatePlayerLeaderboard(mainLeaderboard[index], player);
                    }
                    if (yourLeaderboard != null && player.SteamID == ZeepkistNetwork.LocalPlayer.SteamID)
                    {
                        updatePlayerLeaderboard(yourLeaderboard, player);
                    }
                }
            }
        }
        catch (Exception e)
        {
            TopOutLogger.Instance.LogError($"Unhandled Error in Gameplay {nameof(displayLeaderboardPoints)}: " + e);
        }
    }

    private void updatePlayerLeaderboard(GUI_OnlineLeaderboardPosition leaderboard_position, ZeepkistNetworkPlayer player)
    {
        if (TopOutTracker.isNuisance(player.SteamID))
        {
            leaderboard_position.position.color = TopOutColors.colorNuisance;
            leaderboard_position.position.text = "\\o7";
        }
        else if (TopOutTracker.alreadyWinner(player.SteamID))
        {
            leaderboard_position.position.color = TopOutColors.colorWinner;
            leaderboard_position.position.text = "WIN";
        }
        else if (TopOutTracker.alreadyFinalist(player.SteamID))
        {
            leaderboard_position.position.color = TopOutColors.colorFinalist;
            leaderboard_position.position.text = "FIN";
        }
        else
        {
            leaderboard_position.position.color = TopOutColors.colorPlayer;
            // Championship points aren't reset quite yet on startup
            if (TopOutTracker.tournamentState == TopOutState.Initiated || !TopOutTracker.participants.ContainsKey(player.SteamID))
            {
                leaderboard_position.position.text = 0.ToString(); 
            }
            else
            {
                leaderboard_position.position.text = TopOutTracker.participants[player.SteamID].points.ToString(); 
            }
        }
        leaderboard_position.position.gameObject.SetActive(true);
    }
}
