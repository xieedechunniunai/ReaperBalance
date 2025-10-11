using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using ReaperBalance.Source.Behaviours;
using GlobalSettings;
using BepInEx.Configuration;
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
    private static ChangeReaper _changeReaperComponent = null;
    public static Plugin Instance { get; private set; }
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
        Plugin.Instance = this;
        SceneManager.activeSceneChanged += OnSceneChange;
        // 初始化配置
        InitializeConfig();
        // 从配置加载启用状态
        IsReaperBalanceEnabled = EnableReaperBalance.Value;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();
        Log.Info("Harmony补丁已应用");
    }

    private void OnSceneChange(Scene oldScene, Scene newScene)
    {
        // Only change things when loading a save file
        UpdateChangeReaperComponent();
        if (oldScene.name == "Menu_Title")
        {
            CreateManager();
            return;
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
            ReaperBalanceManager.AddComponent<ConfigGUI>();
            Log.Info("创建持久化组件");
            // ReaperBalanceManager.AddComponent<ChangeReaper>();
            // 根据开关状态管理ChangeReaper组件
            UpdateChangeReaperComponent();
        }
        else
        {
            Log.Info("找到已存在的持久化存档切换管理器");
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
        return Gameplay.ReaperCrest.IsEquipped;
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
        var pluginInstance = FindObjectOfType<Plugin>();
        if (pluginInstance != null)
        {
            pluginInstance.UpdateChangeReaperComponent();
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