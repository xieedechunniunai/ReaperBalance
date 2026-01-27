using HarmonyLib;
using ReaperBalance.Source;

namespace ReaperBalance.Source.Patches
{
    /// <summary>
    /// 护符/道具相关 Harmony 补丁
    /// 监听护符装备状态变化，刷新 ReaperBalance 组件状态
    /// </summary>
    [HarmonyPatch(typeof(ToolItemManager))]
    internal static class ToolItemPatches
    {
        [HarmonyPatch("SetEquippedCrest")]
        [HarmonyPostfix]
        private static void SetEquippedCrest_Postfix(string crestId)
        {
            Log.Info($"[ToolItemPatches] 护符装备状态已改变: {crestId}");
            var plugin = Plugin.FindPlugin();
            if (plugin != null)
            {
                plugin.RefreshReaperBalance("ToolItemManager.SetEquippedCrest", true);
            }
        }

        [HarmonyPatch("RefreshEquippedState")]
        [HarmonyPostfix]
        private static void RefreshEquippedState_Postfix()
        {
            Log.Info("[ToolItemPatches] 护符装备状态已刷新");
            var plugin = Plugin.FindPlugin();
            if (plugin != null)
            {
                plugin.RefreshReaperBalance("ToolItemManager.RefreshEquippedState", true);
            }
        }

        [HarmonyPatch("SendEquippedChangedEvent")]
        [HarmonyPostfix]
        private static void SendEquippedChangedEvent_Postfix(bool force)
        {
            Log.Info("[ToolItemPatches] 护符装备变更事件已发送");
            var plugin = Plugin.FindPlugin();
            if (plugin != null)
            {
                plugin.RefreshReaperBalance("ToolItemManager.SendEquippedChangedEvent", true);
            }
        }
    }
}
