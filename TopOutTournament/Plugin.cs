﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

using System;
using System.IO;
using System.Collections.Generic;

using TMPro;

using ZeepkistClient;

using ZeepSDK.ChatCommands;
using ZeepSDK.Messaging;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;
using ZeepkistNetworking;

namespace Zeepkist;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    private static TopOutSettings tournamentSettings;

    private static ConfigEntry<bool> ConfigFirstFinishStartsTimer;
    private static ConfigEntry<bool> ConfigPrivateRoom;
    private static ConfigEntry<int> ConfigAbsoluteThresholdVal;
    private static ConfigEntry<int> ConfigWinnerCount;
    private static ConfigEntry<int> ConfigRoundTimer;
    private static ConfigEntry<int> ConfigTimeAfterFirstFinish;
    private static ConfigEntry<bool> ConfigAllowNuisances;
    private static ConfigEntry<bool> ConfigWinnersBecomeNuisance;

    private void Awake()
    {
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        TopOutLogger.Instance = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_GUID); 
        setupConfig();
        setupTriggers();

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
        Plugin.ConfigFirstFinishStartsTimer = this.Config.Bind<bool>("Timer", "Set Countdown on First Finish", true, "Round timer resets to the countdown timer\nvalue after someone finishes");
        Plugin.ConfigPrivateRoom = this.Config.Bind<bool>("Room", "Private Lobby for Tournament", false, "Make the room private during the tournament\nand public again when it ends");
        Plugin.ConfigAbsoluteThresholdVal = this.Config.Bind<int>(
            "Points",
            "Absolute Point Threshold",
            100,
            new ConfigDescription("Number of points required to reach top", new AcceptableValueRange<int>(64, 6400)));
        Plugin.ConfigWinnerCount = this.Config.Bind<int>(
            "Other",
            "Number of Winners",
            3,
            new ConfigDescription("Number of players that must win\nto automatically end tournament", new AcceptableValueRange<int>(1, 10)));
        Plugin.ConfigRoundTimer = this.Config.Bind<int>(
            "Timer",
            "Tournament Round Timer",
            300,
            new ConfigDescription("Round timer value to set when a round starts", new AcceptableValueRange<int>(30, 86400)));
        Plugin.ConfigTimeAfterFirstFinish = this.Config.Bind<int>(
            "Timer",
            "Countdown Time from First Finish",
            60,
            new ConfigDescription("Time allowed for other competitors\nto finish after first finisher", new AcceptableValueRange<int>(30, 86400)));
        Plugin.ConfigAllowNuisances = this.Config.Bind<bool>("Other", "Turn on Nuisance Players", false, "Players in nuisance.txt file can't win,\njust inhibit progress of other players");
        Plugin.ConfigWinnersBecomeNuisance = this.Config.Bind<bool>("Other", "Winners become Nuisance", false, "Winners can still set times to act as a nuisance");

        tournamentSettings.toggleFirstFinishTimer(Plugin.ConfigFirstFinishStartsTimer.Value);
        tournamentSettings.togglePrivateRoomOption(Plugin.ConfigPrivateRoom.Value);
        tournamentSettings.setThreshold(Plugin.ConfigAbsoluteThresholdVal.Value);
        tournamentSettings.setWinnerCount(Plugin.ConfigWinnerCount.Value);
        tournamentSettings.setRoundTimer(Plugin.ConfigRoundTimer.Value);
        tournamentSettings.setTimerFromFirstFinish(Plugin.ConfigTimeAfterFirstFinish.Value);
        tournamentSettings.toggleNuisances(Plugin.ConfigAllowNuisances.Value);
        tournamentSettings.toggleWinnerNuisance(Plugin.ConfigWinnersBecomeNuisance.Value);

        Plugin.ConfigFirstFinishStartsTimer.SettingChanged += new EventHandler((object o, EventArgs e) => tournamentSettings.toggleFirstFinishTimer(Plugin.ConfigFirstFinishStartsTimer.Value));
        Plugin.ConfigPrivateRoom.SettingChanged += new EventHandler((object o, EventArgs e) => tournamentSettings.togglePrivateRoomOption(Plugin.ConfigPrivateRoom.Value));
        Plugin.ConfigAbsoluteThresholdVal.SettingChanged += new EventHandler((object o, EventArgs e) => tournamentSettings.setThreshold(Plugin.ConfigAbsoluteThresholdVal.Value));
        Plugin.ConfigWinnerCount.SettingChanged += new EventHandler((object o, EventArgs e) => tournamentSettings.setWinnerCount(Plugin.ConfigWinnerCount.Value));
        Plugin.ConfigRoundTimer.SettingChanged += new EventHandler((object o, EventArgs e) => tournamentSettings.setRoundTimer(Plugin.ConfigRoundTimer.Value));
        Plugin.ConfigTimeAfterFirstFinish.SettingChanged += new EventHandler((object o, EventArgs e) => tournamentSettings.setTimerFromFirstFinish(Plugin.ConfigTimeAfterFirstFinish.Value));
        Plugin.ConfigAllowNuisances.SettingChanged += new EventHandler((object o, EventArgs e) => tournamentSettings.toggleNuisances(Plugin.ConfigAllowNuisances.Value));
        Plugin.ConfigWinnersBecomeNuisance.SettingChanged += new EventHandler((object o, EventArgs e) => tournamentSettings.toggleWinnerNuisance(Plugin.ConfigWinnersBecomeNuisance.Value));

        string modStorage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zeepkist", "Mods", MyPluginInfo.PLUGIN_GUID);
        Directory.CreateDirectory(modStorage);
        TopOutTracker.nuisanceFile = Path.Combine(modStorage, "nuisance.txt");
        if (!File.Exists(TopOutTracker.nuisanceFile)) File.Create(TopOutTracker.nuisanceFile);
    }

    private void setupTriggers()
    {
        RacingApi.LevelLoaded += TopOutTracker.startRound;
        RacingApi.RoundEnded += TopOutTracker.endRound;

        MultiplayerApi.PlayerJoined += TopOutTracker.playerJoined;
        MultiplayerApi.DisconnectedFromGame += TopOutTracker.localPlayerLeft;

        ChatCommandApi.RegisterLocalChatCommand("/", "topout", "\"start\"/\"stop\"/\"join\" Top->Out Tournament", engageTournament);
        ZeepkistNetwork.ChatMessageReceived += new Action<ZeepkistChatMessage>(TopOutChatManager.parseChatForTournamentInfo);
        ZeepkistNetwork.MasterChanged += TopOutTracker.hostChanged;
    }

    private void deleteTriggers()
    {
        RacingApi.LevelLoaded -= TopOutTracker.startRound;
        RacingApi.RoundEnded -= TopOutTracker.endRound;

        MultiplayerApi.PlayerJoined -= TopOutTracker.playerJoined;
        MultiplayerApi.DisconnectedFromGame -= TopOutTracker.localPlayerLeft;

        ZeepkistNetwork.ChatMessageReceived -= new Action<ZeepkistChatMessage>(TopOutChatManager.parseChatForTournamentInfo);
        ZeepkistNetwork.MasterChanged -= TopOutTracker.hostChanged;
    }

    // Important this stays here because this tournamentSettings is the one interfacing with Config
    public static void engageTournament(string args)
    {
        TopOutLogger.Instance.LogInfo($"Top->Out Tournament command {args} received.");

        if (args == "start")
        {
            TopOutTracker.startTournament(tournamentSettings);
        }
        else if (args == "stop")
        {
            TopOutTracker.endTournament();
        }
        else if (args == "join")
        {
            TopOutTracker.joinTournament();
        }
        else
        {
            MessengerApi.LogCustomColors($"{args} is not a \"/topout\" command.", TopOutColors.colorText, TopOutColors.colorFailure, 5.0f);
        }
    }

    [HarmonyPatch(typeof(ZeepkistNetwork), "OnLeaderboard")]
    public class ZeepkistNetwork_OnLeaderboard
    {
        private static void Postfix()
        {
            TopOutTracker.someoneFinished();
        }
    }

    [HarmonyPatch(typeof(OnlineGameplayUI), "Update")]
    public class OnlineGameplayUI_Update
    {
        private static void Postfix(TMP_Text ___TimeLeftText)
        {
            ___TimeLeftText.color = TopOutColors.colorTimer;
        }
    }

    [HarmonyPatch(typeof(SpectatorCameraUI), "Update")]
    public class SpectatorCameraUI_Update
    {
        private static void Postfix(TMP_Text ___Timer)
        {
            ___Timer.color = TopOutColors.colorTimer;
        }
    }

    [HarmonyPatch(typeof(SpectatorCameraUI), "Awake")]
    public class SpectatorCameraUI_Awake
    {
        private static void Postfix(List<GUI_OnlineLeaderboardPosition> ___leaderboard_ingame_positions)
        {
            TopOutTracker.spectatorLeaderboard.mainLeaderboard = ___leaderboard_ingame_positions;
        }
    }

    [HarmonyPatch(typeof(SpectatorCameraUI), "DrawIngameLeaderboard")]
    public class SpectatorCameraUI_DrawIngameLeaderboard
    {
        private static void Postfix()
        {
            if (TopOutTracker.tournamentState == TopOutState.Primed ||
                  TopOutTracker.tournamentState == TopOutState.Shutdown ||
                  TopOutTracker.tournamentState == TopOutState.Inactive)
            {
                return;
            }

            TopOutTracker.spectatorLeaderboard.displayLeaderboardPoints();
        }
    }

    [HarmonyPatch(typeof(OnlineGameplayUI), "Awake")]
    public class OnlineGameplayUI_Awake
    {
        private static void Postfix(List<GUI_OnlineLeaderboardPosition> ___leaderboard_ingame_positions, GUI_OnlineLeaderboardPosition ___leaderboard_your_position)
        {
            TopOutTracker.gameplayLeaderboard.mainLeaderboard = ___leaderboard_ingame_positions;
            TopOutTracker.gameplayLeaderboard.yourLeaderboard = ___leaderboard_your_position;
        }
    }

    [HarmonyPatch(typeof(OnlineGameplayUI), "DrawIngameLeaderboard")]
    public class OnlineGameplayUI_DrawIngameLeaderboard
    {
        private static void Postfix()
        {
            if (TopOutTracker.tournamentState == TopOutState.Primed ||
                  TopOutTracker.tournamentState == TopOutState.Shutdown ||
                  TopOutTracker.tournamentState == TopOutState.Inactive)
            {
                return;
            }

            TopOutTracker.gameplayLeaderboard.displayLeaderboardPoints();
        }
    }

    [HarmonyPatch(typeof(ZeepkistNetwork), "OnChangeLobbyTimer")]
    public class ZeepkistNetwork_OnChangeLobbyTimer
    {
        static void Postfix(ChangeLobbyTimerPacket packet)
        {
            TopOutChatManager.updateServerMessage(packet);
        }
    }

    [HarmonyPatch(typeof(ZeepkistNetwork), "SetMasterClient")]
    public class ZeepkistNetwork_SetMasterClient
    {
        static bool Prefix()
        {
            TopOutTracker.endTournament();
            return true;
        }
    }
}
