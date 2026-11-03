using System;
using DiscordRPC.Logging;
using MelonLoader;

namespace maimai_rpc
{
    public class RPCLogger : ILogger
    {
        public LogLevel Level { get; set; } = LogLevel.Info;

        private void SafeLog(Action logAction, string fallbackPrefix, string message, object[] args)
        {
            try
            {
                logAction();
            }
            catch
            {
                try
                {
                    string formatted = args != null && args.Length > 0 ? string.Format(message, args) : message;
                    System.Console.WriteLine(string.Format("[{0}] {1}", fallbackPrefix, formatted));
                }
                catch { }
            }
        }

        public void Trace(string message, params object[] args)
        {
            if (Level <= LogLevel.Trace)
                SafeLog(() => MelonLogger.Msg(string.Format(message, args)), "Discord-Trace", message, args);
        }

        public void Info(string message, params object[] args)
        {
            if (Level <= LogLevel.Info)
                SafeLog(() => MelonLogger.Msg(string.Format(message, args)), "Discord-Info", message, args);
        }

        public void Warning(string message, params object[] args)
        {
            if (Level <= LogLevel.Warning)
                SafeLog(() => MelonLogger.Warning(string.Format(message, args)), "Discord-Warn", message, args);
        }

        public void Error(string message, params object[] args)
        {
            if (Level <= LogLevel.Error)
                SafeLog(() => MelonLogger.Error(string.Format(message, args)), "Discord-Error", message, args);
        }
    }
}
