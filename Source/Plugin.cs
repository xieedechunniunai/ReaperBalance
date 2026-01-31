using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using ReaperBalance.Source.Behaviours;
using ReaperBalance.Source.ModMenu;
using GlobalSettings;
using BepInEx.Configuration;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace ReaperBalance.Source;

/// <summary>
/// The main plugin class.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("org.silksong-modding.modmenu")]
public class Plugin : BaseUnityPlugin, IModMenuCustomMenu
{
    private static Harmony _harmony = null!;
    private GameObject ReaperBalanceManager = null!;
    // 全局开关 - 控制是否启用Reaper平衡修改
    public static bool IsReaperBalanceEnabled { get; set; } = true;
    private ChangeReaper? _changeReaperComponent = null;
    // ReaperBalance配置变量
    public static ConfigEntry<bool> EnableReaperBalance { get; private set; } = null!;
    public static ConfigEntry<bool> UseChinese { get; private set; } = null!;
    public static ConfigEntry<bool> EnableCrossSlash { get; private set; } = null!;
    public static ConfigEntry<bool> EnableSilkAttraction { get; private set; } = null!;
    public static ConfigEntry<float> CrossSlashScale { get; private set; } = null!;
    public static ConfigEntry<float> CrossSlashDamage { get; private set; } = null!;
    public static ConfigEntry<float> NormalAttackMultiplier { get; private set; } = null!;
    public static ConfigEntry<float> DownSlashMultiplier { get; private set; } = null!;
    public static ConfigEntry<float> CollectRange { get; private set; } = null!;
    public static ConfigEntry<float> CollectMaxSpeed { get; private set; } = null!;
    public static ConfigEntry<float> CollectAcceleration { get; private set; } = null!;
    public static ConfigEntry<float> DurationMultiplier { get; private set; } = null!;
    public static ConfigEntry<float> StunDamageMultiplier { get; private set; } = null!;
    public static ConfigEntry<float> ReaperBundleMultiplier { get; private set; } = null!;

    // Reaper 暴击配置
    public static ConfigEntry<bool> EnableReaperCrit { get; private set; } = null!;
    public static ConfigEntry<float> ReaperCritChancePercent { get; private set; } = null!;
    public static ConfigEntry<float> ReaperCritDamageMultiplier { get; private set; } = null!;
    private void Awake()
    {
        Log.Init(Logger);
        SceneManager.activeSceneChanged += OnSceneChange;
        // 初始化配置
        InitializeConfig();
        // 从配置加载启用状态
        IsReaperBalanceEnabled = EnableReaperBalance.Value;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        // 注册所有 Harmony 补丁（扫描整个程序集）
        _harmony.PatchAll();
        Log.Info($"Harmony补丁已应用，已注册 {_harmony.GetPatchedMethods().Count()} 个方法");
        StartCoroutine(Initialize());
    }

    private IEnumerator Initialize()
    {
        while (GameManager.instance == null)
        {
            yield return new WaitForSeconds(1f);
        }
        CreateManager();
    }

    private void OnSceneChange(Scene oldScene, Scene newScene)
    {
        // Returning to main menu: cleanup and release references/state.
        if (newScene.name == "Menu_Title")
        {
            CleanupOnMenuReturn();
            return;
        }

        // Entering game from main menu (or menu intro): ensure manager/resources are ready.
        if (oldScene.name == "Menu_Title")
        {
            EnsureAssetManagerInitialized();
            RefreshReaperBalance("EnterGame", true);
            return;
        }

        // Normal scene change inside gameplay.
        if (newScene.name != "Pre_Menu_Intro" || newScene.name != "Menu_Title")
        {
            EnsureAssetManagerInitialized();
            RefreshReaperBalance("SceneChange", true);
        }
    }

    /// <summary>
    /// Cleanup when returning to main menu.
    /// </summary>
    private void CleanupOnMenuReturn()
    {
        // Reset ChangeReaper component
        if (_changeReaperComponent != null)
        {
            _changeReaperComponent.ResetModifications();
            Destroy(_changeReaperComponent);
            _changeReaperComponent = null;
        }

        // Force AssetPool cleanup so next save-load re-initializes cleanly.
        if (ReaperBalanceManager != null)
        {
            var assetManager = ReaperBalanceManager.GetComponent<AssetManager>();
            if (assetManager != null)
            {
                assetManager.CleanupAssetPool();
            }
        }

        Log.Info("Menu return cleanup completed");
    }

    private void EnsureAssetManagerInitialized()
    {
        if (ReaperBalanceManager == null) return;

        var assetManager = ReaperBalanceManager.GetComponent<AssetManager>();
        if (assetManager == null)
        {
            // Should not happen (CreateManager adds it on first creation), but keep it safe.
            assetManager = ReaperBalanceManager.AddComponent<AssetManager>();
        }

        if (!assetManager.IsInitialized())
        {
            StartCoroutine(assetManager.Initialize());
        }
    }

    private void OnDestroy()
    {
        _harmony.UnpatchSelf();
    }

    public string ModMenuName() => MyPluginInfo.PLUGIN_NAME;

    private static string Tr(string en, string zh) => (UseChinese?.Value ?? true) ? zh : en;

    public AbstractMenuScreen BuildCustomMenu()
    {
        // Create global toggle (will be placed in ControlsPane)
        var enabledChoice = new ChoiceElement<bool>(
            Tr("Enable Reaper Balance", "启用收割者平衡"),
            ChoiceModels.ForBool(Tr("Disabled", "禁用"), Tr("Enabled", "启用")),
            Tr("Enable or disable Reaper balance changes.", "启用或禁用收割者平衡修改。")
        )
        {
            Value = IsReaperBalanceEnabled
        };
        enabledChoice.OnValueChanged += enabled => ToggleReaperBalance(enabled);

        // Create reset button (will be placed in ControlsPane)
        var resetButton = new TextButton(Tr("Reset to Defaults", "重置为默认值"));
        resetButton.OnSubmit += () =>
        {
            // Reset all configs back to their defaults.
            EnableReaperBalance.Value = (bool)EnableReaperBalance.DefaultValue;
            UseChinese.Value = (bool)UseChinese.DefaultValue;
            EnableCrossSlash.Value = (bool)EnableCrossSlash.DefaultValue;
            EnableSilkAttraction.Value = (bool)EnableSilkAttraction.DefaultValue;
            CrossSlashScale.Value = (float)CrossSlashScale.DefaultValue;
            CrossSlashDamage.Value = (float)CrossSlashDamage.DefaultValue;
            NormalAttackMultiplier.Value = (float)NormalAttackMultiplier.DefaultValue;
            DownSlashMultiplier.Value = (float)DownSlashMultiplier.DefaultValue;
            CollectRange.Value = (float)CollectRange.DefaultValue;
            CollectMaxSpeed.Value = (float)CollectMaxSpeed.DefaultValue;
            CollectAcceleration.Value = (float)CollectAcceleration.DefaultValue;
            DurationMultiplier.Value = (float)DurationMultiplier.DefaultValue;
            StunDamageMultiplier.Value = (float)StunDamageMultiplier.DefaultValue;
            ReaperBundleMultiplier.Value = (float)ReaperBundleMultiplier.DefaultValue;
            EnableReaperCrit.Value = (bool)EnableReaperCrit.DefaultValue;
            ReaperCritChancePercent.Value = (float)ReaperCritChancePercent.DefaultValue;
            ReaperCritDamageMultiplier.Value = (float)ReaperCritDamageMultiplier.DefaultValue;

            IsReaperBalanceEnabled = EnableReaperBalance.Value;

            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.Config.Save();
                plugin.RefreshReaperBalance("ResetDefaults", true);
                MenuScreenNavigation.Show(plugin.BuildCustomMenu(), HistoryMode.Replace);
            }
        };

        // Create the paginated menu screen with only reset button in ControlsPane
        // Global toggle is now in content pages for better layout
        var menu = new ReaperBalancePaginatedMenuScreen(
            MyPluginInfo.PLUGIN_NAME,
            resetButton
        );

        // Page 1: Global toggle, Language, 通用攻击设置, 十字斩设置
        var page1 = new VerticalGroup();

        // Global toggle at the top of content area (moved from ControlsPane)
        page1.Add(enabledChoice);

        var languageChoice = new ChoiceElement<bool>(
            Tr("Language", "语言"),
            ChoiceModels.ForBool("English", "中文"),
            Tr("Switch the menu language.", "切换菜单语言。")
        )
        {
            Value = UseChinese.Value
        };
        languageChoice.OnValueChanged += isChinese =>
        {
            UseChinese.Value = isChinese;
            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.Config.Save();
                MenuScreenNavigation.Show(plugin.BuildCustomMenu(), HistoryMode.Replace);
            }
        };
        page1.Add(languageChoice);

        // 通用攻击倍率 - 使用 ChoiceElement + Slider 保持对齐且支持拖动
        var normalAttackChoice = CreateFloatChoiceWithSlider(
            Tr("Normal Attack Multiplier", "普通攻击倍率"),
            Tr("Damage multiplier for normal attacks", "普通攻击的伤害倍率"),
            NormalAttackMultiplier,
            0.1f,
            3.0f,
            0.1f,
            "ModMenu.NormalAttackMultiplier"
        );
        page1.Add(normalAttackChoice);
        page1.Add(CreateFloatChoiceWithSlider(
            Tr("Down Slash Multiplier", "下劈攻击倍率"),
            Tr("Damage multiplier for down slash attacks", "下劈攻击的伤害倍率"),
            DownSlashMultiplier,
            0.1f,
            4.0f,
            0.1f,
            "ModMenu.DownSlashMultiplier"
        ));
        page1.Add(CreateFloatChoiceWithSlider(
            Tr("Stun Damage Multiplier", "眩晕值倍率"),
            Tr("Stun damage multiplier (Normal/Down/Cross)", "影响普攻/下劈/十字斩的眩晕值"),
            StunDamageMultiplier,
            0f,
            5.0f,
            0.1f,
            "ModMenu.StunDamageMultiplier"
        ));

        // 十字斩设置
        var enableCrossSlashChoice = new ChoiceElement<bool>(
            Tr("Enable Cross Slash", "启用十字斩"),
            ChoiceModels.ForBool(Tr("Disabled", "禁用"), Tr("Enabled", "启用")),
            Tr("Enable or disable the Cross Slash heavy attack.", "启用或禁用十字斩蓄力攻击。")
        )
        {
            Value = EnableCrossSlash.Value
        };
        enableCrossSlashChoice.OnValueChanged += enabled =>
        {
            EnableCrossSlash.Value = enabled;
            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.Config.Save();
                plugin.RefreshReaperBalance("EnableCrossSlash", true);
            }
        };
        page1.Add(enableCrossSlashChoice);
        page1.Add(CreateFloatSlider(
            Tr("Cross Slash Scale", "十字斩缩放大小"),
            CrossSlashScale,
            0.5f,
            3.0f,
            0.1f,
            "ModMenu.CrossSlashScale"
        ));
        page1.Add(CreateFloatSlider(
            Tr("Cross Slash Damage Multiplier", "十字斩伤害倍率"),
            CrossSlashDamage,
            0.5f,
            8.0f,
            0.1f,
            "ModMenu.CrossSlashDamage"
        ));

        menu.AddPage(page1);

        // Page 2: 持续时间, 丝球吸引设置
        var page2 = new VerticalGroup();

        // 持续时间倍率 - 使用 ChoiceElement + Slider 保持对齐且支持拖动
        page2.Add(CreateFloatChoiceWithSlider(
            Tr("Duration Multiplier", "持续时间倍率"),
            Tr("Reaper mode duration multiplier", "收割者模式持续时间倍率"),
            DurationMultiplier,
            0.2f,
            10.0f,
            0.2f,
            "ModMenu.DurationMultiplier"
        ));
        page2.Add(CreateFloatChoiceWithSlider(
            Tr("Silk Orb Drop Multiplier", "丝球掉落倍率"),
            Tr("Silk orb drop multiplier in Reaper mode", "收割模式攻击敌人时掉落丝球的倍率"),
            ReaperBundleMultiplier,
            0f,
            5.0f,
            0.5f,
            "ModMenu.ReaperBundleMultiplier"
        ));

        // 丝球吸引设置
        var enableSilkAttractionChoice = new ChoiceElement<bool>(
            Tr("Enable Silk Attraction", "吸引小丝球"),
            ChoiceModels.ForBool(Tr("Disabled", "禁用"), Tr("Enabled", "启用")),
            Tr("Attract silk orbs from a distance in Reaper mode.", "收割模式下远距离吸引小丝球。")
        )
        {
            Value = EnableSilkAttraction.Value
        };
        enableSilkAttractionChoice.OnValueChanged += enabled =>
        {
            EnableSilkAttraction.Value = enabled;
            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.Config.Save();
                plugin.RefreshReaperBalance("EnableSilkAttraction", true);
            }
        };
        page2.Add(enableSilkAttractionChoice);

        page2.Add(CreateFloatSlider(
            Tr("Collect Range", "吸引范围"),
            CollectRange,
            1.0f,
            24.0f,
            0.5f,
            "ModMenu.CollectRange"
        ));
        page2.Add(CreateFloatSlider(
            Tr("Collect Max Speed", "吸引最大速度"),
            CollectMaxSpeed,
            0.0f,
            60.0f,
            1.0f,
            "ModMenu.CollectMaxSpeed"
        ));
        page2.Add(CreateFloatSlider(
            Tr("Collect Acceleration", "吸引加速度"),
            CollectAcceleration,
            0.0f,
            3000.0f,
            20.0f,
            "ModMenu.CollectAcceleration"
        ));

        menu.AddPage(page2);

        // Page 3: Reaper 暴击设置
        var page3 = new VerticalGroup();

        var enableCritChoice = new ChoiceElement<bool>(
            Tr("Enable Reaper Crit", "启用收割者暴击"),
            ChoiceModels.ForBool(Tr("Disabled", "禁用"), Tr("Enabled", "启用")),
            Tr("Enable Reaper-exclusive crit system (only works with Reaper Crest, overrides vanilla crit)",
               "启用收割者独享暴击系统（仅收割者纹章生效，完全覆盖原版暴击判定）")
        )
        {
            Value = EnableReaperCrit.Value
        };
        enableCritChoice.OnValueChanged += enabled =>
        {
            EnableReaperCrit.Value = enabled;
            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.Config.Save();
            }
        };
        page3.Add(enableCritChoice);

        page3.Add(CreateFloatSlider(
            Tr("Crit Chance %", "暴击率 %"),
            ReaperCritChancePercent,
            0f,
            100f,
            1f,
            "ModMenu.ReaperCritChancePercent"
        ));
        page3.Add(CreateFloatSlider(
            Tr("Crit Damage Multiplier", "暴击伤害倍率"),
            ReaperCritDamageMultiplier,
            1f,
            5f,
            0.1f,
            "ModMenu.ReaperCritDamageMultiplier"
        ));

        menu.AddPage(page3);

        return menu;
    }

    private static SliderElement<float> CreateFloatSlider(
        string label,
        ConfigEntry<float> entry,
        float min,
        float max,
        float step,
        string refreshReason
    )
    {
        int ticks = Mathf.RoundToInt((max - min) / step) + 1;
        if (ticks < 2)
        {
            ticks = 2;
        }

        var model = new LinearFloatSliderModel(min, max, ticks);
        model.DisplayFn = (_, value) => value.ToString("0.###");

        float clamped = Mathf.Clamp(entry.Value, Mathf.Min(min, max), Mathf.Max(min, max));
        model.SetValue(clamped);

        // Workaround for ModMenu SliderElement initializing the unity slider at MinimumIndex
        // without syncing it to the model's current index unless a value-changed event fires.
        int desiredIndex = model.Index;
        var element = new SliderElement<float>(label, model);
        if (desiredIndex != model.MinimumIndex)
        {
            model.Index = model.MinimumIndex;
            model.Index = desiredIndex;
        }

        model.OnValueChanged += value =>
        {
            entry.Value = value;
            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.RefreshReaperBalance(refreshReason, true);
            }
        };

        return element;
    }

    /// <summary>
    /// 创建浮点数选项的 ChoiceElement（用于顶级设置项，与 ChoiceElement 对齐）
    /// </summary>
    private static ChoiceElement<float> CreateFloatChoice(
        string label,
        string description,
        ConfigEntry<float> entry,
        float min,
        float max,
        float step,
        string refreshReason
    )
    {
        // 生成离散值列表
        var values = new System.Collections.Generic.List<float>();
        for (float v = min; v <= max + step * 0.5f; v += step)
        {
            values.Add((float)System.Math.Round(v, 3));
        }

        var model = new ListChoiceModel<float>(values)
        {
            Circular = false,
            DisplayFn = (_, v) => v.ToString("0.###")
        };

        // 设置初始值（找到最接近的）
        float clamped = Mathf.Clamp(entry.Value, min, max);
        int closestIndex = 0;
        float minDiff = float.MaxValue;
        for (int i = 0; i < values.Count; i++)
        {
            float diff = Mathf.Abs(values[i] - clamped);
            if (diff < minDiff)
            {
                minDiff = diff;
                closestIndex = i;
            }
        }
        model.Index = closestIndex;

        var element = new ChoiceElement<float>(label, model, description)
        {
            Value = values[closestIndex]
        };

        model.OnValueChanged += value =>
        {
            entry.Value = value;
            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.RefreshReaperBalance(refreshReason, true);
            }
        };

        return element;
    }

    /// <summary>
    /// 创建带滑动条的浮点数选项 ChoiceElement（结合 Choice 的布局/描述 与 Slider 的拖动交互）
    /// </summary>
    private static ChoiceElement<float> CreateFloatChoiceWithSlider(
        string label,
        string description,
        ConfigEntry<float> entry,
        float min,
        float max,
        float step,
        string refreshReason
    )
    {
        // === Choice 侧（保留现有布局与描述） ===
        var values = new System.Collections.Generic.List<float>();
        for (float v = min; v <= max + step * 0.5f; v += step)
        {
            values.Add((float)System.Math.Round(v, 3));
        }

        var choiceModel = new ListChoiceModel<float>(values)
        {
            Circular = false,
            DisplayFn = (_, v) => v.ToString("0.###")
        };

        // 设置初始值（找到最接近的）
        float clamped = Mathf.Clamp(entry.Value, min, max);
        int closestIndex = 0;
        float minDiff = float.MaxValue;
        for (int i = 0; i < values.Count; i++)
        {
            float diff = Mathf.Abs(values[i] - clamped);
            if (diff < minDiff)
            {
                minDiff = diff;
                closestIndex = i;
            }
        }
        choiceModel.Index = closestIndex;

        var choiceElement = new ChoiceElement<float>(label, choiceModel, description)
        {
            Value = values[closestIndex]
        };

        // === Slider 侧（借用 prefab 并嵌入） ===
        int ticks = values.Count;
        if (ticks < 2) ticks = 2;

        var sliderModel = new LinearFloatSliderModel(min, max, ticks)
        {
            DisplayFn = (_, v) => v.ToString("0.###")
        };
        // Keep slider model aligned with the discrete tick list.
        sliderModel.SetValue(values[closestIndex]);

        // 创建临时 SliderElement 以获取 slider prefab
        var tempSliderElement = new SliderElement<float>("_temp_", sliderModel);
        var sliderGO = tempSliderElement.Slider.gameObject;

        // 隐藏 slider 自己的 label（避免与 Choice 的 label 重复）
        if (tempSliderElement.LabelText != null)
        {
            tempSliderElement.LabelText.gameObject.SetActive(false);
        }

        // 获取 Choice 的 ChoiceText（值显示区域）的 RectTransform
        var choiceTextRect = choiceElement.ChoiceText.GetComponent<RectTransform>();

        // 将 slider 移动到 Choice 的 ValueChoice 节点内（与 Menu Option Text 同级）
        var sliderRect = sliderGO.GetComponent<RectTransform>();
        var valueChoiceParent = choiceTextRect.transform.parent;
        if (valueChoiceParent != null)
        {
            sliderGO.transform.SetParent(valueChoiceParent, false);
            // 让 slider 的层级与 Menu Option Text 保持一致，避免遮挡/裁剪异常
            sliderRect.SetSiblingIndex(choiceTextRect.transform.GetSiblingIndex());
        }
        else
        {
            // fallback: keep old behavior
            sliderGO.transform.SetParent(choiceElement.Container.transform, false);
        }

        // 对齐 slider 到 ChoiceText 的位置，但缩小检测区域（右对齐）
        float originalSliderHeight = sliderRect.sizeDelta.y;
        sliderRect.anchorMin = choiceTextRect.anchorMin;
        sliderRect.anchorMax = choiceTextRect.anchorMax;
        sliderRect.pivot = choiceTextRect.pivot;

        // 检测区域缩小到 50%，向右偏移后再向左移动 25%
        float widthRatio = 0.5f;
        float originalWidth = choiceTextRect.sizeDelta.x;
        float newWidth = originalWidth * widthRatio;
        float widthDiff = originalWidth - newWidth;
        float leftShift = originalWidth * 0.25f; // 向左移动 25%

        sliderRect.anchoredPosition = choiceTextRect.anchoredPosition + new Vector2(widthDiff - leftShift, 0);
        sliderRect.sizeDelta = new Vector2(newWidth, Mathf.Max(11f, originalSliderHeight));

        // 调整内部轨道组件的锚点，让轨道向左延伸保持原始视觉宽度
        // anchorMin.x = -1 表示向左延伸一倍容器宽度，总宽度 = 2 * newWidth = originalWidth
        float trackAnchorMinX = -1f;
        foreach (Transform child in sliderGO.transform)
        {
            var childRect = child.GetComponent<RectTransform>();
            if (childRect == null) continue;

            string childName = child.name.ToLowerInvariant();
            // 只调整滑动条轨道相关组件，跳过文本/光标等
            if (childName.Contains("background") || childName.Contains("fill") || childName.Contains("handle") || childName.Contains("slider"))
            {
                // 向左延伸，使轨道视觉宽度恢复原始大小
                childRect.anchorMin = new Vector2(trackAnchorMinX, childRect.anchorMin.y);
                childRect.offsetMin = new Vector2(0, childRect.offsetMin.y);
            }
        }

        // 隐藏 Choice 的 ChoiceText，保留 slider 的 ValueText 显示数值
        choiceElement.ChoiceText.gameObject.SetActive(false);

        // Ensure slider thumb position matches the current value immediately.
        // Runtime evidence (run7) shows the thumb stays at min (index 0) until first interaction.
        try
        {
            if (tempSliderElement.Slider != null)
            {
                tempSliderElement.Slider.SetValueWithoutNotify(closestIndex);
            }
            // Disable slider-internal cursor visuals (runtime evidence: CursorHotspot/CursorLeft/CursorRight)
            foreach (var t in sliderGO.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CursorHotspot" || t.name == "CursorLeft" || t.name == "CursorRight")
                {
                    t.gameObject.SetActive(false);
                }
            }

            // Forward slider hover to the underlying ValueChoice so description behaves like Choice.
            if (valueChoiceParent != null)
            {
                AttachPointerProxy(sliderGO, valueChoiceParent.gameObject, $"SliderToValueChoice:{label}", choiceElement.DescriptionText);
            }

            // Ensure mouse hover selects the Choice row, so description shows as expected.
            AttachForceSelect(
                choiceElement.LabelText.gameObject,
                choiceElement.SelectableComponent.gameObject,
                $"ChoiceLabel:{label}",
                selectOnEnter: true,
                selectOnDown: true
            );
            AttachForceSelect(
                choiceElement.SelectableComponent.gameObject,
                choiceElement.SelectableComponent.gameObject,
                $"ChoiceSelectable:{label}",
                selectOnEnter: true,
                selectOnDown: true
            );
            // Slider tends to steal selection; redirect it back to the Choice row.
            AttachForceSelect(
                sliderGO,
                choiceElement.SelectableComponent.gameObject,
                $"SliderRedirect:{label}",
                selectOnEnter: false,
                selectOnDown: true
            );
        }
        catch
        {
            // ignore
        }

        // 销毁临时 SliderElement 的原容器（slider 已被搬走）
        if (tempSliderElement.Container != null && tempSliderElement.Container != sliderGO)
        {
            UnityEngine.Object.Destroy(tempSliderElement.Container);
        }

        // === 双向同步 ===
        bool isSyncing = false;

        // 用于防抖：监听 slider 拖动结束（而不是值变化），避免松开鼠标时误触发 ChoiceElement 的点击
        var debounceState = new SliderDebounceState();
        AttachSliderDragEndTracker(sliderGO, debounceState);

        // 当 ChoiceModel 改变（左右切换）：同步 sliderModel
        choiceModel.OnValueChanged += value =>
        {
            if (isSyncing) return;

            // 防抖：如果刚刚结束拖动 slider，忽略 ChoiceElement 的点击事件（松开鼠标时可能误触发）
            if (debounceState.LastDragEndTime >= 0 && Time.unscaledTime - debounceState.LastDragEndTime < 0.2f)
            {
                return;
            }

            isSyncing = true;

            entry.Value = value;
            sliderModel.SetValue(value);

            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.RefreshReaperBalance(refreshReason, true);
            }

            isSyncing = false;
        };

        // 当 sliderModel 改变（拖动滑动条）：同步 choiceElement
        sliderModel.OnValueChanged += value =>
        {
            if (isSyncing) return;
            isSyncing = true;

            // 找到最接近的 choice 值
            int newClosestIndex = 0;
            float newMinDiff = float.MaxValue;
            for (int i = 0; i < values.Count; i++)
            {
                float diff = Mathf.Abs(values[i] - value);
                if (diff < newMinDiff)
                {
                    newMinDiff = diff;
                    newClosestIndex = i;
                }
            }

            // 更新 choice model 并同步 entry（注意：由于 isSyncing=true，choiceModel.OnValueChanged 不会执行，所以这里需要手动更新 entry）
            if (choiceModel.Index != newClosestIndex)
            {
                choiceModel.Index = newClosestIndex;
            }
            // 无论 index 是否改变，都需要更新 entry 并刷新（因为 isSyncing 会阻止 choiceModel.OnValueChanged 执行）
            entry.Value = values[newClosestIndex];
            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.RefreshReaperBalance(refreshReason, true);
            }

            isSyncing = false;
        };

        return choiceElement;
    }

    /// <summary>
    /// 用于在 lambda 中共享防抖状态
    /// </summary>
    private sealed class SliderDebounceState
    {
        public float LastDragEndTime = -1f;
    }

    private static void AttachSliderDragEndTracker(GameObject sliderGO, SliderDebounceState state)
    {
        var tracker = sliderGO.GetComponent<AgentSliderDragEndTracker>();
        if (tracker == null) tracker = sliderGO.AddComponent<AgentSliderDragEndTracker>();
        tracker.DebounceState = state;
    }

    private sealed class AgentSliderDragEndTracker : MonoBehaviour, IEndDragHandler, IPointerUpHandler
    {
        public SliderDebounceState? DebounceState;

        public void OnEndDrag(PointerEventData eventData)
        {
            RecordDragEnd();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 同时监听 PointerUp，因为有些情况下 EndDrag 可能不触发
            RecordDragEnd();
        }

        private void RecordDragEnd()
        {
            if (DebounceState != null)
            {
                DebounceState.LastDragEndTime = Time.unscaledTime;
            }
        }
    }

    private static void AttachForceSelect(
        GameObject target,
        GameObject selectTarget,
        string tag,
        bool selectOnEnter,
        bool selectOnDown
    )
    {
        var f = target.GetComponent<AgentForceSelectOnHover>();
        if (f == null) f = target.AddComponent<AgentForceSelectOnHover>();
        f.SelectTarget = selectTarget;
        f.Tag = tag;
        f.SelectOnEnter = selectOnEnter;
        f.SelectOnDown = selectOnDown;
    }

    private static void AttachPointerProxy(GameObject target, GameObject proxyTo, string tag, Text descText)
    {
        var p = target.GetComponent<AgentPointerProxy>();
        if (p == null) p = target.AddComponent<AgentPointerProxy>();
        p.ProxyTo = proxyTo;
        p.Tag = tag;
        p.DescText = descText;
    }

    private sealed class AgentPointerProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject? ProxyTo;
        public string Tag = "";
        public Text? DescText;
        private bool _hovered;
        private float _lastForcedAtUnscaled;
        private RectTransform? _selfRect;
        private Canvas? _canvas;
        private UnityEngine.Camera? _uiCam;
        private bool _lastInside;
        private bool _lastRaycastHover;
        private static readonly System.Collections.Generic.List<RaycastResult> _raycastResults =
            new System.Collections.Generic.List<RaycastResult>(32);

        public void OnPointerEnter(PointerEventData eventData) => Forward(eventData, true);
        public void OnPointerExit(PointerEventData eventData) => Forward(eventData, false);

        private void Awake()
        {
            try
            {
                _selfRect = GetComponent<RectTransform>();
                _canvas = GetComponentInParent<Canvas>();
                if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    _uiCam = _canvas.worldCamera;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void LateUpdate()
        {
            try
            {
                if (DescText == null) return;

                // Prefer EventSystem raycast hover test (more robust than enter/exit flicker and rect math).
                bool rayHover = IsPointerOverSelfHierarchy(out string topHit);

                // Keep old rect-based test for evidence.
                bool rectInside = false;
                if (_selfRect != null)
                {
                    rectInside = RectTransformUtility.RectangleContainsScreenPoint(
                        _selfRect,
                        Input.mousePosition,
                        _uiCam
                    );
                }

                _lastRaycastHover = rayHover;

                _lastInside = rectInside;

                if (!rayHover) return;

                // Keep description visible while pointer stays inside slider rect.
                if (!DescText.gameObject.activeInHierarchy)
                {
                    DescText.gameObject.SetActive(true);
                }

                // Keep description visible while hovering slider region.
                // Other UI logic may fade it out; we restore its alpha.
                var c = DescText.color;
                if (c.a < 0.95f)
                {
                    c.a = 0.95f;
                    DescText.color = c;
                    if ((Time.unscaledTime - _lastForcedAtUnscaled) > 0.05f)
                    {
                        _lastForcedAtUnscaled = Time.unscaledTime;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private bool IsPointerOverSelfHierarchy(out string topHitName)
        {
            topHitName = "<none>";
            try
            {
                if (EventSystem.current == null) return false;

                var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                _raycastResults.Clear();
                EventSystem.current.RaycastAll(ped, _raycastResults);

                for (int i = 0; i < _raycastResults.Count; i++)
                {
                    var go = _raycastResults[i].gameObject;
                    if (go == null) continue;
                    if (i == 0) topHitName = go.name;
                    if (go.transform.IsChildOf(transform)) return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private void Forward(PointerEventData eventData, bool isEnter)
        {
            try
            {
                if (ProxyTo == null) return;

                if (isEnter)
                    ExecuteEvents.Execute(ProxyTo, eventData, ExecuteEvents.pointerEnterHandler);
                else
                    ExecuteEvents.Execute(ProxyTo, eventData, ExecuteEvents.pointerExitHandler);

                _hovered = isEnter;
            }
            catch
            {
                // ignore
            }
        }
    }

    private sealed class AgentForceSelectOnHover : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
    {
        public GameObject? SelectTarget;
        public string Tag = "";
        public bool SelectOnEnter = true;
        public bool SelectOnDown = true;
        private bool _pendingLateSelect;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (SelectOnEnter)
                ForceSelectImmediate("PointerEnter");
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (SelectOnDown)
                ForceSelectNextFrame("PointerDown");
        }

        private void ForceSelectImmediate(string evt)
        {
            try
            {
                if (SelectTarget == null || EventSystem.current == null) return;
                if (EventSystem.current.currentSelectedGameObject == SelectTarget) return;
                EventSystem.current.SetSelectedGameObject(SelectTarget);
            }
            catch
            {
                // ignore
            }
        }

        private void ForceSelectNextFrame(string evt)
        {
            try
            {
                if (!isActiveAndEnabled) return;
                if (_pendingLateSelect) return;
                _pendingLateSelect = true;
                StartCoroutine(ForceSelectNextFrameCoroutine(evt));
            }
            catch
            {
                // ignore
            }
        }

        private IEnumerator ForceSelectNextFrameCoroutine(string evt)
        {
            yield return null;
            try
            {
                if (SelectTarget == null || EventSystem.current == null) yield break;
                if (EventSystem.current.currentSelectedGameObject == SelectTarget) yield break;
                EventSystem.current.SetSelectedGameObject(SelectTarget);
            }
            catch
            {
                // ignore
            }
            finally
            {
                _pendingLateSelect = false;
            }
        }
    }


    /// <summary>
    /// 初始化配置变量
    /// </summary>
    private void InitializeConfig()
    {
        // 全局启用开关
        EnableReaperBalance = Config.Bind("General", "EnableReaperBalance", true,
            "是否启用Reaper平衡修改 (默认: true)");
        UseChinese = Config.Bind("General", "UseChinese", true,
            "是否使用中文菜单 (true=中文, false=English) (默认: true)");
        EnableCrossSlash = Config.Bind("General", "EnableCrossSlash", true,
            "是否启用十字斩 (默认: true)");
        EnableSilkAttraction = Config.Bind("General", "EnableSilkAttraction", true,
            "是否吸引小丝球 (默认: true)");
        CrossSlashScale = Config.Bind("ReaperBalance", "CrossSlashScale", 1.2f,
            "十字斩击缩放大小 (默认: 1.2)");
        CrossSlashDamage = Config.Bind("ReaperBalance", "CrossSlashDamage", 2.3f,
            "十字斩伤害倍率 (nailDamageMultiplier) (默认: 2.3)");
        NormalAttackMultiplier = Config.Bind("ReaperBalance", "NormalAttackMultiplier", 1.2f,
            "普通攻击倍率 (默认: 1.2)");
        DownSlashMultiplier = Config.Bind("ReaperBalance", "DownSlashMultiplier", 1.5f,
            "下劈攻击倍率 (默认: 1.5)");
        CollectRange = Config.Bind("ReaperBalance", "CollectRange", 8f,
            "灵魂吸收范围 (默认: 8)");
        CollectMaxSpeed = Config.Bind("ReaperBalance", "CollectMaxSpeed", 20f,
            "吸引最大速度 (默认: 20)");
        CollectAcceleration = Config.Bind("ReaperBalance", "CollectAcceleration", 800f,
            "吸引加速度 (默认: 800)");
        DurationMultiplier = Config.Bind("ReaperBalance", "DurationMultiplier", 3f,
            "持续时间倍率 (默认: 3.0)");
        StunDamageMultiplier = Config.Bind("ReaperBalance", "StunDamageMultiplier", 1.2f,
            "眩晕值倍率，影响普攻/下劈/十字斩的眩晕值 (默认: 1.2)");
        ReaperBundleMultiplier = Config.Bind("ReaperBalance", "ReaperBundleMultiplier", 1f,
            "小丝球掉落倍率，影响攻击敌人时掉落的丝球数量 (默认: 1.0)");

        // Reaper 暴击配置
        EnableReaperCrit = Config.Bind("ReaperCrit", "EnableReaperCrit", false,
            "是否启用收割者独享暴击系统（仅收割者纹章生效，完全覆盖原版暴击判定）(默认: false)");
        ReaperCritChancePercent = Config.Bind("ReaperCrit", "ReaperCritChancePercent", 10f,
            "收割者暴击概率百分比 (默认: 10, 范围: 0-100)");
        ReaperCritDamageMultiplier = Config.Bind("ReaperCrit", "ReaperCritDamageMultiplier", 3f,
            "收割者暴击伤害倍率 (默认: 3.0, 范围: 1-5)");

        Log.Info("ReaperBalance配置已初始化");
    }

    internal static Plugin? FindPlugin()
    {
        var plugins = Resources.FindObjectsOfTypeAll<Plugin>();
        if (plugins != null && plugins.Length > 0)
        {
            return plugins[0];
        }

        return null;
    }
    private void CreateManager()
    {
        // 查找是否已存在持久化管理器
        ReaperBalanceManager = GameObject.Find("ReaperBalanceManager");
        if (ReaperBalanceManager == null)
        {
            ReaperBalanceManager = new GameObject("ReaperBalanceManager");
            UnityEngine.Object.DontDestroyOnLoad(ReaperBalanceManager);

            // 添加存档管理器组件
            ReaperBalanceManager.AddComponent<AssetManager>();
            Log.Info("创建持久化组件");
        }
        else
        {
            Log.Info("找到已存在的持久化存档切换管理器");
        }

    }

    /// <summary>
    /// 统一入口：根据当前状态刷新组件与配置
    /// </summary>
    public void RefreshReaperBalance(string reason, bool forceConfig)
    {
        if (ReaperBalanceManager == null)
        {
            return;
        }

        UpdateChangeReaperComponent();

        if (forceConfig && _changeReaperComponent != null)
        {
            _changeReaperComponent.ApplyAllChanges(reason);
        }
    }
    /// <summary>
    /// 根据开关状态更新ChangeReaper组件
    /// </summary>
    private void UpdateChangeReaperComponent()
    {
        if (ReaperBalanceManager == null) return;

        // 检查当前是否应该启用组件
        bool shouldBeEnabled = IsReaperBalanceEnabled && IsReaperCrestEquipped();

        // 获取当前组件状态
        _changeReaperComponent = ReaperBalanceManager.GetComponent<ChangeReaper>();
        bool isCurrentlyEnabled = _changeReaperComponent != null;

        // 如果状态不匹配，进行相应操作
        if (shouldBeEnabled && !isCurrentlyEnabled)
        {
            // 添加组件
            _changeReaperComponent = ReaperBalanceManager.AddComponent<ChangeReaper>();
            Log.Info("添加ChangeReaper组件 - 全局开关开启且装备Reaper护符");
        }
        else if (!shouldBeEnabled && isCurrentlyEnabled)
        {
            // 在销毁前重置修改
            if (_changeReaperComponent != null)
            {
                _changeReaperComponent.ResetModifications();
            }
            // 移除组件
            Destroy(_changeReaperComponent);
            _changeReaperComponent = null;
            Log.Info("移除ChangeReaper组件 - 全局开关关闭或未装备Reaper护符");
        }
    }

    /// <summary>
    /// 检查是否装备了Reaper护符
    /// </summary>
    public bool IsReaperCrestEquipped()
    {
        try
        {
            return Gameplay.ReaperCrest != null && Gameplay.ReaperCrest.IsEquipped;
        }
        catch (System.Exception ex)
        {
            Log.Warn($"获取Reaper护符状态失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 公开方法：在游戏内切换开关
    /// </summary>
    public static void ToggleReaperBalance(bool enabled)
    {
        IsReaperBalanceEnabled = enabled;
        // 保存到配置
        if (EnableReaperBalance != null)
        {
            EnableReaperBalance.Value = enabled;
        }
        Log.Info($"Reaper平衡修改已{(enabled ? "启用" : "禁用")}");

        // 通知Plugin实例更新组件状态
        var plugin = FindPlugin();
        if (plugin != null)
        {
            plugin.RefreshReaperBalance("ToggleReaperBalance", true);
        }
    }

    /// <summary>
    /// 检查是否应该应用Harmony补丁
    /// </summary>
    public static bool ShouldApplyPatches()
    {
        return IsReaperBalanceEnabled;
    }

}