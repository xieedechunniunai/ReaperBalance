using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using ReaperBalance.Source.Behaviours;
using GlobalSettings;
using BepInEx.Configuration;
using System.Collections;
using ReaperBalance.Source.Patches;
namespace ReaperBalance.Source;

/// <summary>
/// The main plugin class.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
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
        // 显式注册 Harmony 补丁
        _harmony.PatchAll(typeof(HealthManagerCritPatch));
        _harmony.PatchAll(typeof(HeroControllerBindCompletedPatch));
        _harmony.PatchAll(typeof(HeroControllerGetReaperPayoutPatch));
        _harmony.PatchAll(typeof(ToolItemPatches));
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
            ReaperBalanceManager.AddComponent<ConfigUI>();
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
