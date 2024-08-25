using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;

using ZeepkistClient;

using ZeepSDK.Chat;
using ZeepSDK.Messaging;

public static class TopOutChatManager
{
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

        if (startOrEndTournament(message)) return;

        if (!(TopOutTracker.tournamentState == TopOutState.BetweenRounds || TopOutTracker.tournamentState == TopOutState.Active)) return;
        TopOutLogger.Instance.LogInfo($"Received message from host {message.Message}");

        string[] messageLines = message.Message.Split("<br>");

        if (Regex.Replace(messageLines[0], "<.*?>", string.Empty) == "Reached Top:")
        {
            for (int idx=1; idx < messageLines.Length; ++idx)
            {
                parseFinalist(Regex.Replace(messageLines[idx], "<.*?>", string.Empty));
            }
            foreach (KeyValuePair<ulong, TopOutPlayer> player in TopOutTracker.participants)
            {
                if (player.Value.winnerPosition == -3) // Were just chatted as a finalist
                {
                    player.Value.winnerPosition = -1;
                }
                else if (player.Value.points >= TopOutTracker.topThreshold && !TopOutTracker.alreadyWinner(player.Key))
                {
                    if (player.Key == ZeepkistNetwork.LocalPlayer.SteamID)
                    {
                        MessengerApi.LogCustomColors("YOU WON!!!, at some point we think...", TopOutColors.colorText, TopOutColors.colorSuccess, 5.0f);
                    }

                    // They have enough points and host said they weren't finalist so they must've won and it was missed
                    player.Value.winnerPosition = TopOutTracker.winnerCount;
                    player.Value.finalist = false;
                    ++TopOutTracker.winnerCount;
                }
            }
        }
        else if (messageLines.Length == 2 && Regex.Replace(messageLines[0], "<.*?>", string.Empty) == "Top->Out Winner:")
        {
            parseWinner(Regex.Replace(messageLines[1], "<.*?>", string.Empty));
        }
    }

    public static void announceFinalists()
    {
        if (TopOutTracker.finalistCount > 0)
        {
            string chatMessage = "Reached Top:";

            foreach (KeyValuePair<ulong, TopOutPlayer> participant in TopOutTracker.participants)
            {
                if (participant.Value.finalist) chatMessage += $"<br>{participant.Value.username}";
            }

            ChatApi.SendMessage(chatMessage);
        }
    }
    
    public static void announceWinner(string username)
    {
        string chatMessage = $"Top->Out Winner:<br>{TopOutTracker.winnerCount}) {username}";
        ChatApi.SendMessage(chatMessage);
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

    public static void setServerMessage()
    {
        int messageTimer = TopOutTracker.tournamentSettings.roundTime;
        if (TopOutTracker.tournamentSettings.firstFinishStartsTimer) messageTimer += TopOutTracker.tournamentSettings.timeFromFirstFinish;
        ChatApi.SendMessage($"/servermessage white {messageTimer} Top->Out: {TopOutTracker.topThreshold} Championship Points | {TopOutTracker.tournamentSettings.winners} Winners");
    }

    public static void getTournamentParams(byte messageType, Color color, string message)
    {
        if (!(TopOutTracker.tournamentState == TopOutState.Inactive || TopOutTracker.tournamentState == TopOutState.Shutdown)
            && messageType == 2 && message.StartsWith("Top->Out: ") && !ZeepkistNetwork.IsMasterClient)
        {
            try
            {
                string[] parameters = message.Substring(10).Split(" | ", 2);
                if (parameters.Length != 2) return;

                string[] thresholdParam = parameters[0].Split(' ', 2);
                if (!(thresholdParam.Length == 2 & thresholdParam[1] == "Championship Points")) return;

                string[] winnersParam = parameters[1].Split(' ', 2);
                if (!(winnersParam.Length == 2 && winnersParam[1] == "Winners")) return;
                
                TopOutTracker.topThreshold = Int32.Parse(parameters[0].Split(' ', 2)[0]);
                TopOutTracker.tournamentSettings.winners = Int32.Parse(parameters[1].Split(' ', 2)[0]);
                TopOutLogger.Instance.LogInfo($"Server message set topThreshold to {TopOutTracker.topThreshold} and winners to {TopOutTracker.tournamentSettings.winners}");
            }
            catch
            {
                TopOutLogger.Instance.LogWarning($"Malformed \"Top->Out: \" server message {message}");
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
                    TopOutTracker.participants.Add(player.SteamID, new TopOutPlayer(player.GetTaggedUsername())
                    {
                        finalist = true,
                        winnerPosition = -3
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
                TopOutTracker.participants[steamID].winnerPosition = -3;
                ++TopOutTracker.finalistCount;
            }
            else
            {
                TopOutTracker.participants[steamID].winnerPosition = -3;
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
                            TopOutTracker.participants.Add(player.SteamID, new TopOutPlayer(player.GetTaggedUsername())
                            {
                                finalist = false,
                                winnerPosition = winnerRank
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
                        TopOutTracker.winnerCount = winnerRank + 1;

                        if (steamID == ZeepkistNetwork.LocalPlayer.SteamID)
                        {
                            MessengerApi.LogCustomColors("YOU WON!!!", TopOutColors.colorText, TopOutColors.colorSuccess, 5.0f);
                        }
                    }
                    else if (TopOutTracker.participants[steamID].winnerPosition != winnerRank) {
                        TopOutTracker.participants[steamID].winnerPosition = winnerRank;
                    }
                }
            }
            catch
            {
                TopOutLogger.Instance.LogInfo($"Unable to determine winner from chat: {winnerString}");
            }
        }
    }
}
