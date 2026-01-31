using HarmonyLib;
using GlobalSettings;
using ReaperBalance.Source;
using UnityEngine;

namespace ReaperBalance.Source.Patches
{
    /// <summary>
    /// Harmony补丁：修改HealthManager的ApplyDamageScaling方法，实现Reaper独享暴击系统
    /// 当启用时，完全覆盖原版Wanderer暴击判定，暴击率和暴击倍率完全由配置决定
    /// </summary>
    [HarmonyPatch(typeof(HealthManager))]
    [HarmonyPatch("ApplyDamageScaling")]
    internal static class HealthManagerCritPatch
    {
        /// <summary>
        /// 前置补丁：在ApplyDamageScaling执行前，处理Reaper暴击逻辑
        /// </summary>
        /// <param name="hitInstance">命中实例（ref参数，可以修改）</param>
        [HarmonyPrefix]
        private static void Prefix(ref HitInstance hitInstance)
        {
            // 检查是否启用Reaper平衡修改
            if (!Plugin.ShouldApplyPatches())
            {
                return;
            }

            // 检查是否启用Reaper暴击系统
            if (!Plugin.EnableReaperCrit.Value)
            {
                return;
            }

            // 检查是否装备了Reaper纹章
            if (!Gameplay.ReaperCrest.IsEquipped)
            {
                return;
            }

            // 检查是否是英雄的nail类攻击
            // IsNailDamage 检查 IsNailTag || AttackType == AttackTypes.Nail
            // 我们也接受 Heavy 类型（十字斩）因为我们的十字斩设置了 "Nail Attack" tag
            if (!hitInstance.IsHeroDamage || !hitInstance.IsNailDamage)
            {
                return;
            }

            try
            {
                // 清除原版可能已经设置的暴击标记
                bool originalCrit = hitInstance.CriticalHit;
                hitInstance.CriticalHit = false;

                // 使用我们自己的暴击概率 roll
                float critChance = Plugin.ReaperCritChancePercent.Value / 100f;
                bool isCrit = Random.value < critChance;

                if (isCrit)
                {
                    // 设置暴击标记（让游戏显示暴击特效）
                    hitInstance.CriticalHit = true;

                    // 直接乘以我们配置的暴击倍率
                    // 注意：由于我们设置了 CriticalHit = true，原版的 TakeDamage 中会再乘以
                    // WandererCritMultiplier。为了让最终倍率完全由我们控制，我们需要
                    // 预先除以 WandererCritMultiplier，然后乘以我们的倍率
                    // 最终效果：damage * (ourMultiplier / wandererMult) * wandererMult = damage * ourMultiplier
                    
                    float wandererCritMult = Gameplay.WandererCritMultiplier;
                    if (wandererCritMult > 0)
                    {
                        // 计算预调整因子
                        float adjustmentFactor = Plugin.ReaperCritDamageMultiplier.Value / wandererCritMult;
                        hitInstance.DamageDealt = Mathf.RoundToInt(hitInstance.DamageDealt * adjustmentFactor);
                    }

                    Log.Info($"Reaper暴击触发！原始伤害调整为 {hitInstance.DamageDealt}，最终倍率将为 {Plugin.ReaperCritDamageMultiplier.Value}x");
                }
                else if (originalCrit)
                {
                    // 原版标记了暴击但我们的 roll 没中，取消暴击
                    Log.Debug("原版暴击被Reaper暴击系统覆盖：未触发");
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"Reaper暴击处理出错：{e}");
            }
        }
    }
}
