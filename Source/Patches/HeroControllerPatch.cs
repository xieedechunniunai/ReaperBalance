using HarmonyLib;
using GlobalSettings;
using ReaperBalance.Source;
using UnityEngine;

namespace ReaperBalance.Source.Patches
{
    /// <summary>
    /// Harmony补丁：修改HeroController的BindCompleted方法，实现Reaper模式持续时间翻倍
    /// </summary>
    [HarmonyPatch(typeof(HeroController))]
    [HarmonyPatch("BindCompleted")]
    internal static class HeroControllerBindCompletedPatch
    {
        /// <summary>
        /// 后置补丁：在BindCompleted方法执行后修改Reaper模式持续时间
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(HeroController __instance)
        {
            // 检查是否启用Reaper平衡修改
            if (!Plugin.ShouldApplyPatches() || !Gameplay.ReaperCrest.IsEquipped)
            {
                return;
            }
            // 使用反射获取reaperState字段
            var reaperStateField = typeof(HeroController).GetField("reaperState", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (reaperStateField == null)
            {
                Log.Error("无法找到HeroController的reaperState字段");
                return;
            }

            try
            {
                // 获取reaperState的值
                var reaperState = reaperStateField.GetValue(__instance);
                if (reaperState == null)
                {
                    Log.Error("reaperState为null");
                    return;
                }

                // 使用反射获取ReaperModeDurationLeft字段
                var durationLeftField = reaperState.GetType().GetField("ReaperModeDurationLeft");
                if (durationLeftField == null)
                {
                    Log.Error("无法找到ReaperModeDurationLeft字段");
                    return;
                }

                // 获取原始持续时间
                float originalDuration = (float)durationLeftField.GetValue(reaperState);
                
                // 翻3倍持续时间
                float doubledDuration = originalDuration * Plugin.DurationMultiplier.Value;
                durationLeftField.SetValue(reaperState, doubledDuration);

                // 将修改后的reaperState设置回HeroController
                reaperStateField.SetValue(__instance, reaperState);

                Log.Info($"Reaper模式持续时间翻倍：{originalDuration} -> {doubledDuration}");
            }
            catch (System.Exception e)
            {
                Log.Error($"修改Reaper模式持续时间时出错：{e}");
            }
        }
    }

    /// <summary>
    /// Harmony补丁：修改HeroController的GetReaperPayout方法，实现Reaper bundle倍率
    /// </summary>
    [HarmonyPatch(typeof(HeroController))]
    [HarmonyPatch("GetReaperPayout")]
    internal static class HeroControllerGetReaperPayoutPatch
    {
        /// <summary>
        /// 后置补丁：将 GetReaperPayout 的返回值乘以配置的倍率
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            // 检查是否启用Reaper平衡修改
            if (!Plugin.ShouldApplyPatches() || !Gameplay.ReaperCrest.IsEquipped)
            {
                return;
            }

            try
            {
                int originalResult = __result;
                
                // 应用倍率
                float multipliedResult = originalResult * Plugin.ReaperBundleMultiplier.Value;
                
                // 四舍五入到整数，并确保最小值为 1（避免倍率 < 1 时掉到 0）
                __result = Mathf.Max(1, Mathf.RoundToInt(multipliedResult));
                
                // 仅在倍率不为 1 时记录日志（减少日志噪音）
                if (Plugin.ReaperBundleMultiplier.Value != 1f)
                {
                    Log.Debug($"Reaper bundle payout: {originalResult} -> {__result} (multiplier: {Plugin.ReaperBundleMultiplier.Value})");
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"修改Reaper bundle payout时出错：{e}");
            }
        }
    }
}
