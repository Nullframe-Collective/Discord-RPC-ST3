using Il2Cpp;
using Il2CppFishNet.Transporting.Relay;
using MelonLoader;
using System.Text.RegularExpressions;
using UnityEngine;

[assembly: MelonInfo(typeof(DiscordRPC.Core), "DiscordRPC", "1.0.0", "Nullframe Collective", null)]
[assembly: MelonGame("ZeoWorks", "Slendytubbies 3")]
[assembly: MelonColor(255, 88, 101, 242)]

namespace DiscordRPC
{
    public class Core : MelonMod
    {
        private DiscordRpcClient _discordClient;
        private Timestamps _sessionStartTime;
        private RelayTransport _relayTransport;
        private object _monitorCoroutine;

        private bool _isInGameScene;
        private int _lastPlayerCount = -1;
        private string _lastSceneName;

        private const string DISCORD_APP_ID = "1556206405562802226";

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Discord RPC Mod Initialized");

            _discordClient = new DiscordRpcClient(DISCORD_APP_ID);
            _discordClient.Initialize();
        }

        public override void OnDeinitializeMelon() => _discordClient?.Dispose();

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            _lastSceneName = sceneName;
            _sessionStartTime = Timestamps.Now;

            string lowerSceneName = sceneName.ToLower();
            _isInGameScene = !lowerSceneName.Contains("mainmenu") &&
                             !lowerSceneName.Contains("updater");

            if (_monitorCoroutine != null) MelonCoroutines.Stop(_monitorCoroutine);

            if (_isInGameScene)
            {
                _monitorCoroutine = MelonCoroutines.Start(OptimizedPlayerMonitor());

                RoomInfo roomInfo = GetRoomInfo(sceneName);

                if (roomInfo.CurrentPlayers > 0)
                {
                    string details = $"Room: {roomInfo.Name}";
                    string state = $"Map: {roomInfo.DisplayName}";

                    UpdateDiscordPresence(state, details, roomInfo);
                }
            }
            else
            {
                _relayTransport = GameObject.FindObjectOfType<RelayTransport>();

                string details = lowerSceneName.Equals("updater") ? "Configurator" : "Main Menu";
                UpdateDiscordPresence("In Menu", details);
            }
        }

        private RoomInfo GetRoomInfo(string sceneName)
        {
            var roomInfo = new RoomInfo
            {
                Name = _relayTransport.OfflineMode ? "Solo" : sceneName,
                SceneName = CleanSceneName(sceneName),
                DisplayName = sceneName
            };

            try
            {
                if (_relayTransport == null || _relayTransport.OfflineMode)
                {
                    roomInfo.CurrentPlayers = 1;
                    roomInfo.MaxPlayers = 1;
                    return roomInfo;
                }

                if (_relayTransport.CurrentRoom != null)
                {
                    roomInfo.Name = _relayTransport.CurrentRoom.Name;
                    roomInfo.CurrentPlayers = _relayTransport.CurrentRoom.PlayerCount;
                    roomInfo.MaxPlayers = _relayTransport.CurrentRoom.MaxPlayers;
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Warning($"Failed to get room info: {e.Message}");
                roomInfo.CurrentPlayers = 1;
                roomInfo.MaxPlayers = 1;
            }

            return roomInfo;
        }

        private string CleanSceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return sceneName;

            string cleaned = Regex.Replace(sceneName, @"\s*\((Day|Night|Dusk)\)\s*", "");
            cleaned = cleaned.Replace("(S2)", "S2");
            cleaned = cleaned.Replace(' ', '_');

            return cleaned.Trim().ToLower();
        }

        private void UpdateDiscordPresence(string state, string details, RoomInfo roomInfo = null)
        {
            var presence = new RichPresence
            {
                State = state,
                Details = details,
                Timestamps = _sessionStartTime,
            };

            if (roomInfo != null)
            {
                presence.Assets = new Assets
                {
                    SmallImageKey = roomInfo.SceneName,
                    SmallImageText = "By Nullframe Collective",
                };

                if (roomInfo.CurrentPlayers > 0 && roomInfo.MaxPlayers > 0)
                {
                    presence.Party = new Party
                    {
                        ID = "st3_party",
                        Size = roomInfo.CurrentPlayers,
                        Max = roomInfo.MaxPlayers,
                    };                  
                }
            }

            _discordClient.SetPresence(presence);
        }

        public void OnPlayerCountChanged()
        {
            RoomInfo roomInfo = GetRoomInfo(_lastSceneName);

            if (roomInfo.CurrentPlayers > 0)
            {
                string details = $"Room: {roomInfo.Name}";
                string state = $"Map: {roomInfo.DisplayName}";

                UpdateDiscordPresence(state, details, roomInfo);
            }
        }

        private System.Collections.IEnumerator OptimizedPlayerMonitor()
        {
            while (_isInGameScene && _relayTransport.CurrentRoom != null)
            {
                int currentCount = _relayTransport.CurrentRoom.PlayerCount;
                if (currentCount != _lastPlayerCount)
                {
                    _lastPlayerCount = currentCount;
                    OnPlayerCountChanged();
                }

                yield return new WaitForSeconds(3f);
            }
        }
    }
}