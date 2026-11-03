using System;
using System.IO;
using System.Collections.Generic;
using MelonLoader;
using MelonLoader.Utils;
using maimai_rpc;

[assembly: MelonInfo(typeof(maimai_rpc.maimai_rpc), maimai_rpc.BuildInfo.Name, maimai_rpc.BuildInfo.Version, maimai_rpc.BuildInfo.Author, maimai_rpc.BuildInfo.DownloadLink)]
[assembly: MelonGame("sega-interactive", "Sinmai")]

namespace maimai_rpc
{
    public static class BuildInfo
    {
        public const string Name = "maimai-rpc";
        public const string Description = "Discord RPC for Sinmai";
        public const string Author = "Baconway";
        public const string Company = "bwaycheats";
        public const string Version = "1.0.0";
        public const string DownloadLink = null;
    }

    public class maimai_rpc : MelonMod
    {
        public static maimai_rpc Instance { get; private set; }

        public static string ConfigPath { get; private set; }
        public static IniFile Config { get; private set; }

        public static string PlaceholderValue { get; private set; }
        public static string DiscordAppId { get; private set; }
        public static string DefaultLargeImage { get; private set; }
        public static int PlayerSlot { get; private set; }
        public static int UpdateIntervalMs { get; private set; }

        private float startupDelayTime = 0f;
        private bool isRpcStarted = false;

        public RPC RPCHandler { get; private set; }

        static maimai_rpc()
        {
            PlaceholderValue = "PlaceholderValue";
            DiscordAppId = "1208326104977375233";
            DefaultLargeImage = "https://cdn.discordapp.com/app-icons/1208326104977375233/46e56822bb49b482ff81bb8b195d4ac1.png";
            PlayerSlot = 0;
            UpdateIntervalMs = 1000;
        }

        public override void OnInitializeMelon()
        {
            Instance = this;
            PrintModInfo();
            LoadConfig();
        }

        [Obsolete]
        public override void OnApplicationLateStart()
        {
            RPCHandler = new RPC(DiscordAppId);
            // Delay connection 3 seconds after late start to ensure asset loading and Harmony patching have finished
            startupDelayTime = UnityEngine.Time.time + 3.0f;
            LoggerInstance.Msg("[Discord] RPC initialized. Will connect in 3 seconds...");
        }

        public override void OnUpdate()
        {
            if (RPCHandler == null)
            {
                return;
            }

            // Check if startup delay has finished before initiating the connection
            if (!isRpcStarted)
            {
                if (UnityEngine.Time.time >= startupDelayTime)
                {
                    isRpcStarted = true;
                    LoggerInstance.Msg("[Discord] Connecting to Discord IPC...");
                    RPCHandler.init();
                }
            }

            // Event-driven: Presence updates are dispatched directly by Harmony patches (ProcessPatch).
            // No polling or RPCHandler.set() in OnUpdate() needed!
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("[Discord] Shutting down RPC...");
            if (RPCHandler != null)
            {
                RPCHandler.endRPC();
            }
        }

        /// <summary>
        /// Prints basic information about the mod to the MelonLoader console when loaded.
        /// </summary>
        public void PrintModInfo()
        {
            LoggerInstance.Msg("========================================");
            LoggerInstance.Msg(string.Format("{0} v{1}", BuildInfo.Name, BuildInfo.Version));
            LoggerInstance.Msg(string.Format("Author: {0}", BuildInfo.Author));
            LoggerInstance.Msg(string.Format("Company: {0}", BuildInfo.Company));
            LoggerInstance.Msg(string.Format("Description: {0}", BuildInfo.Description));
            LoggerInstance.Msg("========================================");
        }

        /// <summary>
        /// Loads or creates the maimai-rpc.ini configuration file in the parent directory (./maimai-rpc.ini).
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                // Parent directory of the mod (Game root directory: ./maimai-rpc.ini)
                string baseDir = MelonEnvironment.GameRootDirectory != null ? MelonEnvironment.GameRootDirectory : Directory.GetCurrentDirectory();
                ConfigPath = Path.Combine(baseDir, "maimai-rpc.ini");

                if (!File.Exists(ConfigPath))
                {
                    LoggerInstance.Msg(string.Format("Configuration file not found. Creating default at '{0}'...", ConfigPath));

                    string defaultIniContent =
@"[General]
; Placeholder value for testing configuration loading
PlaceholderKey = PlaceholderValue

[Discord]
; Discord Application / Client ID
ApplicationID = 1208326104977375233

; Default large image URL / asset key (Discord application icon)
DefaultLargeImage = https://cdn.discordapp.com/app-icons/1208326104977375233/46e56822bb49b482ff81bb8b195d4ac1.png

; Update interval in milliseconds
UpdateIntervalMs = 1000
";
                    File.WriteAllText(ConfigPath, defaultIniContent);
                    LoggerInstance.Msg("Default configuration file created successfully.");
                }

                // Load configuration using MelonLoader's built-in IniFile parser
                Config = new IniFile(ConfigPath);

                PlaceholderValue = Config.GetString("General", "PlaceholderKey", "PlaceholderValue", false);
                DiscordAppId = Config.GetString("Discord", "ApplicationID", "1208326104977375233", false);
                DefaultLargeImage = Config.GetString("Discord", "DefaultLargeImage", "https://cdn.discordapp.com/app-icons/1208326104977375233/46e56822bb49b482ff81bb8b195d4ac1.png", false);
                UpdateIntervalMs = Config.GetInt("Discord", "UpdateIntervalMs", 1000, false);

                LoggerInstance.Msg(string.Format("Configuration loaded from: {0}", ConfigPath));
                LoggerInstance.Msg(string.Format("[Config] PlaceholderKey = {0}", PlaceholderValue));
                LoggerInstance.Msg(string.Format("[Discord] ApplicationID = {0}", DiscordAppId));
                LoggerInstance.Msg(string.Format("[Discord] DefaultLargeImage = {0}", DefaultLargeImage));
            }
            catch (Exception ex)
            {
                LoggerInstance.Error(string.Format("Failed to load configuration file: {0}", ex.Message));
            }
        }
    }
}
