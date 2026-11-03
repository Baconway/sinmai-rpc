using System;
using MelonLoader;

namespace maimai_rpc
{
    public class RPC
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DiscordIpcClient Client { get; private set; }

        public bool IsReady
        {
            get { return Client != null && Client.IsReady; }
        }

        public static long GetCurrentUnixTimestamp()
        {
            return (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
        }

        public RPC(string appId)
        {
            Client = new DiscordIpcClient(appId);
            Client.LogInfo = delegate(string msg) { MelonLogger.Msg("[DiscordRPC] " + msg); };
            Client.LogWarn = delegate(string msg) { MelonLogger.Warning("[DiscordRPC] " + msg); };
            Client.LogError = delegate(string msg) { MelonLogger.Error("[DiscordRPC] " + msg); };

            Client.OnReady += delegate(DiscordUser user)
            {
                MelonLogger.Msg(string.Format("[DiscordRPC] Successfully connected as {0}!", user.Username));
                GameStateHandler.ResendCurrentState();
            };

            Client.OnClose += delegate(int code, string reason)
            {
                MelonLogger.Warning(string.Format("[DiscordRPC] Connection closed ({0}): {1}", code, reason));
            };

            Client.OnError += delegate(string err)
            {
                MelonLogger.Error(string.Format("[DiscordRPC] Error: {0}", err));
            };
        }

        public void init()
        {
            if (Client != null)
            {
                MelonLogger.Msg("[DiscordRPC] Initializing connection...");
                Client.ConnectAsync();
            }
        }

        public void endRPC()
        {
            if (Client != null)
            {
                MelonLogger.Msg("[DiscordRPC] Shutting down connection...");
                Client.Dispose();
                Client = null;
            }
        }

        public void SetActivity(DiscordActivity activity)
        {
            if (!IsReady || Client == null)
            {
                MelonLogger.Warning("[DiscordRPC] SetActivity ignored: Discord IPC client is not connected or ready.");
                return;
            }

            if (activity == null)
            {
                MelonLogger.Msg("[DiscordRPC] SetActivity: Clearing Rich Presence.");
            }
            else
            {
                string details = !string.IsNullOrEmpty(activity.Details) ? activity.Details : "(none)";
                string state = !string.IsNullOrEmpty(activity.State) ? activity.State : "(none)";
                string largeImg = !string.IsNullOrEmpty(activity.LargeImageKey) ? activity.LargeImageKey : "(none)";
                string smallImg = !string.IsNullOrEmpty(activity.SmallImageKey) ? activity.SmallImageKey : "(none)";
                MelonLogger.Msg(string.Format("[DiscordRPC] SetActivity enqueued: Details=\"{0}\", State=\"{1}\", LargeImage=\"{2}\", SmallImage=\"{3}\"", details, state, largeImg, smallImg));
            }

            Client.SetActivity(activity);
        }

        public void set()
        {
            if (!IsReady || Client == null)
            {
                MelonLogger.Warning("[DiscordRPC] set() ignored: Discord IPC client is not connected or ready.");
                return;
            }

            var activity = new DiscordActivity
            {
                Details = "testing 123",
                State = "csharp example",
                StartTimestamp = GetCurrentUnixTimestamp(),
                Type = 0 // Playing
            };

            MelonLogger.Msg(string.Format("[DiscordRPC] set() called: Details=\"{0}\", State=\"{1}\"", activity.Details, activity.State));
            Client.SetActivity(activity);
        }

        public void set(string details, string state)
        {
            set(details, state, null, null, null);
        }

        public void set(string details, string state, long? startTimestamp, string largeImageKey, string largeImageText)
        {
            if (!IsReady || Client == null)
            {
                MelonLogger.Warning("[DiscordRPC] set() ignored: Discord IPC client is not connected or ready.");
                return;
            }

            var activity = new DiscordActivity
            {
                Details = details,
                State = state,
                StartTimestamp = startTimestamp.HasValue ? startTimestamp.Value : GetCurrentUnixTimestamp(),
                LargeImageKey = largeImageKey,
                LargeImageText = largeImageText,
                Type = 0
            };

            MelonLogger.Msg(string.Format("[DiscordRPC] set(...) called: Details=\"{0}\", State=\"{1}\", LargeImage=\"{2}\"",
                !string.IsNullOrEmpty(details) ? details : "(none)",
                !string.IsNullOrEmpty(state) ? state : "(none)",
                !string.IsNullOrEmpty(largeImageKey) ? largeImageKey : "(none)"));

            Client.SetActivity(activity);
        }
    }
}
