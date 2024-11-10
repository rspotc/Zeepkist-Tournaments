using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

using ZeepkistClient;

using ZeepSDK.ChatCommands;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;
using ZeepSDK.Chat;
using ZeepSDK.Level;
using System;
using System.Collections.Generic;
using ZeepkistNetworking;
using BepInEx.Logging;

namespace Zeepkist;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    private static ConfigEntry<string> ConfigMedal;
    private static ConfigEntry<int> ConfigLives;

    private static bool survivalLobby = false;
    private static bool survivalSolo = false;
    private static Dictionary<ulong, SurvivalPlayer> participants = new Dictionary<ulong, SurvivalPlayer>();

    private void Awake()
    {
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        SurvivalLogger.Instance = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_GUID);
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
            "Survival Requirement",
            "Bronze",
            new ConfigDescription("Requirement for survival\n(\"Finish\", \"Bronze\", \"Silver\", \"Gold\", \"Author\"", new AcceptableValueList<string>(["Finish", "Bronze", "Silver", "Gold", "Author"])));
        Plugin.ConfigLives = this.Config.Bind<int>(
            "Settings",
            "Lives Requirement",
            1,
            new ConfigDescription("Lives a player has before failing survival", new AcceptableValueRange<int>(0, 100)));
        Plugin.ConfigMedal.SettingChanged += new EventHandler((object o, EventArgs e) => setServerMessage());
    }

    private void setupTriggers()
    {
        RacingApi.LevelLoaded += setServerMessage;
        RacingApi.RoundEnded += awardPoints;
        RacingApi.RoundStarted += resetTimes;
        MultiplayerApi.DisconnectedFromGame += endTournament;
        MultiplayerApi.PlayerJoined += playerJoined;
        ZeepkistNetwork.MasterChanged += hostChanged;
        ChatCommandApi.RegisterLocalChatCommand("/", "survival", "\"start\"/\"stop\"/\"solo\" Time is Right Tournament", engageTournament);
    }

    private void deleteTriggers()
    {
        RacingApi.LevelLoaded -= setServerMessage;
        RacingApi.RoundEnded -= awardPoints;
        RacingApi.RoundStarted -= resetTimes;
        MultiplayerApi.DisconnectedFromGame -= endTournament;
        MultiplayerApi.PlayerJoined -= playerJoined;
        ZeepkistNetwork.MasterChanged -= hostChanged;
    }

    private void hostChanged(ZeepkistNetworkPlayer player)
    {
        if (survivalLobby) endTournament();
    }

    private void resetTimes()
    {
        foreach (KeyValuePair<ulong, SurvivalPlayer> participant in participants)
        {
            participant.Value.currentTime = 0;
            if (!(ZeepkistNetwork.LocalPlayerHasHostPowers() && survivalLobby)) continue;

            ZeepkistNetwork.CustomLeaderBoard_SetPlayerChampionshipPoints(participant.Key, participant.Value.points, 0, false);
            ZeepkistNetwork.CustomLeaderBoard_SetPlayerLeaderboardOverrides(participant.Key, pointsWon: $"Lives: {participant.Value.lives}");
        }
    }

    private void addParticipant(ZeepkistNetworkPlayer player)
    {
        if (!participants.ContainsKey(player.SteamID))
        {
            participants.Add(player.SteamID, new SurvivalPlayer(player.GetTaggedUsername())
            {
                currentTime = 0,
                lives = Plugin.ConfigLives.Value,
                points = 0,
            });
        }
    }

    private void engageTournament(string args)
    {
        if (args == "start")
        {
            survivalLobby = true;
            survivalSolo = false;
            startTournament();
        }
        else if (args == "stop")
        {
            endTournament();
            if (ZeepkistNetwork.LocalPlayerHasHostPowers()) ChatApi.SendMessage("/servermessage remove");
        }
        else if (args == "solo")
        {
            survivalLobby = false;
            survivalSolo = true;
            if (ZeepkistNetwork.LocalPlayerHasHostPowers()) ChatApi.SendMessage("/servermessage remove");
            startTournament();
        }
    }

    private void setServerMessage()
    {
        string conditionStr;
        if (Plugin.ConfigMedal.Value == "Finish") conditionStr = "Get a Finish";
        else conditionStr = $"Beat Time {targetTime(Plugin.ConfigMedal.Value).GetFormattedTime()} ({Plugin.ConfigMedal.Value})";

        if (survivalLobby && ZeepkistNetwork.LocalPlayerHasHostPowers())
        {
            ChatApi.SendMessage($"/servermessage white {ZeepkistNetwork.CurrentLobby.RoundTime} Survival<br>Condition: {conditionStr}");
        }
        else if (survivalSolo)
        {
            ChatApi.AddLocalMessage($"[SURVIVAL] Condition: {conditionStr}<br>You have {participants[ZeepkistNetwork.LocalPlayer.SteamID].points} points and {participants[ZeepkistNetwork.LocalPlayer.SteamID].lives} lives remaining.");
        }
    }

    private void startTournament()
    {
        registerPlayers();
        setServerMessage();
        SurvivalLogger.Instance.LogDebug($"Lobby {survivalLobby} Solo {survivalSolo}");
    }

    private void endTournament()
    {
        participants.Clear();
        survivalLobby = false;
        survivalSolo = false;
    }

    private void playerJoined(ZeepkistNetworkPlayer player)
    {
        if (ZeepkistNetwork.LocalPlayerHasHostPowers() && survivalLobby)
        {
            addParticipant(player);
            if (participants[player.SteamID].currentTime > 0) ZeepkistNetwork.CustomLeaderBoard_SetPlayerTimeOnLeaderboard(player.SteamID, participants[player.SteamID].currentTime, false);
        }
    }

    private void registerPlayers()
    {
        participants.Clear();
        if (survivalSolo)
        {
            addParticipant(ZeepkistNetwork.LocalPlayer);
        }
        else if (ZeepkistNetwork.LocalPlayerHasHostPowers() && survivalLobby)
        {
            foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
            {
                addParticipant(player);
                ZeepkistNetwork.CustomLeaderBoard_SetPlayerChampionshipPoints(player.SteamID, participants[player.SteamID].points, 0, false);
                ZeepkistNetwork.CustomLeaderBoard_SetPlayerLeaderboardOverrides(player.SteamID, pointsWon: $"Lives: {participants[player.SteamID].lives}");
            }
        }
    }

    private static float targetTime(string medal)
    {
        if (medal == "Bronze") return LevelApi.CurrentLevel.TimeBronze;
        else if (medal == "Silver") return LevelApi.CurrentLevel.TimeSilver;
        else if (medal == "Gold") return LevelApi.CurrentLevel.TimeGold;
        else if (medal == "Author") return LevelApi.CurrentLevel.TimeAuthor;
        return 0;
    }

    private static void awardPoints()
    {
        foreach (KeyValuePair<ulong, SurvivalPlayer> participant in participants)
        {
            SurvivalPlayer survivalPlayer = participant.Value;
            bool dead = false;
            SurvivalLogger.Instance.LogDebug($"Time: {survivalPlayer.currentTime} Lives: {survivalPlayer.lives} Condition: {Plugin.ConfigMedal.Value}");
            if (survivalPlayer.currentTime != 0 && (Plugin.ConfigMedal.Value == "Finish" || survivalPlayer.currentTime <= targetTime(Plugin.ConfigMedal.Value))) survivalPlayer.points++;
            else survivalPlayer.lives--;

            SurvivalLogger.Instance.LogDebug($"Died? {survivalPlayer.lives}");
            if (survivalPlayer.lives <= 0)
            {
                dead = true;
                survivalPlayer.points = 0;
                survivalPlayer.lives = Plugin.ConfigLives.Value;
            }

            if (survivalSolo && ZeepkistNetwork.LocalPlayer.SteamID == participant.Key)
            {
                SurvivalLogger.Instance.LogDebug($"End Round Solo");
                if (dead) ChatApi.AddLocalMessage("[SURVIVAL] You failed to survive. Try Again.");
            }
            else if (survivalLobby && ZeepkistNetwork.LocalPlayerHasHostPowers())
            {
                // Make sure the player is in the lobby
                foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
                {
                    if (player.SteamID != participant.Key) continue;
                    
                    if (dead) ZeepkistNetwork.SendCustomChatMessage(false, player.SteamID, "You failed to survive. Try Again.", "SURVIVAL");
                }
            }
        }
    }

    [HarmonyPatch(typeof(ZeepkistNetwork), "OnLeaderboard")]
    public class ZeepkistNetwork_OnLeaderboard
    {
        private static void Postfix()
        {
            foreach (LeaderboardItem playerLeaderboard in ZeepkistNetwork.Leaderboard)
            {
                if (!participants.ContainsKey(playerLeaderboard.SteamID)) continue;

                participants[playerLeaderboard.SteamID].currentTime = playerLeaderboard.Time;
            }
        }
    }
}
