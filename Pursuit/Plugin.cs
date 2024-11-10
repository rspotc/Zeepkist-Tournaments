using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Lidgren.Network;

using ZeepkistClient;

using ZeepSDK.ChatCommands;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;
using ZeepkistNetworking;
using ZeepSDK.Chat;
using ZeepSDK.Level;
using System;
using System.Collections.Generic;
using FMODSyntax;
using UnityEngine;
using ZeepSDK.Messaging;

namespace Zeepkist;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    private static ConfigEntry<string> ConfigMedal;
    private static ConfigEntry<bool> ConfigHardcore;

    private static bool timeIsRightLobby = false;
    private static bool timeIsRightSolo = false;
    private static List<LeaderboardItem> leaderboard = new List<LeaderboardItem>();

    private void Awake()
    {
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        setupConfig();
        setupTriggers();

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnDestroy()
    {
        deleteTriggers();

        harmony?.UnpatchSelf();
        harmony = null;
    }

    private void setupConfig()
    {
        Plugin.ConfigMedal = this.Config.Bind<string>(
            "Settings",
            "Medal Target",
            "Bronze",
            new ConfigDescription("Medal that players mush shoot for, but not beat\n(\"Bronze\", \"Silver\", \"Gold\", \"Author\"", new AcceptableValueList<string>(["Bronze", "Silver", "Gold", "Author"])));
        Plugin.ConfigHardcore = this.Config.Bind<bool>("Settings", "Hardcore Mode", false, "A successful time is wiped when a failed time is set.\n(Not available in solo mode)");

        Plugin.ConfigMedal.SettingChanged += new EventHandler((object o, EventArgs e) => startTournament());
        Plugin.ConfigHardcore.SettingChanged += new EventHandler((object o, EventArgs e) => startTournament());
    }

    private void setupTriggers()
    {
        RacingApi.LevelLoaded += setServerMessage;
        MultiplayerApi.DisconnectedFromGame += endTournament;
        RacingApi.CrossedFinishLine += removeSelf;
        ChatCommandApi.RegisterLocalChatCommand("/", "timeisright", "\"start\"/\"stop\" Time is Right Tournament", engageTournament);
    }

    private void deleteTriggers()
    {
        RacingApi.LevelLoaded -= setServerMessage;
        MultiplayerApi.DisconnectedFromGame -= endTournament;
    }

    private void engageTournament(string args)
    {
        if (args == "start")
        {
            timeIsRightLobby = true;
            timeIsRightSolo = false;

            startTournament();
        }
        else if (args == "stop")
        {
            endTournament();
            if (ZeepkistNetwork.LocalPlayerHasHostPowers()) ChatApi.SendMessage("/servermessage remove");
        }
        else if (args == "solo")
        {
            timeIsRightLobby = false;
            timeIsRightSolo = true;
            startTournament(true);
        }
    }

    private void setServerMessage()
    {
        if (timeIsRightLobby && ZeepkistNetwork.LocalPlayerHasHostPowers())
        {
            ChatApi.SendMessage($"/servermessage white {ZeepkistNetwork.CurrentLobby.RoundTime} The Time Is Right<br>Be as close to, but slower than: {targetTime(Plugin.ConfigMedal.Value).GetFormattedTime()} ({Plugin.ConfigMedal.Value})");
        }
        else if (timeIsRightSolo)
        {
            ChatApi.AddLocalMessage("The Time Is Right<br>Be as close to, but slower than: {targetTime(Plugin.ConfigMedal.Value).GetFormattedTime()} ({Plugin.ConfigMedal.Value})");
        }
    }

    private void startTournament(bool solo=false)
    {
        if (ZeepkistNetwork.LocalPlayerHasHostPowers() && timeIsRightLobby)
        {
            timeIsRightSolo = false;
            leaderboard = ZeepkistNetwork.Leaderboard;
            setServerMessage();
            removePlayers();
        }
        else if (timeIsRightSolo)
        {
            timeIsRightLobby = false;
            if (ZeepkistNetwork.LocalPlayer.removeSelf(Z);
        }
    }

    private void endTournament()
    {
        timeIsRightLobby = false;
        timeIsRightSolo = false;
    }

    private static float targetTime(string medal)
    {
        if (medal == "Bronze") return LevelApi.CurrentLevel.TimeBronze;
        else if (medal == "Silver") return LevelApi.CurrentLevel.TimeSilver;
        else if (medal == "Gold") return LevelApi.CurrentLevel.TimeGold;
        else if (medal == "Author") return LevelApi.CurrentLevel.TimeAuthor;
        return 0;
    }

    private static void removePlayers()
    {
        foreach (LeaderboardItem player in ZeepkistNetwork.Leaderboard)
        {
            if (player.Time >= targetTime(Plugin.ConfigMedal.Value)) continue;

            bool shouldRemove = Plugin.ConfigHardcore.Value;
            if (!shouldRemove)
            {
                bool foundPlayer = false;
                foreach (LeaderboardItem oldLeaderboard in leaderboard)
                {
                    if (oldLeaderboard.SteamID != player.SteamID) continue;
                    foundPlayer = true;

                    if (oldLeaderboard.Time >= targetTime(Plugin.ConfigMedal.Value)) ZeepkistNetwork.CustomLeaderBoard_SetPlayerTimeOnLeaderboard(player.SteamID, oldLeaderboard.Time);
                    else shouldRemove = true;
                    break;
                }
                if (!foundPlayer) shouldRemove = true;
            }
            if (shouldRemove) ZeepkistNetwork.CustomLeaderBoard_RemovePlayerFromLeaderboard(player.SteamID);
            ZeepkistNetwork.SendCustomChatMessage(false, player.SteamID, "You were too fast. Try again :zaagbladpadrood2:");
        }
    }

    [HarmonyPatch(typeof(ZeepkistNetwork), "OnLeaderboard")]
    public class ZeepkistNetwork_OnLeaderboard
    {
        private static void Postfix()
        {
            if (!(timeIsRightLobby && ZeepkistNetwork.LocalPlayerHasHostPowers())) return;

            removePlayers();
            leaderboard = ZeepkistNetwork.Leaderboard;
        }
    }

    private void removeSelf(float time)
    {
        if (timeIsRightSolo && (time < targetTime(Plugin.ConfigMedal.Value)))
        {
            ZeepkistNetwork.NetworkClient?.SendPacket<CLB_Packet_AddRemovePlayerFromLeaderboard>(new CLB_Packet_AddRemovePlayerFromLeaderboard()
            {
                SteamID = ZeepkistNetwork.LocalPlayer.SteamID,
                removePerson = true,
                time = 0.0f,
                checkpoints = 0
            });
        }
    }
}
