using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZeepkistClient;
using ZeepkistNetworking;

public class TopOutGameplayLeaderboard
{
  public TopOutGameplayLeaderboard()
  {
  }

	public List<GUI_OnlineLeaderboardPosition> mainLeaderboard { get; set; }
    public GUI_OnlineLeaderboardPosition yourLeaderboard { get; set; }

    public void displayLeaderboardPoints()
    {
        if (mainLeaderboard == null) return;

        try
        {
            List<ZeepkistNetworkPlayer> players = TopOutTracker.getOrderedPlayers(true);
            int numberOnLeaderboard = Math.Min(mainLeaderboard.Count, ZeepkistNetwork.PlayerList.Count);

            for (int leaderboardPosition=0; leaderboardPosition < numberOnLeaderboard; ++leaderboardPosition)
            {
                if (leaderboardPosition < ZeepkistNetwork.Leaderboard.Count)
                {
                    foreach (ZeepkistNetworkPlayer player in players)
                    {
                        if (player.SteamID == mainLeaderboard[leaderboardPosition].steamID)
                        {
                            players.Remove(player);
                            break;
                        }
                    }
                    if (yourLeaderboard != null && mainLeaderboard[leaderboardPosition].steamID == ZeepkistNetwork.LocalPlayer.SteamID)
                    {
                        yourLeaderboard.position.text = (leaderboardPosition + 1).ToString((IFormatProvider)CultureInfo.InvariantCulture);
                        yourLeaderboard.position.color = PlayerManager.Instance.GetColorFromPosition(leaderboardPosition + 1);
                    }
                    continue;
                }

                ZeepkistNetworkPlayer currentPlayer = players.First();
                LeaderboardOverrideItem overrides = ZeepkistNetwork.GetLeaderboardOverride(currentPlayer.SteamID);

                string name = overrides.overrideNameText == "" ? currentPlayer.GetTaggedUsername() : overrides.overrideNameText;
                mainLeaderboard[leaderboardPosition].DrawLeaderboard(currentPlayer.SteamID, PlayerManager.Instance.steamAchiever.BWF_FilterString(name.NoParse(), Steam_TheAchiever.FilterPurpose.player));

                mainLeaderboard[leaderboardPosition].time.text = overrides.overrideTimeText == "" ? "" : overrides.overrideNameText;

                if (overrides.overridePositionText != "")
                {
                    mainLeaderboard[leaderboardPosition].position.text = overrides.overridePositionText;
                    mainLeaderboard[leaderboardPosition].position.gameObject.SetActive(true);
                } 
                else
                {
                    mainLeaderboard[leaderboardPosition].position.gameObject.SetActive(false);
                }
                players.RemoveAt(0);
            }
        }
        catch (Exception e)
        {
            TopOutLogger.Instance.LogError($"Unhandled Error in Gameplay {nameof(displayLeaderboardPoints)}: " + e);
        }
    }
}
