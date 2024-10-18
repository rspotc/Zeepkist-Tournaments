using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Chat;
using ZeepSDK.Messaging;

public static class TopOutChatManager
{
    public static int serverMessagePage = 0;

    public static void sendStartTournament()
    {
        ChatApi.SendMessage("!topout start");
    }

    public static void sendEndTournament()
    {
        ChatApi.SendMessage("/servermessage remove");
        ChatApi.SendMessage("/joinmessage white ");
        ChatApi.SendMessage("/joinmessage off");
        ChatApi.SendMessage("!topout stop");
    }

    public static void parseChatForTournamentInfo(ZeepkistChatMessage message)
    {
        if (message.Player == null || !message.Player.isHost || ZeepkistNetwork.IsMasterClient) return;

        startOrEndTournament(message);
    }

    public static void setRoundTime(bool finisher=false)
    {
        if (finisher)
        {
            ChatApi.SendMessage($"/settime {TopOutTracker.tournamentSettings.timeFromFirstFinish}");
        }
        else
        {
            ChatApi.SendMessage($"/settime {TopOutTracker.tournamentSettings.roundTime}");
        }
    }

    public static void setJoinMessage()
    {
        ChatApi.SendMessage("/joinmessage blue Welcome to the Top->Out Tournament. Participants accumulate points until reaching a threshold, then must come first in one round to win. (Note: You will be kicked if you finish after winning the tournament)");
    }

    public static void updateServerMessage(ChangeLobbyTimerPacket timePkt)
    {
        if (!(ZeepkistNetwork.IsMasterClient && TopOutTracker.tournamentState == TopOutState.Active)) return;

        if (timePkt.TimeLeftString.EndsWith((TopOutTracker.tournamentSettings.roundTime % 10).ToString()))
        {
            setServerMessage();
        }
    }

    public static void setServerMessage()
    {
        int maxShownLines = 5;
        int minLineShown = serverMessagePage * maxShownLines + 1;
        int maxLineShown = (serverMessagePage + 1) * maxShownLines;

        string[] winnerNames = new string[TopOutTracker.tournamentSettings.winners+1];
        winnerNames[0] += $"<size=+10><pos=60%>{TopOutColors.convertColor(TopOutColors.colorWinner)}<u>Winners</u>";
        for (int i = 0; i < TopOutTracker.tournamentSettings.winners; ++i) {
            if (i + 1 < minLineShown || i + 1 > maxLineShown) winnerNames[i+1] = "<size=0%>";
            else winnerNames[i+1] = "<size=+5>";
            winnerNames[i+1] += $"<pos=60%>{TopOutColors.convertColor(PlayerManager.Instance.GetColorFromPosition(i+1))}{i+1}) {TopOutColors.convertColor(TopOutColors.colorText)}";

            if (i > TopOutTracker.winnerCount) continue;

            foreach (KeyValuePair<ulong, TopOutPlayer> player in TopOutTracker.participants)
            {
                if (player.Value.winnerPosition == i)
                {
                    winnerNames[i+1] += $"{player.Value.fullUsername}";
                    break;
                }
            }
        }

        List<string> finalistNames = new List<string>();
        finalistNames.Add($"<size=+10><pos=25%>{TopOutColors.convertColor(TopOutColors.colorFinalist)}<u>Finalists</u>");
        int longestNameAllowed = 25;
        foreach (KeyValuePair<ulong, TopOutPlayer> player in TopOutTracker.participants)
        {
            if (player.Value.finalist)
            {
                string nameString;
                if (finalistNames.Count + 1 < minLineShown || finalistNames.Count + 1 > maxShownLines) nameString = "<size=0%>";
                else nameString = "<size=+5>";

                nameString += $"<pos=25%>{TopOutColors.convertColor(TopOutColors.colorText)}";
                if (player.Value.fullUsername.Length > longestNameAllowed) nameString += player.Value.fullUsername.Substring(0, longestNameAllowed) + "<alpha=#00>" + player.Value.fullUsername.Substring(longestNameAllowed);
                else nameString += player.Value.fullUsername;
                finalistNames.Add(nameString);
            }
        }

        string[] tournamentSettingStrings = new string[5];
        tournamentSettingStrings[0] = $"<size=+18><b>{TopOutColors.convertColor(TopOutColors.colorNeutral)}TOP{TopOutColors.convertColor(TopOutColors.colorText)}->{TopOutColors.convertColor(TopOutColors.colorNeutral)}OUT</b>";
        tournamentSettingStrings[1] = $"<size=+13>{TopOutColors.convertColor(TopOutColors.colorText)}<u>TOURNAMENT</u>";
        tournamentSettingStrings[2] = $"<size=+3>{TopOutColors.convertColor(TopOutColors.colorText)}<i>Points: {TopOutTracker.topThreshold}</i>";
        tournamentSettingStrings[3] = $"<size=+3>{TopOutColors.convertColor(TopOutColors.colorText)}<i>Round Length: {TopOutTracker.tournamentSettings.roundTime}</i>";
        tournamentSettingStrings[4] = $"<size=+3>{TopOutColors.convertColor(TopOutColors.colorText)}<i>Finish Timer: ";
        if (TopOutTracker.tournamentSettings.firstFinishStartsTimer) tournamentSettingStrings[4] += $"{TopOutTracker.tournamentSettings.timeFromFirstFinish}</i>";
        else tournamentSettingStrings[4] += "N/A</i>";

        string serverMessage = "/servermessage white 0 <align=\"left\">";

        int showableLineCount = Math.Max(minLineShown + tournamentSettingStrings.Length - 1, Math.Max(finalistNames.Count, winnerNames.Length));
        for (int i=0; i < showableLineCount; ++i)
        {
            if (i > 0)
            {
                serverMessage += "<br>";
            }

            if (i == 0)
            {
                serverMessage += tournamentSettingStrings[0];
            }
            else if (i >= minLineShown && i < maxLineShown)
            {
                serverMessage += tournamentSettingStrings[1 + (i - minLineShown)];
            }

            if (finalistNames.Count > i)
            {
                serverMessage += finalistNames[i];
            }
            if (winnerNames.Length > i)
            {
                serverMessage += winnerNames[i];
            }

            if (i == 0 || (i >= minLineShown && i < Math.Min(showableLineCount, maxLineShown)))
            {
                serverMessage += "<line-height=100%>";
            }
            else
            {
                serverMessage += "<line-height=0%>";
            }
        }
        if ((++serverMessagePage) * maxShownLines + 1 > showableLineCount - 1) serverMessagePage = 0;

        foreach (KeyValuePair<ulong, TopOutPlayer> player in TopOutTracker.participants)
        {
            if (player.Value.nuisance)
            {
                serverMessage += $"<br><line-height=0%><size=0%>{player.Value.fullUsername}";
            }
        }
        ChatApi.SendMessage(serverMessage);
    }

    public static void getTournamentParams(byte messageType, Color color, string message)
    {
        if (!(TopOutTracker.tournamentState == TopOutState.Inactive || TopOutTracker.tournamentState == TopOutState.Shutdown)
            && messageType == 2)
//            && messageType == 2 && !ZeepkistNetwork.IsMasterClient)
        {
            try
            {
                string[] lines = message.Split("<br>");
                if (Regex.Replace(lines[0], "<.*?>", string.Empty) != "TOP->OUTFinalistsWinners") return;

                int numWinners = 0;
                for (int i = 1; i < lines.Length; ++i)
                {
                    string[] columnedLine = Regex.Split(lines[i], "<pos=");

                    string rawFirstCol = Regex.Replace(columnedLine[0], "<.*?>", string.Empty);
                    if (rawFirstCol.Length >= 8 && rawFirstCol.Substring(0, 8) == "Points: ")
                    {
                        Int32.TryParse(Regex.Replace(columnedLine[0], "<.*?>", string.Empty).Substring(8), out TopOutTracker.topThreshold);
                        TopOutLogger.Instance.LogInfo($"Parsed threshold from server message {TopOutTracker.topThreshold}");
                    }
                    else if (rawFirstCol.Length >= 14 && rawFirstCol.Substring(0, 14) == "Round Length: ")
                    {
                        Int32.TryParse(Regex.Replace(columnedLine[0], "<.*?>", string.Empty).Substring(14), out TopOutTracker.tournamentSettings.roundTime);
                        TopOutLogger.Instance.LogInfo($"Parsed roundTime from server message {TopOutTracker.tournamentSettings.roundTime}");
                    }
                    else if (rawFirstCol.Length >= 14 && rawFirstCol.Substring(0, 14) == "Finish Timer: ")
                    {
                        string finishCountdown = Regex.Replace(columnedLine[0], "<.*?>", string.Empty).Substring(14);
                        if (finishCountdown == "N/A") TopOutTracker.tournamentSettings.firstFinishStartsTimer = false;
                        else
                        {
                            TopOutTracker.tournamentSettings.firstFinishStartsTimer = true;
                            Int32.TryParse(finishCountdown, out TopOutTracker.tournamentSettings.timeFromFirstFinish);
                        }
                        TopOutLogger.Instance.LogInfo($"Parsed finishTime from server message {TopOutTracker.tournamentSettings.firstFinishStartsTimer} {TopOutTracker.tournamentSettings.timeFromFirstFinish}");
                    }
                    else if (rawFirstCol != "TOURNAMENT")
                    {
                        parseNuisance(rawFirstCol);
                    }

                    for (int j = 0; j < columnedLine.Length; ++j)
                    {
                        if (columnedLine[j].Substring(0, 4) == "25%>")
                        {
                            parseFinalist(Regex.Replace(columnedLine[j].Substring(4), "<.*?>", string.Empty));
                        }
                        else if (columnedLine[j].Substring(0, 4) == "60%>")
                        {
                            ++numWinners;
                            parseWinner(Regex.Replace(columnedLine[j].Substring(4), "<.*?>", string.Empty));
                        }
                    }
                }
                TopOutTracker.tournamentSettings.winners = numWinners;
                TopOutLogger.Instance.LogInfo($"Parsed winners from server message {TopOutTracker.tournamentSettings.winners}");
            }
            catch (Exception e)
            {
                TopOutLogger.Instance.LogWarning($"Malformed \"Top->Out: \" server message {message}");
                TopOutLogger.Instance.LogError($"Unhandled Exception in {nameof(getTournamentParams)}: {e}");
            }
        }
    }

    private static bool startOrEndTournament(ZeepkistChatMessage message)
    {
        string messageString = Regex.Replace(message.Message, "<.*?>", string.Empty);
        string[] messageText = messageString.Split(' ', 2);

        if (messageText[0] != "!topout") return false;

        if (messageText.Length < 2) return true;

        TopOutLogger.Instance.LogInfo($"Top->Out message {messageText[1]} received. State {TopOutTracker.tournamentState}");

        if (messageText[1] == "stop")
        {
            TopOutTracker.endTournament();
            MessengerApi.LogCustomColors("Top->Out tournament is over.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);
        }
        else if (messageText[1] == "start")
        {
            if (TopOutTracker.tournamentState == TopOutState.Inactive || TopOutTracker.tournamentState == TopOutState.Shutdown)
            {
                TopOutTracker.endTournament();
            }
            TopOutTracker.tournamentState = TopOutState.Primed;
            TopOutTracker.setRidiculousTournamentParameters();

            MessengerApi.LogCustomColors("Top->Out tournament starts next round.", TopOutColors.colorNeutral, TopOutColors.colorText, 5.0f);
        }
        return true;
    }

    private static void parseFinalist(string username)
    {
        ulong steamID = TopOutTracker.findSteamIDFromUsername(username);
        
        if (steamID == 0)
        {
            foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
            {
                if (player.GetTaggedUsername() == username) {
                    TopOutTracker.participants.Add(player.SteamID, new TopOutPlayer(player.GetUserNameNoTag(), player.GetTaggedUsername())
                    {
                        finalist = true,
                        winnerPosition = -1,
                        nuisance = false
                    });

                    ++TopOutTracker.finalistCount;
                    break;
                }
            }
        }
        else
        {
            if (!TopOutTracker.alreadyFinalist(steamID))
            {
                TopOutTracker.participants[steamID].finalist = true;
                TopOutTracker.participants[steamID].winnerPosition = -1;
                TopOutTracker.participants[steamID].nuisance = false;
                ++TopOutTracker.finalistCount;
            }
        }
    }

    private static void parseWinner(string winnerString)
    {
        string[] positionAndUsername = winnerString.Split(") ", 2);
        if (positionAndUsername.Length == 2)
        {
            try
            {
                int winnerRank = Int32.Parse(positionAndUsername[0]) - 1;
                string winnerUsername = positionAndUsername[1];

                ulong steamID = TopOutTracker.findSteamIDFromUsername(winnerUsername);
                if (steamID == 0)
                {
                    foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
                    {
                        if (player.GetTaggedUsername() == winnerUsername) {
                            TopOutTracker.participants.Add(player.SteamID, new TopOutPlayer(player.GetUserNameNoTag(), player.GetTaggedUsername())
                            {
                                finalist = false,
                                winnerPosition = winnerRank,
                                nuisance = false
                            });

                            TopOutTracker.winnerCount = winnerRank + 1;
                            break;
                        }
                    }
                }
                else
                {
                    if (!TopOutTracker.alreadyWinner(steamID))
                    {
                        TopOutTracker.participants[steamID].finalist = false;
                        TopOutTracker.participants[steamID].winnerPosition = winnerRank;
                        TopOutTracker.participants[steamID].nuisance = false;
                        TopOutTracker.winnerCount = winnerRank + 1;
                    }
                    else if (TopOutTracker.participants[steamID].winnerPosition != winnerRank) {
                        TopOutTracker.participants[steamID].winnerPosition = winnerRank;
                        TopOutTracker.participants[steamID].nuisance = false;
                    }
                }
            }
            catch
            {
                TopOutLogger.Instance.LogInfo($"Unable to determine winner from server message: {winnerString}");
            }
        }
    }

    private static void parseNuisance(string username)
    {
        ulong steamID = TopOutTracker.findSteamIDFromUsername(username);
        
        if (steamID == 0)
        {
            foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
            {
                if (player.GetTaggedUsername() == username) {
                    TopOutTracker.participants.Add(player.SteamID, new TopOutPlayer(player.GetUserNameNoTag(), player.GetTaggedUsername())
                    {
                        finalist = false,
                        winnerPosition = -1,
                        nuisance = true
                    });
                    break;
                }
            }
        }
        else
        {
            TopOutTracker.participants[steamID].finalist = false;
            TopOutTracker.participants[steamID].winnerPosition = -1;
            TopOutTracker.participants[steamID].nuisance = true;
        }
    }
}
