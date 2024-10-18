using System;
using System.Globalization;

using UnityEngine;

using ZeepkistClient;

using ZeepSDK.Leaderboard.Pages;

public class TopOutTabLeaderboard : BaseMultiplayerLeaderboardTab
{
    protected override string GetLeaderboardTitle()
    {
        return "Top->Out Leaderboard";
    }

    protected override void OnEnable()
    {
        ZeepkistNetwork.LeaderboardUpdated += OnLeaderboardUpdated;
        ZeepkistNetwork.PlayerResultsChanged += OnPlayerResultsChanged;

        MaxPages = (ZeepkistNetwork.PlayerList.Count - 1) / 16;
    }

    protected override void OnDisable()
    {
        ZeepkistNetwork.LeaderboardUpdated -= OnLeaderboardUpdated;
        ZeepkistNetwork.PlayerResultsChanged -= OnPlayerResultsChanged;

        for (int index = 0; index < Instance.leaderboard_tab_positions.Count; ++index)
        {
            Instance.leaderboard_tab_positions[index].player_name.color = TopOutColors.colorReset;
            Instance.leaderboard_tab_positions[index].position.color = PlayerManager.Instance.GetColorFromPosition(index);
            Instance.leaderboard_tab_positions[index].time.color = TopOutColors.colorReset;
            Instance.leaderboard_tab_positions[index].pointsCurrent.color = TopOutColors.colorReset;
            Instance.leaderboard_tab_positions[index].pointsWon.color = TopOutColors.colorReset;
        }
    }

    protected override void OnDraw()
    {
        ZeepkistNetworkPlayer[] players;
        Color[] playerColors;

        try
        {
            players = TopOutTracker.getOrderedPlayers(out playerColors);
        }
        catch (Exception e)
        {
            Logger.LogError($"Unhandled exception in {nameof(TopOutTracker.getOrderedPlayers)}: " + e);
            return;
        }

        try
        {
            for (int i = 0; i < Instance.leaderboard_tab_positions.Count; ++i)
            {
                int index = CurrentPage * 16 + i;
                if (index >= players.Length)
                    continue;

                ZeepkistNetworkPlayer player = players[index];
                GUI_OnlineLeaderboardPosition item = Instance.leaderboard_tab_positions[i];

                Color playerColor = playerColors[index];
                if (TopOutTracker.isNuisance(player.SteamID))
                {
                    playerColor = TopOutColors.colorNuisance;
                    item.pointsCurrent.text = "NUISANCE";
                }
                else if (TopOutTracker.alreadyWinner(player.SteamID))
                {
                    playerColor = TopOutColors.colorWinner;
                    item.pointsCurrent.text = "WINNER";
                }
                else if (TopOutTracker.alreadyFinalist(player.SteamID))
                {
                    playerColor = TopOutColors.colorFinalist;
                    item.pointsCurrent.text = "FINALIST";
                }
                else if (player.ChampionshipPoints.x > 0)
                {
                    playerColor = TopOutColors.colorPlayer;
                    Vector2Int championshipPoints = player.ChampionshipPoints;
                    item.pointsCurrent.text = I2.Loc.LocalizationManager.GetTranslation("Online/Leaderboard/Points")
                        .Replace(
                            "{[POINTS]}",
                            Mathf.Round(
                                TopOutTracker.participants.ContainsKey(player.SteamID) ? TopOutTracker.participants[player.SteamID].points : championshipPoints.x
                            ).ToString(CultureInfo.InvariantCulture));

                    if (championshipPoints.y != 0)
                    {
                        item.pointsWon.text =
                            "(+" + Mathf.Round(championshipPoints.y).ToString(CultureInfo.InvariantCulture) + ")";
                    }
                }
                item.pointsCurrent.color = playerColor;

                item.position.color = playerColor;
                item.position.text = (index + 1).ToString(CultureInfo.InvariantCulture);
                item.position.gameObject.SetActive(true);

                string formattedTime = player.CurrentResult != null
                    ? player.CurrentResult.Time.GetFormattedTime()
                    : string.Empty;
                item.time.text = formattedTime;

                if (player.isHost)
                {
                    item.DrawLeaderboard(
                        player.SteamID,
                        string.Format(
                            "<link=\"{0}\"><sprite=\"achievement 2\" name=\"host_client\">{1}</link>",
                            player.SteamID,
                            Instance.Filter(
                                player.GetTaggedUsername().NoParse(),
                                Steam_TheAchiever.FilterPurpose.player)));
                }
                else
                {
                    item.DrawLeaderboard(
                        player.SteamID,
                        string.Format(
                            "<link=\"{0}\">{1}</link>",
                            player.SteamID,
                            Instance.Filter(
                                player.GetTaggedUsername().NoParse(),
                                Steam_TheAchiever.FilterPurpose.player)));
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError($"Unhandled exception in {nameof(OnDraw)}: " + e);
        }
    }

    private void OnPlayerResultsChanged(ZeepkistNetworkPlayer obj)
    {
        try
        {
            MaxPages = (ZeepkistNetwork.PlayerList.Count - 1) / 16;
        }
        catch (Exception e)
        {
            Logger.LogError($"Unhandled exception in {nameof(OnPlayerResultsChanged)}: " + e);
        }
    }

    private void OnLeaderboardUpdated()
    {
        try
        {
            MaxPages = (ZeepkistNetwork.PlayerList.Count - 1) / 16;
        }
        catch (Exception e)
        {
            Logger.LogError($"Unhandled exception in {nameof(OnLeaderboardUpdated)}: " + e);
        }
    }
}
