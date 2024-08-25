using BepInEx.Logging;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using ZeepkistClient;
using ZeepkistNetworking;

using ZeepSDK.Leaderboard;
using ZeepSDK.Messaging;

public static class TopOutTracker
{
    public static Dictionary<ulong, TopOutPlayer> participants = new Dictionary<ulong, TopOutPlayer>();
    public static bool noFinishers = true;
    public static int winnerCount = 0;
    public static int finalistCount = 0;
    public static int topThreshold;

    public static TopOutState tournamentState = TopOutState.Inactive;
    public static TopOutSettings tournamentSettings;

    public static TopOutGameplayLeaderboard gameplayLeaderboard = new TopOutGameplayLeaderboard();
    public static TopOutGameplayLeaderboard spectatorLeaderboard = new TopOutGameplayLeaderboard();

    public static void startTournament(TopOutSettings settings)
    {
        TopOutLogger.Instance.LogInfo($"Trying to start Top->Out in state {tournamentState} as host? {ZeepkistNetwork.IsMasterClient}");

        if ((tournamentState == TopOutState.Inactive || tournamentState == TopOutState.Shutdown) && ZeepkistNetwork.IsMasterClient)
        {
            tournamentSettings = settings;

            topThreshold = tournamentSettings.useThreshold ? tournamentSettings.threshold : 64 * (tournamentSettings.rounds - 2) + 1;
            tournamentState = TopOutState.Primed;
            MessengerApi.LogCustomColors("Top->Out tournament starts next round.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);

            TopOutLogger.Instance.LogInfo($"Top->Out tournament started with threshold {topThreshold}, round time {tournamentSettings.roundTime}:{tournamentSettings.timeFromFirstFinish}, winners {tournamentSettings.winners}");

            TopOutChatManager.sendStartTournament();
        }
        else if (!(tournamentState == TopOutState.Inactive || tournamentState == TopOutState.Shutdown) && ZeepkistNetwork.IsMasterClient) {
            MessengerApi.LogCustomColors("Top->Out tournament is already going. End the tournament first to start a new one.", TopOutColors.colorText, TopOutColors.colorFailure, 5.0f);
        }
        else
        {
            MessengerApi.LogCustomColors("You are not host, you may not start a Top->Out tournament.", TopOutColors.colorText, TopOutColors.colorFailure, 5.0f);
        }
    }

    public static void endTournament()
    {
        TopOutLogger.Instance.LogInfo($"Ending Top->Out in state {tournamentState}");

        if (!(tournamentState == TopOutState.Inactive || tournamentState == TopOutState.Shutdown))
        {
            participants.Clear();
            winnerCount = 0;
            finalistCount = 0;
            tournamentState = TopOutState.Shutdown;

            noFinishers = true;
            TopOutColors.colorTimer = Color.white;

            TopOutLogger.Instance.LogInfo("Deleting Top->Out leaderboard tab");
            if (tabLeaderboard != null)
            {
                LeaderboardApi.RemoveTab(tabLeaderboard);
                tabLeaderboard = null;
            }
            TopOutLogger.Instance.LogInfo("Done deleting Top->Out leaderboard tab");

            TopOutLogger.Instance.LogDebug("Resetting GAME");
            gameplayLeaderboard.resetPlayerLeaderboard();
            TopOutLogger.Instance.LogDebug("Resetting SPEC");
            spectatorLeaderboard.resetPlayerLeaderboard();
        
            if (ZeepkistNetwork.IsMasterClient)
            {
                if (tournamentSettings.setPrivateOnStart) ZeepkistNetwork.CurrentLobby.UpdateVisibility(true);

                TopOutChatManager.sendEndTournament();
            }
        } 
    }

    public static void joinTournament()
    {
        if (ZeepkistNetwork.IsMasterClient)
        {
            MessengerApi.LogCustomColors("You cannot join a tournament as host.", TopOutColors.colorText, TopOutColors.colorFailure, 5.0f);
        }
        else if (!(tournamentState == TopOutState.Shutdown || tournamentState == TopOutState.Inactive))
        {
            TopOutLogger.Instance.LogError($"Unable to join current Top->Out tournament as one is already active");
            MessengerApi.LogCustomColors("Tournament is already running.", TopOutColors.colorText, TopOutColors.colorFailure, 5.0f);
        }

        TopOutLogger.Instance.LogInfo("Joining Top->Out Tournament");

        registerPlayers(true);
        setRidiculousTournamentParameters();

        tabLeaderboard = LeaderboardApi.InsertTab<TopOutTabLeaderboard>(0);
        spectatorLeaderboard.displayLeaderboardPoints();
        gameplayLeaderboard.displayLeaderboardPoints();

        tournamentState = TopOutState.Active;

        TopOutLogger.Instance.LogInfo("Joined Top->Out Tournament Successfully");
    }

    public static void startRound()
    {
        if (tournamentState == TopOutState.Shutdown)
        {
            TopOutLogger.Instance.LogInfo("Finalizing Top->Out shutdown");

            if (ZeepkistNetwork.IsMasterClient) TopOutChatManager.setRoundTime();
            tournamentState = TopOutState.Inactive;
        }
        else if (tournamentState == TopOutState.Initiated)
        {
            TopOutLogger.Instance.LogInfo("Finalizing Top->Out startup");

            tournamentState = TopOutState.Active;

            TopOutLogger.Instance.LogInfo("Creating Top->Out leaderboard tab");
            tabLeaderboard = LeaderboardApi.InsertTab<TopOutTabLeaderboard>(0);
 
            if (ZeepkistNetwork.IsMasterClient)
            {
                ZeepkistNetwork.ResetChampionshipPoints();
                if (tournamentSettings.setPrivateOnStart) ZeepkistNetwork.CurrentLobby.UpdateVisibility(false);
                TopOutChatManager.setServerMessage();
                TopOutChatManager.setJoinMessage();
                TopOutChatManager.setRoundTime();
            }
        }
        else if (tournamentState == TopOutState.BetweenRounds)
        {
            TopOutLogger.Instance.LogInfo("Starting New Top->Out round");

            tournamentState = TopOutState.Active;

            if (ZeepkistNetwork.IsMasterClient)
            {
                TopOutChatManager.announceFinalists();
                TopOutChatManager.setServerMessage();
                TopOutChatManager.setRoundTime();
            }
        }
    }

    public static void endRound()
    {
        if (tournamentState == TopOutState.Primed)
        {
            registerPlayers();
            tournamentState = TopOutState.Initiated;
        }

        noFinishers = true;

        spectatorLeaderboard.mainLeaderboard = null;

        if (tournamentState != TopOutState.Active) return;

        TopOutLogger.Instance.LogInfo($"Ending Top->Out round");

        tournamentState = TopOutState.BetweenRounds;

        createWinner();

        TopOutLogger.Instance.LogInfo($"{winnerCount} Top->Out winners so far");

        if (ZeepkistNetwork.IsMasterClient && winnerCount >= tournamentSettings.winners)
        {
            TopOutLogger.Instance.LogInfo($"Top->Out winner quota achieved");

            endTournament();
            MessengerApi.LogCustomColors("Top->Out tournament is over.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);
        }

        createFinalists();
    }

    public static ZeepkistNetworkPlayer[] getOrderedPlayers(out Color[] playerColors)
    {
        ZeepkistNetworkPlayer[] players = ZeepkistNetwork.PlayerList.OrderByDescending(p => winnerPosition(p.SteamID)).
            ThenByDescending(p => alreadyFinalist(p.SteamID)).
            ThenByDescending(p => playerPoints(p.SteamID)).ToArray();

        playerColors = new Color[ZeepkistNetwork.PlayerList.Count];
        for (int playerIdx=0; playerIdx < playerColors.Length; ++playerIdx)
        {
            if (alreadyWinner(players[playerIdx].SteamID)) playerColors[playerIdx] = TopOutColors.colorWinner;
            else if (alreadyFinalist(players[playerIdx].SteamID)) playerColors[playerIdx] = TopOutColors.colorFinalist;
            else playerColors[playerIdx] = TopOutColors.colorPlayer;
        }

        return players;
    }

    public static void putInPhotoMode()
    {
        if (ZeepkistNetwork.CurrentLobby == null) return;

        if (alreadyWinner(ZeepkistNetwork.LocalPlayer.SteamID)) {
            TopOutLogger.Instance.LogInfo($"Putting player in photomode because they already won Top->Out");
            PlayerManager.Instance.currentMaster.flyingCamera.ToggleFlyingCamera();
        }
    }

    public static void someoneFinished()
    {
        if (tournamentState != TopOutState.Active) return;

        if (checkLeaderboardForWinners() < ZeepkistNetwork.Leaderboard.Count && noFinishers)
        {
            TopOutLogger.Instance.LogInfo($"A first finish occured in Top->Out");
            noFinishers = false;

            if (ZeepkistNetwork.IsMasterClient && tournamentSettings.firstFinishStartsTimer) TopOutChatManager.setRoundTime(true);
        }
    }

    public static void localPlayerLeft()
    {
        TopOutLogger.Instance.LogInfo($"The local player left lobby in Top->Out state {tournamentState}");

        endTournament();
        tournamentState = TopOutState.Inactive;
    }

    public static void playerJoined(ZeepkistNetworkPlayer player)
    {
        TopOutLogger.Instance.LogInfo($"The player {player.GetTaggedUsername()} joined lobby in Top->Out state {tournamentState}");
        if (tournamentState == TopOutState.Inactive || tournamentState == TopOutState.Shutdown) return;

        if (!participants.ContainsKey(player.SteamID))
        {
            participants.Add(player.SteamID, new TopOutPlayer(player.GetTaggedUsername()));
        }
    }

    public static bool alreadyFinalist(ulong playerSteamID)
    {
        if (!participants.ContainsKey(playerSteamID)) return false;

        return participants[playerSteamID].finalist;
    }
    
    public static bool alreadyWinner(ulong playerSteamID)
    {
        if (!participants.ContainsKey(playerSteamID)) return false;

        return participants[playerSteamID].winnerPosition >= 0;
    }

    public static ulong findSteamIDFromUsername(string username)
    {
        foreach (KeyValuePair<ulong, TopOutPlayer> player in participants)
        {
            if (player.Value.username == username) return player.Key;
        }
        return 0;
    }

    public static void setRidiculousTournamentParameters()
    {
        // On startup the parameters the host started the tournament with are unknown so set them high
        topThreshold = 9999;
        tournamentSettings.firstFinishStartsTimer = false;
        tournamentSettings.roundTime = 3600;
        tournamentSettings.timeFromFirstFinish = 1200;
        tournamentSettings.winners = 50;
        tournamentSettings.setPrivateOnStart = false;
    }

    public static void hostChanged(ZeepkistNetworkPlayer newHost)
    {
        if ((topThreshold == 9999 || tournamentSettings.roundTime == 3600 || tournamentSettings.winners == 50) &&
            !(tournamentState == TopOutState.Inactive || tournamentState == TopOutState.Shutdown))
        {
            TopOutLogger.Instance.LogInfo("Host changed before Top->Out parameters determined. Ending tournament");
            MessengerApi.LogCustomColors("Top->Out tournament is over.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);

            endTournament();
            tournamentState = TopOutState.Inactive; // In case the local player became host
        }
    }

    private static TopOutTabLeaderboard tabLeaderboard = null;

    private static void registerPlayers(bool joining=false)
    {
        foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
        {
            if (!participants.ContainsKey(player.SteamID))
            {
                participants.Add(player.SteamID, new TopOutPlayer(player.GetTaggedUsername())
                {
                    points = joining ? player.ChampionshipPoints.x : 0
                });
            }
        }
    }

    private static int checkLeaderboardForWinners()
    {
        TopOutLogger.Instance.LogInfo("Checking if Top->Out round finishers are already winners.");

        int winnersOnLeaderboard = 0;
        foreach (LeaderboardItem finisher in ZeepkistNetwork.Leaderboard)
        {
            if (alreadyWinner(finisher.SteamID))
            {
                if (ZeepkistNetwork.IsMasterClient)
                {
                    kickWinner(finisher);
                }
                ++winnersOnLeaderboard;
            }
        }
        return winnersOnLeaderboard;
    }

    private static void createFinalists()
    {
        foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
        {
            if (!participants.ContainsKey(player.SteamID)) continue;

            TopOutPlayer participant = participants[player.SteamID];
            participant.points += player.ChampionshipPoints.y;

            if (participant.points >= topThreshold)
            {
                if (!(alreadyFinalist(player.SteamID) || alreadyWinner(player.SteamID)))
                {
                    TopOutLogger.Instance.LogInfo($"Adding {participant.username} with points {participant.points} to finalists");
                    participant.finalist = true;
                    ++finalistCount;
                }
            }
        }
    }

    private static void createWinner()
    {
        for (int idx = 0; idx < ZeepkistNetwork.Leaderboard.Count; ++idx)
        {
            LeaderboardItem roundWinner = ZeepkistNetwork.Leaderboard[idx];
            TopOutLogger.Instance.LogInfo($"Checking round leaderboard position {idx}, Username {roundWinner.Username}");

            if (alreadyWinner(roundWinner.SteamID)) continue;

            if (alreadyFinalist(roundWinner.SteamID))
            {
                TopOutPlayer finalist = participants[roundWinner.SteamID];
                TopOutLogger.Instance.LogInfo($"Swapping {finalist.username} from finalist to winner");

                finalist.winnerPosition = winnerCount;
                finalist.finalist = false;
                --finalistCount;
                ++winnerCount;

                if (roundWinner.SteamID == ZeepkistNetwork.LocalPlayer.SteamID)
                {
                    MessengerApi.LogCustomColors("YOU WON!!!", TopOutColors.colorText, TopOutColors.colorSuccess, 5.0f);
                }

                if (ZeepkistNetwork.IsMasterClient) {
                    TopOutChatManager.announceWinner(finalist.username);
                }
            }
            break;
        }
    }

    private static void kickWinner(LeaderboardItem winner)
    {
        foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
        {
            if (winner.SteamID == player.SteamID && !player.isHost)
            {
                TopOutLogger.Instance.LogInfo($"Kicking {winner.Username} for putting down a time after winning Top->Out");

                ZeepkistNetwork.KickPlayer(player);
            }
        }
    }

    private static int winnerPosition(ulong playerSteamID)
    {
        if (!participants.ContainsKey(playerSteamID)) return -2;

        return winnerCount - participants[playerSteamID].winnerPosition;
    }

    private static int playerPoints(ulong playerSteamID)
    {
        if (!participants.ContainsKey(playerSteamID)) return 0;

        return participants[playerSteamID].points;
    }
}
