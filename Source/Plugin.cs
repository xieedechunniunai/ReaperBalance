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
        yield return new WaitUntil(() => GameManager.instance != null);
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
            Tr("Enable Reaper Balance", "启用Reaper平衡"),
            ChoiceModels.ForBool(Tr("Disabled", "禁用"), Tr("Enabled", "启用")),
            Tr("Enable or disable Reaper balance changes.", "启用或禁用Reaper平衡修改。")
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

        // 通用攻击倍率 - 独立设置项
        page1.Add(CreateFloatSlider(
            Tr("Normal Attack Multiplier", "普通攻击倍率"),
            NormalAttackMultiplier,
            0.1f,
            3.0f,
            0.1f,
            "ModMenu.NormalAttackMultiplier"
        ));
        page1.Add(CreateFloatSlider(
            Tr("Down Slash Multiplier", "下劈攻击倍率"),
            DownSlashMultiplier,
            0.1f,
            4.0f,
            0.1f,
            "ModMenu.DownSlashMultiplier"
        ));
        page1.Add(CreateFloatSliderWithDescription(
            Tr("Stun Damage Multiplier", "眩晕值倍率"),
            Tr("Normal/Down/Cross", "普攻/下劈/十字斩"),
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
            Tr("Enable or disable the Cross Slash heavy attack.", "启用或禁用十字斩重攻击。")
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
            Tr("Cross Slash Scale", "十字斩缩放"),
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

        // 持续时间倍率 - 独立设置项
        page2.Add(CreateFloatSlider(
            Tr("Duration Multiplier", "持续时间倍率"),
            DurationMultiplier,
            0.2f,
            10.0f,
            0.1f,
            "ModMenu.DurationMultiplier"
        ));
        page2.Add(CreateFloatSlider(
            Tr("Reaper Bundle Multiplier", "丝球掉落倍率"),
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
            Tr("Enable or disable attracting silk orbs from a distance.", "启用或禁用远距离吸引小丝球。")
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
            Tr("Enable Reaper Crit", "启用 Reaper 暴击"),
            ChoiceModels.ForBool(Tr("Disabled", "禁用"), Tr("Enabled", "启用")),
            Tr("Enable Reaper-exclusive crit system (only works with Reaper Crest, overrides vanilla crit)",
               "启用 Reaper 独享暴击系统（仅 Reaper 纹章生效，完全覆盖原版暴击判定）")
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

    private static SliderElement<float> CreateFloatSliderWithDescription(
        string label,
        string description,
        ConfigEntry<float> entry,
        float min,
        float max,
        float step,
        string refreshReason
    )
    {
        // 注意：该 ModMenu UI 对富文本/换行支持不稳定，可能导致字号异常和布局错乱。
        // 因此将描述压缩到同一行，避免换行与富文本标签。
        string labelWithDesc = $"{label} ({description})";
        return CreateFloatSlider(labelWithDesc, entry, min, max, step, refreshReason);
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
            "Reaper bundle 生成倍率，影响攻击敌人时掉落的丝球数量 (默认: 1.0)");
        
        // Reaper 暴击配置
        EnableReaperCrit = Config.Bind("ReaperCrit", "EnableReaperCrit", false,
            "是否启用 Reaper 独享暴击系统（仅 Reaper 纹章生效，完全覆盖原版暴击判定）(默认: false)");
        ReaperCritChancePercent = Config.Bind("ReaperCrit", "ReaperCritChancePercent", 10f,
            "Reaper 暴击概率百分比 (默认: 10, 范围: 0-100)");
        ReaperCritDamageMultiplier = Config.Bind("ReaperCrit", "ReaperCritDamageMultiplier", 3f,
            "Reaper 暴击伤害倍率 (默认: 3.0, 范围: 1-5)");

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