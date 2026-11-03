using System;
using HarmonyLib;
using MelonLoader;
using Process;
using Manager;

namespace maimai_rpc
{
    [HarmonyPatch(typeof(ProcessManager), "AddProcess", new System.Type[] { typeof(ProcessBase), typeof(byte) })]
    public static class ProcessPatch
    {
        public static void Postfix(ProcessBase process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                GameStateHandler.HandleProcessTransition(process);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[DiscordRPC] Error in process transition patch: " + ex.ToString());
            }
        }
    }
}
