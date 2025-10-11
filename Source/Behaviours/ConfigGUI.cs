using System;
using System.Collections.Generic;
using UnityEngine;
using ReaperBalance.Source;
using ReaperBalance.Source.Behaviours;
namespace ReaperBalance.Source
{
    // Token: 0x02000006 RID: 6
    public class ConfigGUI : MonoBehaviour
    {
        // Token: 0x06000012 RID: 18 RVA: 0x00002E20 File Offset: 0x00001020
        private string GetText(string key)
        {
            string str = this.isEnglish ? "_en" : "_cn";
            return this.texts.ContainsKey(key + str) ? this.texts[key + str] : key;
        }

        // Token: 0x06000013 RID: 19 RVA: 0x00002E70 File Offset: 0x00001070
        private void Update()
        {
            bool keyDown = Input.GetKeyDown(KeyCode.F2);
            if (keyDown)
            {
                this.showGUI = !this.showGUI;
            }
        }

        // Token: 0x06000014 RID: 20 RVA: 0x00002EA4 File Offset: 0x000010A4
        private void OnGUI()
        {
            bool flag = !this.showGUI;
            if (!flag)
            {
                this.windowRect = GUI.Window(0, this.windowRect, new GUI.WindowFunction(this.ConfigWindow), this.GetText("window_title"));
            }
        }

        // Token: 0x06000015 RID: 21 RVA: 0x00002EEC File Offset: 0x000010EC
        private void ConfigWindow(int windowID)
        {
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            bool flag = GUILayout.Button("中文", this.isEnglish ? GUI.skin.button : GUI.skin.box, Array.Empty<GUILayoutOption>());
            if (flag)
            {
                this.isEnglish = false;
            }
            bool flag2 = GUILayout.Button("ENGLISH", this.isEnglish ? GUI.skin.box : GUI.skin.button, Array.Empty<GUILayoutOption>());
            if (flag2)
            {
                this.isEnglish = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);
            // 全局开关部分
            GUILayout.Space(10f);
            GUILayout.Label(this.GetText("global_settings"), Array.Empty<GUILayoutOption>());
            GUILayout.BeginVertical("box", Array.Empty<GUILayoutOption>());

            // 启用/禁用ReaperBalance
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(this.GetText("enable_reaper_balance"), Array.Empty<GUILayoutOption>());
            bool enableReaperBalance = GUILayout.Toggle(Plugin.IsReaperBalanceEnabled, "", Array.Empty<GUILayoutOption>());
            if (enableReaperBalance != Plugin.IsReaperBalanceEnabled)
            {
                Plugin.ToggleReaperBalance(enableReaperBalance);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            // ReaperBalance配置部分
            GUILayout.Space(10f);
            GUILayout.Label(this.GetText("reaper_balance_settings"), Array.Empty<GUILayoutOption>());
            GUILayout.BeginVertical("box", Array.Empty<GUILayoutOption>());

            // 十字斩击缩放大小
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(this.GetText("cross_slash_scale"), Array.Empty<GUILayoutOption>());
            float crossSlashScale = GUILayout.HorizontalSlider(Plugin.CrossSlashScale.Value, 0.5f, 3.0f, Array.Empty<GUILayoutOption>());
            GUILayout.Label(crossSlashScale.ToString("F2"), Array.Empty<GUILayoutOption>());
            GUILayout.EndHorizontal();
            if (crossSlashScale != Plugin.CrossSlashScale.Value)
            {
                Plugin.CrossSlashScale.Value = crossSlashScale;
            }

            // 十字斩伤害倍率
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(this.GetText("cross_slash_damage_multiplier"), Array.Empty<GUILayoutOption>());
            float damageMultiplier = GUILayout.HorizontalSlider(Plugin.DamageMultiplier.Value, 0.1f, 3.0f, Array.Empty<GUILayoutOption>());
            GUILayout.Label(damageMultiplier.ToString("F2"), Array.Empty<GUILayoutOption>());
            GUILayout.EndHorizontal();
            if (damageMultiplier != Plugin.DamageMultiplier.Value)
            {
                Plugin.DamageMultiplier.Value = damageMultiplier;
            }

            // 普通攻击倍率
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(this.GetText("normal_attack_multiplier"), Array.Empty<GUILayoutOption>());
            float normalAttackMultiplier = GUILayout.HorizontalSlider(Plugin.NormalAttackMultiplier.Value, 0.1f, 3.0f, Array.Empty<GUILayoutOption>());
            GUILayout.Label(normalAttackMultiplier.ToString("F2"), Array.Empty<GUILayoutOption>());
            GUILayout.EndHorizontal();
            if (normalAttackMultiplier != Plugin.NormalAttackMultiplier.Value)
            {
                Plugin.NormalAttackMultiplier.Value = normalAttackMultiplier;
            }

            // 下劈攻击倍率
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(this.GetText("down_slash_multiplier"), Array.Empty<GUILayoutOption>());
            float downSlashMultiplier = GUILayout.HorizontalSlider(Plugin.DownSlashMultiplier.Value, 0.1f, 4.0f, Array.Empty<GUILayoutOption>());
            GUILayout.Label(downSlashMultiplier.ToString("F2"), Array.Empty<GUILayoutOption>());
            GUILayout.EndHorizontal();
            if (downSlashMultiplier != Plugin.DownSlashMultiplier.Value)
            {
                Plugin.DownSlashMultiplier.Value = downSlashMultiplier;
            }

            // 灵魂吸收范围
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(this.GetText("collect_range"), Array.Empty<GUILayoutOption>());
            float collectRange = GUILayout.HorizontalSlider(Plugin.CollectRange.Value, 1.0f, 20.0f, Array.Empty<GUILayoutOption>());
            GUILayout.Label(collectRange.ToString("F1"), Array.Empty<GUILayoutOption>());
            GUILayout.EndHorizontal();
            if (collectRange != Plugin.CollectRange.Value)
            {
                Plugin.CollectRange.Value = collectRange;
            }

            // 持续时间倍率
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(this.GetText("duration_multiplier"), Array.Empty<GUILayoutOption>());
            float durationMultiplier = GUILayout.HorizontalSlider(Plugin.DurationMultiplier.Value, 1.0f, 10.0f, Array.Empty<GUILayoutOption>());
            GUILayout.Label(durationMultiplier.ToString("F1"), Array.Empty<GUILayoutOption>());
            GUILayout.EndHorizontal();
            if (durationMultiplier != Plugin.DurationMultiplier.Value)
            {
                Plugin.DurationMultiplier.Value = durationMultiplier;
            }

            // 应用修改按钮
            bool applyReaperChanges = GUILayout.Button(this.GetText("apply_reaper_changes"), Array.Empty<GUILayoutOption>());
            if (applyReaperChanges)
            {
                ApplyReaperBalanceChanges();
            }

            GUILayout.EndVertical();

            // 底部按钮区域
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            bool flag17 = GUILayout.Button(this.GetText("reset_defaults"), Array.Empty<GUILayoutOption>());
            if (flag17)
            {
                this.ResetToDefaults();
            }
            bool flag18 = GUILayout.Button(this.GetText("close_panel"), Array.Empty<GUILayoutOption>());
            if (flag18)
            {
                this.showGUI = false;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        /// <summary>
        /// 应用ReaperBalance修改
        /// </summary>
        private void ApplyReaperBalanceChanges()
        {
            try
            {
                // 查找ReaperBalanceManager并更新ChangeReaper组件
                var reaperBalanceManager = GameObject.Find("ReaperBalanceManager");
                if (reaperBalanceManager != null)
                {
                    var changeReaper = reaperBalanceManager.GetComponent<ChangeReaper>();
                    if (changeReaper != null)
                    {
                        changeReaper.ForceUpdateConfig();
                        Log.Info("ReaperBalance配置已应用并重新初始化");
                    }
                    else
                    {
                        // 如果组件不存在，尝试添加
                        reaperBalanceManager.AddComponent<ChangeReaper>();
                        Log.Info("ReaperBalance组件已添加并应用配置");
                    }
                }
                else
                {
                    Log.Error("未找到ReaperBalanceManager，无法应用配置");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"应用ReaperBalance修改时发生错误: {ex.Message}");
            }
        }

        // Token: 0x06000017 RID: 23 RVA: 0x00003F1C File Offset: 0x0000211C
        private void ResetToDefaults()
        {
            try
            {
                // 重置ReaperBalance配置为默认值
                Plugin.CrossSlashScale.Value = 1.2f;
                Plugin.DamageMultiplier.Value = 1.0f;
                Plugin.NormalAttackMultiplier.Value = 1.2f;
                Plugin.DownSlashMultiplier.Value = 1.5f;
                Plugin.CollectRange.Value = 8f;
                Plugin.DurationMultiplier.Value = 3f;

                // 保存配置
                Plugin.Instance.Config.Save();

                // 应用修改
                ApplyReaperBalanceChanges();

                Log.Info("ReaperBalance配置已重置为默认值");
            }
            catch (Exception ex)
            {
                Log.Error($"重置默认配置时发生错误: {ex.Message}");
            }
        }
        private bool showGUI = false;

        // Token: 0x04000039 RID: 57
        private Rect windowRect = new Rect(20f, 20f, 400f, 600f);

        // Token: 0x0400003A RID: 58
        private Vector2 scrollPosition = Vector2.zero;

        // Token: 0x0400003B RID: 59
        private bool isEnglish = false;

        // Token: 0x0400003C RID: 60
        private Dictionary<string, string> texts = new Dictionary<string, string>
        {
             // 全局设置文本
            {
                "global_settings_cn",
                "=== 全局设置 ==="
            },
            {
                "global_settings_en",
                "=== Global Settings ==="
            },
            {
                "enable_reaper_balance_cn",
                "启用Reaper平衡修改:"
            },
            {
                "enable_reaper_balance_en",
                "Enable Reaper Balance:"
            },
            // 窗口标题
            {
                "window_title_cn",
                "ReaperBalance 配置面板"
            },
            {
                "window_title_en",
                "ReaperBalance Config Panel"
            },
            // 重置和关闭按钮
            {
                "reset_defaults_cn",
                "重置默认值"
            },
            {
                "reset_defaults_en",
                "Reset to Defaults"
            },
            {
                "close_panel_cn",
                "关闭面板"
            },
            {
                "close_panel_en",
                "Close Panel"
            },
            // ReaperBalance配置文本
            {
                "reaper_balance_settings_cn",
                "=== Reaper平衡配置 ==="
            },
            {
                "reaper_balance_settings_en",
                "=== Reaper Balance Settings ==="
            },
            {
                "cross_slash_scale_cn",
                "十字斩击缩放大小:"
            },
            {
                "cross_slash_scale_en",
                "Cross Slash Scale:"
            },
            {
                "cross_slash_damage_multiplier_cn",
                "十字斩伤害倍率:"
            },
            {
                "cross_slash_damage_multiplier_en",
                "Cross Slash Damage Multiplier:"
            },
            {
                "normal_attack_multiplier_cn",
                "普通攻击倍率:"
            },
            {
                "normal_attack_multiplier_en",
                "Normal Attack Multiplier:"
            },
            {
                "down_slash_multiplier_cn",
                "下劈攻击倍率:"
            },
            {
                "down_slash_multiplier_en",
                "Down Slash Multiplier:"
            },
            {
                "collect_range_cn",
                "灵魂吸收范围:"
            },
            {
                "collect_range_en",
                "Soul Collect Range:"
            },
            {
                "duration_multiplier_cn",
                "持续时间倍率:"
            },
            {
                "duration_multiplier_en",
                "Duration Multiplier:"
            },
            {
                "apply_reaper_changes_cn",
                "应用Reaper修改"
            },
            {
                "apply_reaper_changes_en",
                "Apply Reaper Changes"
            }
        };
    }
}