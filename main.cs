using Modding;
using UnityEngine;
using System;
using System.Collections;
using DiscordRPC;
using Steamworks;
using UnityEngine.SceneManagement;

namespace HollowPresence
{
    public class HollowPresence : Mod
    {
        private DiscordRpcClient discordClient;
        private long startTimeUnix;
        private bool steamInit = false;

        public override string GetVersion() => "2.0";

        public override void Initialize()
        {
            Log("HollowPresence (Discord + Steam) initialized!");

            // --- Discord ---
            discordClient = new DiscordRpcClient("YOUR_DISCORD_APP_CLIENT_ID");
            discordClient.Initialize();

            // --- Steam ---
            try
            {
                SteamClient.Init(367520);
                steamInit = true;
                Log("Steam Rich Presence initialized!");
            }
            catch (Exception e)
            {
                LogError("Steam initialization failed: " + e.Message);
            }

            startTimeUnix = DateTimeOffset.Now.ToUnixTimeSeconds();

            GameManager.instance.StartCoroutine(UpdatePresenceRoutine());
        }

        private IEnumerator UpdatePresenceRoutine()
        {
            while (true)
            {
                try { UpdatePresence(); }
                catch (Exception ex) { LogError($"Presence update failed: {ex}"); }

                yield return new WaitForSeconds(8f);
            }
        }

        private void UpdatePresence()
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;

            if (gm == null || pd == null) return;

            string scene = SceneManager.GetActiveScene().name;
            string biome = pd.GetString("currentMapZone");
            string details = "";
            string state = biome;

            // Bench detection
            if (pd.atBench)
                details = $"Resting at a Bench in {biome}";
            // Hot spring
            else if (scene.ToLower().Contains("hot_spring"))
                details = $"Relaxing in a Hot Spring in {biome}";
            // Stag station
            else if (scene.ToLower().Contains("stag"))
                details = $"At the Stag Station in {biome}";
            // Boss
            else if (IsBossPresent())
                details = $"Fighting {GetCurrentBossName()} in {biome}";
            else
                details = $"Exploring {biome}";

            // --- Discord ---
            discordClient.SetPresence(new RichPresence()
            {
                Details = details,
                State = state,
                Assets = new Assets()
                {
                    LargeImageKey = "hollowknight",
                    LargeImageText = "Hollow Knight",
                    SmallImageKey = GetSmallIcon(scene),
                    SmallImageText = scene
                },
                Timestamps = new Timestamps() { StartUnixMilliseconds = startTimeUnix }
            });

            // --- Steam ---
            if (steamInit)
            {
                SteamFriends.SetRichPresence("status", details);
                SteamFriends.SetRichPresence("steam_display", "#Status");
                SteamFriends.SetRichPresence("steam_status_detail", biome);
            }
        }

        private bool IsBossPresent()
        {
            var bosses = GameObject.FindObjectsOfType<HealthManager>();
            foreach (var hm in bosses)
            {
                if (hm.IsBoss())
                    return true;
            }
            return false;
        }

        private string GetCurrentBossName()
        {
            var bosses = GameObject.FindObjectsOfType<HealthManager>();
            foreach (var hm in bosses)
            {
                if (hm.IsBoss())
                {
                    string name = hm.gameObject.name;
                    name = name.Replace("(Clone)", "").Replace("_", " ");
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                }
            }
            return "Unknown Boss";
        }

        private string GetSmallIcon(string scene)
        {
            scene = scene.ToLower();
            if (scene.Contains("stag")) return "stag";
            if (scene.Contains("bench")) return "bench";
            if (scene.Contains("hot_spring")) return "hotspring";
            if (scene.Contains("dream")) return "dream";
            if (IsBossPresent()) return "boss";
            return "knight";
        }

        public override void Unload()
        {
            if (discordClient != null)
            {
                discordClient.ClearPresence();
                discordClient.Dispose();
                discordClient = null;
            }

            if (steamInit)
            {
                SteamFriends.ClearRichPresence();
                SteamClient.Shutdown();
                steamInit = false;
            }

            base.Unload();
        }
    }

    public static class HealthManagerExtensions
    {
        public static bool IsBoss(this HealthManager hm)
        {
            if (hm == null) return false;
            return hm.gameObject.CompareTag("Boss") || hm.name.ToLower().Contains("boss");
        }
    }
}
