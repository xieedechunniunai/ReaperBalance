using System;
using BepInEx.Configuration;
using UnityEngine;

namespace ReaperBalance.Source;

/// <summary>
/// IMGUI-based in-game config panel (F6 to toggle).
/// No ModMenu dependency required.
/// </summary>
internal sealed class ConfigUI : MonoBehaviour
{
    private bool _showGui;
    private Vector2 _scrollPosition;
    private Rect _windowRect = new(20f, 20f, 540f, 580f);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6))
        {
            _showGui = !_showGui;
        }
    }

    private void OnGUI()
    {
        if (!_showGui) return;
        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow,
            Tr("ReaperBalance 配置面板", "ReaperBalance Config Panel"));
    }

    private void DrawWindow(int windowId)
    {
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(480f));

        DrawLanguageSwitch();
        GUILayout.Space(8f);

        // ── 基本开关 ──────────────────────────────────────────
        DrawToggle(
            Tr("启用收割者平衡", "Enable Reaper Balance"),
            Tr("启用或禁用收割者平衡修改。", "Enable or disable Reaper balance changes."),
            Plugin.EnableReaperBalance,
            onChanged: enabled => Plugin.ToggleReaperBalance(enabled));

        DrawToggle(
            Tr("启用十字斩", "Enable Cross Slash"),
            Tr("启用或禁用十字斩蓄力攻击。", "Enable or disable the Cross Slash heavy attack."),
            Plugin.EnableCrossSlash,
            onChanged: _ => RefreshPlugin("EnableCrossSlash"));

        GUILayout.Space(6f);

        // ── 攻击倍率 ──────────────────────────────────────────
        DrawFloatSlider(Tr("普通攻击倍率", "Normal Attack Multiplier"),   Plugin.NormalAttackMultiplier,  0.1f,    3.0f,    0.1f);
        DrawFloatSlider(Tr("下劈攻击倍率", "Down Slash Multiplier"),      Plugin.DownSlashMultiplier,     0.1f,    4.0f,    0.1f);
        DrawFloatSlider(Tr("眩晕值倍率",   "Stun Damage Multiplier"),     Plugin.StunDamageMultiplier,    0f,      5.0f,    0.1f);
        DrawFloatSlider(Tr("十字斩缩放大小", "Cross Slash Scale"),         Plugin.CrossSlashScale,         0.5f,    3.0f,    0.1f);
        DrawFloatSlider(Tr("十字斩伤害倍率", "Cross Slash Damage"),        Plugin.CrossSlashDamage,        0.5f,    8.0f,    0.1f);

        GUILayout.Space(6f);

        // ── 收割者模式 ─────────────────────────────────────────
        DrawFloatSlider(Tr("持续时间倍率",   "Duration Multiplier"),       Plugin.DurationMultiplier,      0.2f,   10.0f,   0.2f);
        DrawFloatSlider(Tr("丝球掉落倍率",   "Silk Orb Drop Multiplier"),  Plugin.ReaperBundleMultiplier,  0f,      5.0f,   0.5f);

        GUILayout.Space(6f);

        // ── 丝球吸引 ──────────────────────────────────────────
        DrawToggle(
            Tr("吸引小丝球", "Enable Silk Attraction"),
            Tr("收割模式下远距离吸引小丝球。", "Attract silk orbs from a distance in Reaper mode."),
            Plugin.EnableSilkAttraction,
            onChanged: _ => RefreshPlugin("EnableSilkAttraction"));

        DrawFloatSlider(Tr("吸引范围",     "Collect Range"),         Plugin.CollectRange,        1.0f,   24.0f,    0.5f);
        DrawFloatSlider(Tr("吸引最大速度", "Collect Max Speed"),     Plugin.CollectMaxSpeed,     0.0f,   60.0f,    1.0f);
        DrawFloatSlider(Tr("吸引加速度",   "Collect Acceleration"),  Plugin.CollectAcceleration, 0.0f, 3000.0f,   20.0f);

        GUILayout.Space(6f);

        // ── 收割者暴击 ─────────────────────────────────────────
        DrawToggle(
            Tr("启用收割者暴击", "Enable Reaper Crit"),
            Tr("启用收割者独享暴击系统（仅收割者纹章生效，完全覆盖原版暴击判定）",
               "Enable Reaper-exclusive crit system (only works with Reaper Crest, overrides vanilla crit)"),
            Plugin.EnableReaperCrit);

        DrawFloatSlider(Tr("暴击率 %",      "Crit Chance %"),          Plugin.ReaperCritChancePercent,     0f,   100f, 1f);
        DrawFloatSlider(Tr("暴击伤害倍率",  "Crit Damage Multiplier"), Plugin.ReaperCritDamageMultiplier,  1f,     5f, 0.1f);

        GUILayout.EndScrollView();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Tr("应用并保存", "Apply & Save")))   ApplyAndSave();
        if (GUILayout.Button(Tr("重置为默认值", "Reset Defaults"))) ResetToDefaults();
        if (GUILayout.Button(Tr("关闭面板", "Close Panel")))       _showGui = false;
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        var prevColor = GUI.color;
        GUI.color = new Color(0.55f, 0.55f, 0.55f);
        GUILayout.Label(Tr("按 F6 打开/关闭配置面板", "Press F6 to open/close config panel"));
        GUI.color = prevColor;

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private void DrawLanguageSwitch()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(Tr("语言", "Language"), GUILayout.Width(120f));
        bool isChinese = Plugin.UseChinese.Value;
        if (GUILayout.Button("中文", isChinese ? GUI.skin.box : GUI.skin.button))
            Plugin.UseChinese.Value = true;
        if (GUILayout.Button("ENGLISH", isChinese ? GUI.skin.button : GUI.skin.box))
            Plugin.UseChinese.Value = false;
        GUILayout.EndHorizontal();
    }

    private static void DrawToggle(string label, string description, ConfigEntry<bool> entry, Action<bool>? onChanged = null)
    {
        bool next = GUILayout.Toggle(entry.Value, label);
        if (next != entry.Value)
        {
            entry.Value = next;
            onChanged?.Invoke(next);
        }
        if (!string.IsNullOrEmpty(description))
        {
            var prevColor = GUI.color;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label("  " + description);
            GUI.color = prevColor;
        }
    }

    private static void DrawFloatSlider(string label, ConfigEntry<float> entry, float min, float max, float step)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(190f));
        float raw = GUILayout.HorizontalSlider(entry.Value, min, max);
        float snapped = Mathf.Clamp((float)Math.Round(Mathf.Round(raw / step) * step, 3), min, max);
        GUILayout.Label(snapped.ToString("0.###"), GUILayout.Width(50f));
        GUILayout.EndHorizontal();

        if (Math.Abs(snapped - entry.Value) > 1e-6f)
        {
            entry.Value = snapped;
            RefreshPlugin(label);
        }
    }

    private static void RefreshPlugin(string reason)
    {
        Plugin.FindPlugin()?.RefreshReaperBalance(reason, true);
    }

    private static void ApplyAndSave()
    {
        try
        {
            Plugin.FindPlugin()?.Config.Save();
            Log.Info("[ConfigUI] Applied and saved configuration.");
        }
        catch (Exception ex)
        {
            Log.Error($"[ConfigUI] Failed to apply config: {ex.Message}");
        }
    }

    private static void ResetToDefaults()
    {
        try
        {
            Plugin.EnableReaperBalance.Value        = (bool)Plugin.EnableReaperBalance.DefaultValue;
            Plugin.UseChinese.Value                 = (bool)Plugin.UseChinese.DefaultValue;
            Plugin.EnableCrossSlash.Value           = (bool)Plugin.EnableCrossSlash.DefaultValue;
            Plugin.EnableSilkAttraction.Value       = (bool)Plugin.EnableSilkAttraction.DefaultValue;
            Plugin.CrossSlashScale.Value            = (float)Plugin.CrossSlashScale.DefaultValue;
            Plugin.CrossSlashDamage.Value           = (float)Plugin.CrossSlashDamage.DefaultValue;
            Plugin.NormalAttackMultiplier.Value     = (float)Plugin.NormalAttackMultiplier.DefaultValue;
            Plugin.DownSlashMultiplier.Value        = (float)Plugin.DownSlashMultiplier.DefaultValue;
            Plugin.CollectRange.Value               = (float)Plugin.CollectRange.DefaultValue;
            Plugin.CollectMaxSpeed.Value            = (float)Plugin.CollectMaxSpeed.DefaultValue;
            Plugin.CollectAcceleration.Value        = (float)Plugin.CollectAcceleration.DefaultValue;
            Plugin.DurationMultiplier.Value         = (float)Plugin.DurationMultiplier.DefaultValue;
            Plugin.StunDamageMultiplier.Value       = (float)Plugin.StunDamageMultiplier.DefaultValue;
            Plugin.ReaperBundleMultiplier.Value     = (float)Plugin.ReaperBundleMultiplier.DefaultValue;
            Plugin.EnableReaperCrit.Value           = (bool)Plugin.EnableReaperCrit.DefaultValue;
            Plugin.ReaperCritChancePercent.Value    = (float)Plugin.ReaperCritChancePercent.DefaultValue;
            Plugin.ReaperCritDamageMultiplier.Value = (float)Plugin.ReaperCritDamageMultiplier.DefaultValue;

            Plugin.IsReaperBalanceEnabled = Plugin.EnableReaperBalance.Value;

            var plugin = Plugin.FindPlugin();
            if (plugin != null)
            {
                plugin.Config.Save();
                plugin.RefreshReaperBalance("ResetDefaults", true);
            }

            Log.Info("[ConfigUI] Reset configuration to defaults.");
        }
        catch (Exception ex)
        {
            Log.Error($"[ConfigUI] Failed to reset config: {ex.Message}");
        }
    }

    private static string Tr(string zh, string en) => (Plugin.UseChinese?.Value ?? true) ? zh : en;
}
