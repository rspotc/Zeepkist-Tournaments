using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using ZeepkistClient;
using ZeepkistNetworking;

//using ZeepSDK.Leaderboard;
using ZeepSDK.Messaging;

public static class TopOutTracker
{
    public static Dictionary<ulong, TopOutPlayer> participants = new Dictionary<ulong, TopOutPlayer>();
    public static bool noFinishers = true;
    public static int winnerCount = 0;
    public static string nuisanceFile = "";

    public static TopOutState tournamentState = TopOutState.Inactive;
    public static TopOutSettings tournamentSettings;

    public static TopOutGameplayLeaderboard gameplayLeaderboard = new TopOutGameplayLeaderboard();
    public static TopOutGameplayLeaderboard spectatorLeaderboard = new TopOutGameplayLeaderboard();

    public static void startTournament(TopOutSettings settings)
    {
        TopOutLogger.Instance.LogInfo($"Trying to start Top->Out in state {tournamentState} as host? {ZeepkistNetwork.IsMasterClient}");
        if (!ZeepkistNetwork.IsMasterClient)
        {
            MessengerApi.LogCustomColors("You are not host, you may not start a Top->Out tournament.", TopOutColors.colorOffWhite, TopOutColors.colorFailure, 5.0f);
            return;
        }

        if (tournamentState == TopOutState.Inactive || tournamentState == TopOutState.Shutdown)
        {
            tournamentSettings = settings;

            tournamentState = TopOutState.Primed;
            MessengerApi.LogCustomColors("Top->Out tournament starts next round.", TopOutColors.colorNeutral, TopOutColors.colorOffWhite, 5.0f);

            TopOutLogger.Instance.LogInfo($"Top->Out tournament started with threshold {tournamentSettings.threshold}, round time {tournamentSettings.roundTime}:{tournamentSettings.timeFromFirstFinish}, winners {tournamentSettings.winners}");

            TopOutChatManager.sendStartTournament();
        }
        else
        {
            MessengerApi.LogCustomColors("Top->Out tournament is already going. End the tournament first to start a new one.", TopOutColors.colorOffWhite, TopOutColors.colorFailure, 5.0f);
        }
    }

    public static void endTournament()
    {
        TopOutLogger.Instance.LogInfo($"Ending Top->Out in state {tournamentState}");

        TopOutColors.colorTimer = Color.white;
        if (!ZeepkistNetwork.IsMasterClient)
        {
            tournamentState = TopOutState.Inactive;
            return;
        } 
        if (tournamentState == TopOutState.Inactive || tournamentState == TopOutState.Shutdown) return;

        TopOutChatManager.reset();
        participants.Clear();
        winnerCount = 0;
        tournamentState = TopOutState.Shutdown;

        noFinishers = true;

//        TopOutLogger.Instance.LogInfo("Deleting Top->Out leaderboard tab");
//        if (tabLeaderboard != null)
//        {
//            LeaderboardApi.RemoveTab(tabLeaderboard);
//            tabLeaderboard = null;
//        }
//        TopOutLogger.Instance.LogInfo("Done deleting Top->Out leaderboard tab");

        if (tournamentSettings.setPrivateOnStart) ZeepkistNetwork.CurrentLobby.UpdateVisibility(true);

        TopOutChatManager.sendEndTournament();
        ZeepkistNetwork.CustomLeaderBoard_UnblockEveryoneFromSettingTime(false);
        foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
        {
            TopOutChatManager.resetPlayerLeaderboard(player.SteamID);
        }

        MessengerApi.LogCustomColors("Top->Out tournament is over.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);
    }

    public static void joinTournament()
    {
        if (ZeepkistNetwork.IsMasterClient)
        {
            MessengerApi.LogCustomColors("You cannot join a tournament as host.", TopOutColors.colorOffWhite, TopOutColors.colorFailure, 5.0f);
            return;
        }
//        tabLeaderboard = LeaderboardApi.InsertTab<TopOutTabLeaderboard>(0);

        tournamentState = TopOutState.Joined;

//        TopOutLogger.Instance.LogInfo("Joined Top->Out Tournament Successfully");
    }

    public static void startRound()
    {
        if (!ZeepkistNetwork.IsMasterClient) return;

        if (tournamentState == TopOutState.Shutdown)
        {
            TopOutLogger.Instance.LogInfo("Finalizing Top->Out shutdown");

            TopOutChatManager.setRoundTime();
            tournamentState = TopOutState.Inactive;
        }
        else if (tournamentState == TopOutState.Initiated || tournamentState == TopOutState.BetweenRounds)
        {
            if (tournamentState == TopOutState.Initiated)
            {
                TopOutLogger.Instance.LogInfo("Finalizing Top->Out startup");
//                TopOutLogger.Instance.LogInfo("Creating Top->Out leaderboard tab");
//                tabLeaderboard = LeaderboardApi.InsertTab<TopOutTabLeaderboard>(0);

                ZeepkistNetwork.ResetChampionshipPoints(false);
                if (tournamentSettings.setPrivateOnStart) ZeepkistNetwork.CurrentLobby.UpdateVisibility(false);
            }
            else TopOutLogger.Instance.LogInfo("Starting New Top->Out round");

            tournamentState = TopOutState.Active;

            TopOutChatManager.setRoundTime();
            TopOutChatManager.configureServerMessage();
            foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
            {
                TopOutChatManager.configurePlayerOnLeaderboard(player.SteamID);
            }
        }
    }

    public static void endRound()
    {
        TopOutColors.colorTimer = Color.white;

        if (!ZeepkistNetwork.IsMasterClient) return;
        
        TopOutLogger.Instance.LogInfo($"Ending Top->Out round");

        if (tournamentState == TopOutState.Primed)
        {
            registerPlayers();
            tournamentState = TopOutState.Initiated;
        }

        noFinishers = true;

//        spectatorLeaderboard.mainLeaderboard = null;

        TopOutChatManager.reset();

        if (tournamentState != TopOutState.Active) return;

        tournamentState = TopOutState.BetweenRounds;

        createWinner();

        TopOutLogger.Instance.LogInfo($"{winnerCount} Top->Out winners so far");

        bool shouldEndTournament = true;
        foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
        {
            if (!(alreadyWinner(player.SteamID) || isNuisance(player.SteamID)))
            {
                shouldEndTournament = false;
                break;
            }
        }
        shouldEndTournament = (shouldEndTournament || winnerCount >= tournamentSettings.winners);

        if (shouldEndTournament)
        {
            TopOutLogger.Instance.LogInfo($"Top->Out winner quota achieved");

            endTournament();
            MessengerApi.LogCustomColors("Top->Out tournament is over.", TopOutColors.colorNeutral, TopOutColors.colorOffWhite, 5.0f);
        }

        createFinalists();
    }

    public static List<ZeepkistNetworkPlayer> getOrderedPlayers(bool gameplay=false)
    {
        ZeepkistNetworkPlayer[] players;
        if (gameplay)
        {
            players = ZeepkistNetwork.PlayerList.OrderBy(p => ZeepkistNetwork.GetLeaderboardOverride(p.SteamID).overridePositionText.Contains("\\o7")).
                ThenBy(p => ZeepkistNetwork.GetLeaderboardOverride(p.SteamID).overridePositionText.Contains("WIN")).
                ThenByDescending(p => ZeepkistNetwork.GetLeaderboardOverride(p.SteamID).overridePositionText.Contains("FIN")).
                ThenByDescending(p => p.ChampionshipPoints.x).ToArray();
        }
        else
        {
            players = ZeepkistNetwork.PlayerList.OrderBy(p => ZeepkistNetwork.GetLeaderboardOverride(p.SteamID).overridePositionText.Contains("\\o7")).
                ThenByDescending(p => ZeepkistNetwork.GetLeaderboardOverride(p.SteamID).overridePositionText.Contains("WIN")).
                ThenByDescending(p => ZeepkistNetwork.GetLeaderboardOverride(p.SteamID).overridePositionText.Contains("FIN")).
                ThenByDescending(p => p.ChampionshipPoints.x).ToArray();
        }

        return players.ToList();
    }

    public static void someoneFinished()
    {
        if (!ZeepkistNetwork.IsMasterClient || tournamentState != TopOutState.Active) return;

        if (checkLeaderboardForWinners() < ZeepkistNetwork.Leaderboard.Count && noFinishers)
        {
            TopOutLogger.Instance.LogInfo($"A first finish occured in Top->Out");
            noFinishers = false;

            if (tournamentSettings.firstFinishStartsTimer) TopOutChatManager.setRoundTime(true);
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
        if (!ZeepkistNetwork.IsMasterClient || tournamentState == TopOutState.Joined) return;

        TopOutLogger.Instance.LogInfo($"The player {player.GetTaggedUsername()} joined lobby in Top->Out state {tournamentState}");
        if (tournamentState == TopOutState.Inactive || tournamentState == TopOutState.Shutdown) return;

        if (!participants.ContainsKey(player.SteamID))
        {
            participants.Add(player.SteamID, new TopOutPlayer(player.GetUserNameNoTag(), player.GetTaggedUsername())
            {
                nuisance = tournamentSettings.allowNuisances && checkNuisanceConfig(player.SteamID),
                fullUsername = player.GetTaggedUsername()
            });
        }

        if (tournamentState != TopOutState.Primed)
        {
            ZeepkistNetwork.CustomLeaderBoard_SetPlayerChampionshipPoints(player.SteamID, participants[player.SteamID].points, 0, false);
            if (participants[player.SteamID].currentTime > 0) ZeepkistNetwork.CustomLeaderBoard_SetPlayerTimeOnLeaderboard(player.SteamID, participants[player.SteamID].currentTime, false);
            TopOutChatManager.configurePlayerOnLeaderboard(player.SteamID);
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

    public static bool isNuisance(ulong playerSteamID)
    {
        if (!participants.ContainsKey(playerSteamID)) return false;

        return participants[playerSteamID].nuisance;
    }

    public static ulong findSteamIDFromUsername(string full_username)
    {
        foreach (KeyValuePair<ulong, TopOutPlayer> player in participants)
        {
            if (player.Value.fullUsername == full_username) return player.Key;
        }
        return 0;
    }

    public static void hostChanged(ZeepkistNetworkPlayer newHost)
    {
        endTournament();
    }

    public static string[] getNuisances()
    {
        try
        {
            return File.ReadAllLines(nuisanceFile);
        }
        catch (Exception e)
        {
            TopOutLogger.Instance.LogError($"Unhandled Exception in {nameof(getNuisances)}: {e}");
            return [];
        }
    }

//    private static TopOutTabLeaderboard tabLeaderboard = null;

    private static void registerPlayers()
    {
        foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
        {
            if (!participants.ContainsKey(player.SteamID))
            {
                participants.Add(player.SteamID, new TopOutPlayer(player.GetUserNameNoTag(), player.GetTaggedUsername())
                {
                    points = 0,
                    nuisance = tournamentSettings.allowNuisances && checkNuisanceConfig(player.SteamID),
                    fullUsername = player.GetTaggedUsername()
                }); ;
            }
        }
    }

    private static int checkLeaderboardForWinners()
    {
        TopOutLogger.Instance.LogInfo("Checking if Top->Out round finishers are already winners.");

        int winnersOnLeaderboard = 0;
        foreach (LeaderboardItem finisher in ZeepkistNetwork.Leaderboard)
        {
            if (isNuisance(finisher.SteamID) || alreadyWinner(finisher.SteamID)) ++winnersOnLeaderboard;
            participants[finisher.SteamID].currentTime = finisher.Time;
        }
        return winnersOnLeaderboard;
    }

    private static void createFinalists()
    {
        foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
        {
            if (!participants.ContainsKey(player.SteamID)) continue;
            if (isNuisance(player.SteamID) || alreadyWinner(player.SteamID) || alreadyFinalist(player.SteamID)) continue;

            TopOutPlayer participant = participants[player.SteamID];
            participant.points += player.ChampionshipPoints.y;

            if (participant.points >= tournamentSettings.threshold)
            {
                TopOutLogger.Instance.LogInfo($"Adding {participant.fullUsername} with points {participant.points} to finalists");
                participant.finalist = true;
            }
        }
    }

    private static void createWinner()
    {
        for (int idx = 0; idx < ZeepkistNetwork.Leaderboard.Count; ++idx)
        {
            LeaderboardItem roundWinner = ZeepkistNetwork.Leaderboard[idx];
            TopOutLogger.Instance.LogInfo($"Checking round leaderboard position {idx}, Username {roundWinner.Username}");

            if (isNuisance(roundWinner.SteamID) || (tournamentSettings.winnerIsNuisance && alreadyWinner(roundWinner.SteamID))) break;
            if (alreadyWinner(roundWinner.SteamID)) continue;

            if (alreadyFinalist(roundWinner.SteamID))
            {
                TopOutPlayer finalist = participants[roundWinner.SteamID];
                TopOutLogger.Instance.LogInfo($"Swapping {finalist.fullUsername} from finalist to winner");

                finalist.winnerPosition = winnerCount;
                finalist.finalist = false;
                ++winnerCount;
            }
            break;
        }
    }

    private static int winnerPosition(ulong playerSteamID)
    {
        if (!participants.ContainsKey(playerSteamID)) return -2;
        else if (participants[playerSteamID].winnerPosition < 0) return participants[playerSteamID].winnerPosition;

        return winnerCount - participants[playerSteamID].winnerPosition;
    }

    private static int playerPoints(ulong playerSteamID)
    {
        if (!participants.ContainsKey(playerSteamID)) return 0;

        return participants[playerSteamID].points;
    }

    private static bool checkNuisanceConfig(ulong playerID)
    {
        foreach (string idStr in getNuisances())
        {
            ulong steamID;
            ulong.TryParse(idStr, out steamID);
            if (steamID == playerID) return true;
        }
        return false;
    }
}
