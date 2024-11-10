using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Chat;
using ZeepSDK.Messaging;

public static class TopOutChatManager
{
    private static int winnerPage = 0;
    private static int finalistPage = 0;
    private static int maxShownLines = 5;
    private static List<string> winnerNames = new List<string>();
    private static List<string> finalistNames = new List<string>();
    private static bool roundAlmostOver = false;

    public static void reset()
    {
        winnerNames.Clear();
        finalistNames.Clear();
        roundAlmostOver = false;
    }

    public static void sendStartTournament()
    {
        ZeepkistNetwork.SendCustomChatMessage(true, 0, "Tournament starts next round.", "TOP->OUT");
    }

    public static void sendEndTournament()
    {
        ChatApi.SendMessage("/servermessage remove");
        ZeepkistNetwork.SendCustomChatMessage(true, 0, "Tournament is over.", "TOP->OUT");
    }

    public static void parseChatForTournamentInfo(ZeepkistChatMessage message)
    {
        if (message.Player != null || ZeepkistNetwork.IsMasterClient) return;


        string messageStr = Regex.Replace(message.Message, "<.*?>", string.Empty);
        if (!messageStr.StartsWith("[TOP->OUT] ")) return;

        startOrEndTournament(messageStr.Substring(11));
    }

    public static void setRoundTime(bool finisher=false)
    {
        if (finisher)
        {
            if (!roundAlmostOver)
            {
                TopOutColors.colorTimer = TopOutColors.colorFailure;
                ZeepkistNetwork.SendCustomChatMessage(true, 0, "A Zeepkist has crossed the finish line. Countdown timer starts now.", "TOP->OUT");
                MessengerApi.LogCustomColors("A Zeepkist has crossed the finish line.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);
                ChatApi.SendMessage($"/settime {TopOutTracker.tournamentSettings.timeFromFirstFinish}");
            }
        }
        else
        {
            ChatApi.SendMessage($"/settime {TopOutTracker.tournamentSettings.roundTime}");
        }
    }

    public static void resetPlayerLeaderboard(ulong steamID)
    {
        ZeepkistNetwork.CustomLeaderBoard_SetPlayerLeaderboardOverrides(steamID);
    }

    public static void configurePlayerOnLeaderboard(ulong steamID)
    {
        string positionText;
        string pointsText;
        string playerColor;

        if (TopOutTracker.alreadyWinner(steamID))
        {
            playerColor = TopOutColors.convertColor(TopOutColors.colorWinner);
            positionText = "WIN";
            pointsText = "WINNER";
            ZeepkistNetwork.CustomLeaderBoard_SetPlayerChampionshipPoints(steamID, TopOutTracker.tournamentSettings.threshold+1, 0, false);
            if (!TopOutTracker.tournamentSettings.winnerIsNuisance)
            {
                ZeepkistNetwork.SendCustomChatMessage(false, steamID, "You have already won. You may not set a time this round.", "TOP->OUT");
                ZeepkistNetwork.CustomLeaderBoard_BlockPlayerFromSettingTime(steamID, false);
            }
        }
        else if (TopOutTracker.alreadyFinalist(steamID))
        {
            playerColor = TopOutColors.convertColor(TopOutColors.colorFinalist);
            positionText = "FIN";
            pointsText = "FINALIST";
            ZeepkistNetwork.CustomLeaderBoard_SetPlayerChampionshipPoints(steamID, TopOutTracker.tournamentSettings.threshold, 0, false);
        }
        else if (TopOutTracker.isNuisance(steamID))
        {
            playerColor = TopOutColors.convertColor(TopOutColors.colorNuisance);
            positionText = "\\o7";
            pointsText = "NUISANCE";
            ZeepkistNetwork.CustomLeaderBoard_SetPlayerChampionshipPoints(steamID, 0, 0, false);
        }
        else
        {
            playerColor = TopOutColors.convertColor(TopOutColors.colorPlayer);
            positionText = TopOutTracker.participants[steamID].points.ToString();
            pointsText = positionText + " Points";
        }

        TopOutTracker.participants[steamID].currentTime = 0;
        ZeepkistNetwork.CustomLeaderBoard_SetPlayerLeaderboardOverrides(steamID, position: $"{playerColor}{positionText}", points: $"{playerColor}{pointsText}");
    }

    public static void configureServerMessage()
    {
        winnerPage = 0;
        finalistPage = 0;

        winnerNames = new List<string>();
        for (int i = 0; i < TopOutTracker.tournamentSettings.winners; ++i) {
            if (i > TopOutTracker.winnerCount) continue;

            foreach (KeyValuePair<ulong, TopOutPlayer> player in TopOutTracker.participants)
            {
                if (player.Value.winnerPosition == i)
                {
                    winnerNames.Add(player.Value.fullUsername);
                    break;
                }
            }
        }

        finalistNames = new List<string>();
        foreach (KeyValuePair<ulong, TopOutPlayer> player in TopOutTracker.participants)
        {
            if (player.Value.finalist)
            {
                finalistNames.Add(player.Value.fullUsername);
            }
        }
    }

    public static void updateServerMessage(ChangeLobbyTimerPacket timePkt)
    {
        if (!(ZeepkistNetwork.IsMasterClient && TopOutTracker.tournamentState == TopOutState.Active)) return;

        int timeLeft = roundTimerToInt(timePkt.TimeLeftString);

        if (timeLeft > 0 && (TopOutTracker.tournamentSettings.roundTime - timeLeft) % 10 == 0)
        {
            setServerMessage();

            winnerPage++;
            if (winnerPage * maxShownLines >= winnerNames.Count) winnerPage = 0;
            finalistPage++;
            if (finalistPage * maxShownLines >= finalistNames.Count) finalistPage = 0;
        }
    }

    public static void setServerMessage()
    {
        string[] winnersDisplayed = new string[Math.Min(maxShownLines, TopOutTracker.winnerCount - winnerPage*maxShownLines)+1];
        winnersDisplayed[0] += $"<size=+10><pos=65%>{TopOutColors.convertColor(TopOutColors.colorWinner)}<u>Winners</u>";
        for (int i = 1; i < winnersDisplayed.Length; ++i) {
            int position = winnerPage * maxShownLines + i;
            Color positionColor = PlayerManager.Instance.GetColorFromPosition(position);
            winnersDisplayed[i] = $"<size=+5><pos=65%>{TopOutColors.convertColor(positionColor)}{position}) {TopOutColors.convertColor(TopOutColors.colorText)}{winnerNames[position - 1]}";
        }

        string[] finalistsDisplayed = new string[Math.Min(maxShownLines, finalistNames.Count - finalistPage*maxShownLines)+1];
        finalistsDisplayed[0] = $"<size=+10><pos=30%>{TopOutColors.convertColor(TopOutColors.colorFinalist)}<u>Finalists</u>";
        int longestNameAllowed = 25;
        for (int i = 1; i < finalistsDisplayed.Length; ++i)
        {
            int position = finalistPage * maxShownLines + i - 1;
            finalistsDisplayed[i] = $"<size=+5><pos=30%>{TopOutColors.convertColor(TopOutColors.colorText)}";
            if (finalistNames[position].Length > longestNameAllowed) finalistsDisplayed[i] += finalistNames[position].Substring(0, longestNameAllowed) + "<alpha=#00>" + finalistNames[position].Substring(longestNameAllowed);
            else finalistsDisplayed[i] += finalistNames[position];
        }

        string[] tournamentSettingStrings = new string[6];
        tournamentSettingStrings[0] = $"<size=+18><b>{TopOutColors.convertColor(TopOutColors.colorNeutral)}TOP{TopOutColors.convertColor(TopOutColors.colorText)}->{TopOutColors.convertColor(TopOutColors.colorNeutral)}OUT</b>";
        tournamentSettingStrings[1] = $"<size=+13>{TopOutColors.convertColor(TopOutColors.colorText)}<u>TOURNAMENT</u>";
        tournamentSettingStrings[2] = $"<size=+3>{TopOutColors.convertColor(TopOutColors.colorText)}<i>Points: {TopOutTracker.tournamentSettings.threshold}</i>";
        tournamentSettingStrings[3] = $"<size=+3>{TopOutColors.convertColor(TopOutColors.colorText)}<i>Winners: {TopOutTracker.tournamentSettings.winners}</i>";
        tournamentSettingStrings[4] = $"<size=+3>{TopOutColors.convertColor(TopOutColors.colorText)}<i>Round Length: {TopOutTracker.tournamentSettings.roundTime}</i>";
        tournamentSettingStrings[5] = $"<size=+3>{TopOutColors.convertColor(TopOutColors.colorText)}<i>Finish Timer: ";
        if (TopOutTracker.tournamentSettings.firstFinishStartsTimer) tournamentSettingStrings[5] += $"{TopOutTracker.tournamentSettings.timeFromFirstFinish}</i>";
        else tournamentSettingStrings[5] += "N/A</i>";

        string serverMessage = "/servermessage white 0 <align=\"left\"><alpha=#00>linebreak1<br>linebreak2<br>linebreak3<alpha=#FF><br>";

        for (int i=0; i < maxShownLines + 1; ++i)
        {
            if (i > 0)
            {
                serverMessage += "<br>";
            }

            if (tournamentSettingStrings.Length > i) serverMessage += tournamentSettingStrings[i];

            if (finalistsDisplayed.Length > i) serverMessage += finalistsDisplayed[i];
            if (winnersDisplayed.Length > i) serverMessage += winnersDisplayed[i];
        }

        ChatApi.SendMessage(serverMessage);
    }

    private static void startOrEndTournament(string message)
    {
        if (message == "Tournament starts next round.")
        {
            TopOutTracker.tournamentState = TopOutState.Joined;
            MessengerApi.LogCustomColors("Top->Out tournament starts next round.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);
        }
        else if (message == "Tournament is over.")
        {
            TopOutTracker.tournamentState = TopOutState.Inactive;
            MessengerApi.LogCustomColors("Top->Out tournament is over.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);
        }
        else if (TopOutTracker.tournamentState == TopOutState.Joined && message == "A Zeepkist has crossed the finish line. Countdown timer starts now.")
        {
            TopOutColors.colorTimer = TopOutColors.colorFailure;
            MessengerApi.LogCustomColors("A Zeepkist has crossed the finish line.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);
        }
    }
    private static int roundTimerToInt(string roundTimer)
    {
        string[] hms = roundTimer.Split(':');

        int min;
        int sec;
        int.TryParse(hms[hms.Length - 1], out sec);
        int.TryParse(hms[hms.Length - 2], out min);

        int timeLeft = 60 * min + sec;

        if (TopOutTracker.tournamentSettings.firstFinishStartsTimer && timeLeft < TopOutTracker.tournamentSettings.timeFromFirstFinish)
        {
            roundAlmostOver = true;
        }
        else
        {
            roundAlmostOver = false;
        }
        return 60 * min + sec;
    }
}
