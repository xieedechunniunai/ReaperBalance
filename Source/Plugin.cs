using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using ReaperBalance.Source.Behaviours;
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
    public static ConfigEntry<float> CrossSlashScale { get; private set; } = null!;
    public static ConfigEntry<float> DamageMultiplier { get; private set; } = null!;
    public static ConfigEntry<float> NormalAttackMultiplier { get; private set; } = null!;
    public static ConfigEntry<float> DownSlashMultiplier { get; private set; } = null!;
    public static ConfigEntry<float> CollectRange { get; private set; } = null!;
    public static ConfigEntry<float> DurationMultiplier { get; private set; } = null!;
    private void Awake()
    {
        Log.Init(Logger);
        SceneManager.activeSceneChanged += OnSceneChange;
        // 初始化配置
        InitializeConfig();
        // 从配置加载启用状态
        IsReaperBalanceEnabled = EnableReaperBalance.Value;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin));
        Log.Info("Harmony补丁已应用");
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

    public AbstractMenuScreen BuildCustomMenu()
    {
        var menu = new SimpleMenuScreen(MyPluginInfo.PLUGIN_NAME);

        var enabledChoice = new ChoiceElement<bool>(
            "Enable Reaper Balance",
            ChoiceModels.ForBool("Disabled", "Enabled"),
            "Enable or disable Reaper balance changes."
        )
        {
            Value = IsReaperBalanceEnabled
        };
        enabledChoice.OnValueChanged += enabled => ToggleReaperBalance(enabled);
        menu.Add(enabledChoice);

        menu.Add(CreateFloatSlider(
            "Cross Slash Scale",
            CrossSlashScale,
            0.5f,
            3.0f,
            0.1f,
            "ModMenu.CrossSlashScale"
        ));
        menu.Add(CreateFloatSlider(
            "Cross Slash Damage Multiplier",
            DamageMultiplier,
            0.1f,
            3.0f,
            0.1f,
            "ModMenu.DamageMultiplier"
        ));
        menu.Add(CreateFloatSlider(
            "Normal Attack Multiplier",
            NormalAttackMultiplier,
            0.1f,
            3.0f,
            0.1f,
            "ModMenu.NormalAttackMultiplier"
        ));
        menu.Add(CreateFloatSlider(
            "Down Slash Multiplier",
            DownSlashMultiplier,
            0.1f,
            4.0f,
            0.1f,
            "ModMenu.DownSlashMultiplier"
        ));
        menu.Add(CreateFloatSlider(
            "Collect Range",
            CollectRange,
            1.0f,
            20.0f,
            0.5f,
            "ModMenu.CollectRange"
        ));
        menu.Add(CreateFloatSlider(
            "Duration Multiplier",
            DurationMultiplier,
            1.0f,
            10.0f,
            0.5f,
            "ModMenu.DurationMultiplier"
        ));

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

        model.OnValueChanged += value =>
        {
            entry.Value = value;
            var plugin = FindPlugin();
            if (plugin != null)
            {
                plugin.RefreshReaperBalance(refreshReason, true);
            }
        };

        return new SliderElement<float>(label, model);
    }
    /// <summary>
    /// 初始化配置变量
    /// </summary>
    private void InitializeConfig()
    {
        // 全局启用开关
        EnableReaperBalance = Config.Bind("General", "EnableReaperBalance", true,
            "是否启用Reaper平衡修改 (默认: true)");
        CrossSlashScale = Config.Bind("ReaperBalance", "CrossSlashScale", 1.2f,
            "十字斩击缩放大小 (默认: 1.2)");
        DamageMultiplier = Config.Bind("ReaperBalance", "DamageMultiplier", 1.0f,
            "十字斩伤害倍率 (默认: 1.0)");
        NormalAttackMultiplier = Config.Bind("ReaperBalance", "NormalAttackMultiplier", 1.2f,
            "普通攻击倍率 (默认: 1.2)");
        DownSlashMultiplier = Config.Bind("ReaperBalance", "DownSlashMultiplier", 1.5f,
            "下劈攻击倍率 (默认: 1.5)");
        CollectRange = Config.Bind("ReaperBalance", "CollectRange", 8f,
            "灵魂吸收范围 (默认: 8)");
        DurationMultiplier = Config.Bind("ReaperBalance", "DurationMultiplier", 3f,
            "持续时间倍率 (默认: 3.0)");

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
            // ReaperBalanceManager.AddComponent<ChangeReaper>();
            // 根据开关状态管理ChangeReaper组件
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
    private bool IsReaperCrestEquipped()
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