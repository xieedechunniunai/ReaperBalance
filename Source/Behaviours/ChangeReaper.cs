using System.Collections;
using System.Linq;
using System.Reflection;
using GenericVariableExtension;
using UnityEngine;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using GlobalSettings;
namespace ReaperBalance.Source.Behaviours;

/// <summary>
/// Component that modifies Reaper-related behavior and assets.
/// </summary>
internal sealed class ChangeReaper : MonoBehaviour
{
    #region Lifecycle
    private void Awake()
    {
        // Start initialization
        StartCoroutine(Initialize());
    }

    /// <summary>
    /// 统一入口：应用所有修改（非每帧调用）
    /// </summary>
    public void ApplyAllChanges(string reason)
    {
        if (!_isInitialized)
        {
            StartCoroutine(ApplyAllChangesWhenReady(reason));
            return;
        }

        ApplyAllChangesInternal(reason);
    }

    private IEnumerator ApplyAllChangesWhenReady(string reason)
    {
        yield return new WaitUntil(() => _isInitialized);
        ApplyAllChangesInternal(reason);
    }

    private void ApplyAllChangesInternal(string reason)
    {
        ForceUpdateConfig();
    }
    /// <summary>
    /// 强制更新所有配置
    /// </summary>
    public void ForceUpdateConfig()
    {
        if (!_isInitialized) return;
        // 检查 EnableCrossSlash 开关
        if (Plugin.EnableCrossSlash.Value)
        {
            // 开关开启时，应用修改
            ModifyReaperHeavy();
            
            // 从AssetPool获取缓存的预制体
            var cachedPrefab = _assetManager.GetCachedPrefab(CROSS_SLASH_PREFAB_NAME);
            if (cachedPrefab != null)
            {
                UpdatePrefabScale(cachedPrefab);
                UpdatePrefabDamage(cachedPrefab);
            }
        }
        else
        {
            // 开关关闭时，重置重攻击修改
            ResetHeavyAttackModifiers();
        }

        // 更新普通攻击倍率
        ModifyReaperNormal();

        // 更新ReaperSilk范围
        UpdateReaperSilkRange();

    }
    #endregion

    #region Component Implementation
    private bool _isInitialized = false;
    private HeroController _heroController;
    private AssetManager _assetManager;

    // AssetPool中的预制体名称常量
    private const string CROSS_SLASH_PREFAB_NAME = "Song Knight CrossSlash Cached";

    // 保存原始的FSM动作，用于重置
    private FsmStateAction[] _originalDoSlashActions = null;

    // 存储原始 stunDamage 值的字典（key = DamageEnemies.GetInstanceID()）
    // 用于避免重复应用倍率时叠乘
    private readonly Dictionary<int, float> _baseStunDamage = new Dictionary<int, float>();
    private IEnumerator Initialize()
    {
        _heroController = HeroController.instance;
        _assetManager = GetComponent<AssetManager>();
        if (_assetManager == null)
        {
            Log.Error("AssetManager is missing on ReaperBalanceManager.");
            yield break;
        }
        // Wait for AssetManager to be ready
        yield return new WaitUntil(() => _assetManager.IsInitialized());

        // 预加载并缓存预制体
        yield return PreloadAndCachePrefab();
        ModifyReaperHeavy();

        ModifyReaperNormal();
        _isInitialized = true;

        yield return new WaitForSeconds(1);
        ModifyReaperSilk();
    }

    /// <summary>
    /// 预加载并缓存预制体到AssetPool
    /// </summary>
    private IEnumerator PreloadAndCachePrefab()
    {
        // Check if already cached in AssetPool
        if (_assetManager.IsPrefabCached(CROSS_SLASH_PREFAB_NAME))
        {
            var existing = _assetManager.GetCachedPrefab(CROSS_SLASH_PREFAB_NAME);
            if (existing != null)
            {
                yield break;
            }
        }


        // 从AssetManager获取原始预制体
        GameObject crossSlashPrefab = _assetManager.Get<GameObject>("Song Knight CrossSlash Friendly");
        if (crossSlashPrefab == null)
        {
            Log.Error("Song Knight CrossSlash prefab not found in AssetManager!");
            yield break;
        }

        // 创建预制体的副本进行修改，不修改原始预制体
        GameObject cachedPrefab = Instantiate(crossSlashPrefab);
        cachedPrefab.name = CROSS_SLASH_PREFAB_NAME;
        cachedPrefab.SetActive(false);
        cachedPrefab.transform.localScale = Vector3.one * Plugin.CrossSlashScale.Value;

        // 显式配置十字斩的 DamageEnemies 组件
        ConfigureCrossSlashDamagers(cachedPrefab);

        // 存储到AssetPool（会自动设置parent并保持inactive）
        _assetManager.StorePrefabInPool(CROSS_SLASH_PREFAB_NAME, cachedPrefab);

    }

    private void SpawnCrossSlash()
    {
        // 从AssetPool获取缓存的预制体
        var cachedPrefab = _assetManager.GetCachedPrefab(CROSS_SLASH_PREFAB_NAME);
        if (cachedPrefab == null)
        {
            Log.Error("CrossSlash prefab not found in AssetPool! Please wait for initialization to complete.");
            return;
        }

        // Get the hero controller if not already cached
        if (_heroController == null)
        {
            _heroController = HeroController.instance;
            if (_heroController == null)
            {
                Log.Error("HeroController instance not found!");
                return;
            }
        }

        // Get hero position and direction
        Vector3 heroPosition = _heroController.transform.position;
        Quaternion spawnRotation = GetSpawnRotation();

        // Instantiate from AssetPool prefab at hero position with correct rotation
        // Note: Instance is created in scene, not in AssetPool
        GameObject crossSlashInstance = Instantiate(cachedPrefab, heroPosition, spawnRotation);

        // Make sure the instance is active and not parented to AssetPool
        crossSlashInstance.transform.SetParent(null);
        crossSlashInstance.SetActive(true);

        // 同步英雄 slash 01 的动态效果（NailImbuement/NailElement/毒刀/电刀）到十字斩实例
        SyncCrossSlashDynamicFromHeroSlash01(crossSlashInstance);

        // 实例已经包含了显式配置的组件（attackType=Heavy, tag=Nail Attack）+ 动态同步的 imbuement/element
    }

    /// <summary>
    /// 同步英雄当前的 Imbuement 效果到十字斩实例（火刀/毒刀/电刀等）
    /// 关键：使用 HeroController.NailImbuement.CurrentImbuement 来驱动“颜色/SlashEffect”等视觉效果，
    /// 并同步到 DamageEnemies 以确保伤害与 DOT 正确。
    /// </summary>
    private void SyncCrossSlashDynamicFromHeroSlash01(GameObject crossSlashInstance)
    {
        try
        {
            HeroController hero = HeroController.instance;
            if (hero == null || hero.NailImbuement == null)
            {
                Log.Warn("Cannot sync dynamic effects: HeroController or NailImbuement not found");
                return;
            }

            // 从 HeroController.NailImbuement 获取当前元素与配置（原版视觉 tint 由 CurrentImbuement 驱动）
            NailElements currentElement = hero.NailImbuement.CurrentElement;
            NailImbuementConfig imbuementConfig = hero.NailImbuement.CurrentImbuement;
            if (imbuementConfig == null)
            {
                // 兜底：某些情况下 CurrentImbuement 可能为空，但仍能从元素推导（例如 Fire）
                imbuementConfig = GetImbuementConfigForElement(currentElement);
            }

            // 同步刀光颜色与 SlashEffect（不影响 White Flash R）
            ApplyCrossSlashImbuementVisuals(crossSlashInstance, imbuementConfig);

            // 获取毒刀/电刀 ticks（检查装备状态）
            int poisonTicks = GetPoisonTicksFromEquipment();
            int zapTicks = GetZapTicksFromEquipment();

            // 同步到 Damager1
            Transform damager1 = crossSlashInstance.transform.Find("Damager1");
            if (damager1 != null)
            {
                SyncSingleDamagerDynamicEffects(damager1.gameObject, imbuementConfig, currentElement, poisonTicks, zapTicks, "Damager1");
            }

            // 同步到 Damager2
            Transform damager2 = crossSlashInstance.transform.Find("Damager2");
            if (damager2 != null)
            {
                SyncSingleDamagerDynamicEffects(damager2.gameObject, imbuementConfig, currentElement, poisonTicks, zapTicks, "Damager2");
            }
        }
        catch (System.Exception e)
        {
            Log.Error($"Failed to sync dynamic effects to CrossSlash: {e}");
        }
    }

    /// <summary>
    /// 将 Imbuement 的视觉效果（刀光 tint、SlashEffect）应用到十字斩实例。
    /// 仅处理：本体 + Sharp Flash / Sharp Flash (1)；不会修改 White Flash R。
    /// </summary>
    private static void ApplyCrossSlashImbuementVisuals(GameObject crossSlashInstance, NailImbuementConfig imbuementConfig)
    {
        if (crossSlashInstance == null || imbuementConfig == null)
        {
            return;
        }

        try
        {
            // 1) 刀光 tint：本体 + 两个 Sharp Flash
            Color tint = imbuementConfig.NailTintColor;
            TryTintTk2dSprite(crossSlashInstance.transform, tint);

            Transform sharpFlash0 = crossSlashInstance.transform.Find("Sharp Flash");
            if (sharpFlash0 != null)
            {
                TryTintTk2dSprite(sharpFlash0, tint);
            }

            Transform sharpFlash1 = crossSlashInstance.transform.Find("Sharp Flash (1)");
            if (sharpFlash1 != null)
            {
                TryTintTk2dSprite(sharpFlash1, tint);
            }

            // 2) SlashEffect：出刀时生成一次（不需要额外音效）
            GameObject slashEffectPrefab = imbuementConfig.SlashEffect;
            if (slashEffectPrefab != null)
            {
                GameObject fx = Instantiate(slashEffectPrefab, crossSlashInstance.transform);
                fx.transform.localPosition = Vector3.zero;
                fx.transform.localRotation = Quaternion.identity;
                fx.transform.localScale = Vector3.one;
                fx.SetActive(true);
            }
        }
        catch (System.Exception e)
        {
            // 视觉失败不应影响伤害逻辑
            Log.Debug($"Failed to apply CrossSlash imbuement visuals: {e.Message}");
        }
    }

    private static void TryTintTk2dSprite(Transform target, Color tint)
    {
        if (target == null)
        {
            return;
        }

        // tk2dSprite 在游戏侧存在；如果此对象没有则跳过
        var sprite = target.GetComponent<tk2dSprite>();
        if (sprite != null)
        {
            sprite.color = tint;
        }
    }

    /// <summary>
    /// 根据 NailElements 枚举获取对应的 NailImbuementConfig
    /// </summary>
    private NailImbuementConfig GetImbuementConfigForElement(NailElements element)
    {
        switch (element)
        {
            case NailElements.Fire:
                // Effects.FireNail 是火刀的静态配置
                return Effects.FireNail;
            // 可以在这里添加其他元素的配置
            // case NailElements.Ice:
            //     return Effects.IceNail;
            default:
                return null;
        }
    }

    /// <summary>
    /// 从装备状态获取毒刀 ticks
    /// </summary>
    private int GetPoisonTicksFromEquipment()
    {
        try
        {
            // 检查毒袋工具是否装备
            if (Gameplay.PoisonPouchTool != null && Gameplay.PoisonPouchTool.Status.IsEquipped)
            {
                // 返回默认的毒刀 tick 数（根据游戏逻辑调整）
                return 3; // 典型的毒伤害 tick 数
            }
        }
        catch (System.Exception e)
        {
            Log.Debug($"Failed to check PoisonPouchTool: {e.Message}");
        }
        return 0;
    }

    /// <summary>
    /// 从装备状态获取电刀 ticks
    /// </summary>
    private int GetZapTicksFromEquipment()
    {
        try
        {
            // 检查电刀工具是否装备
            if (Gameplay.ZapImbuementTool != null && Gameplay.ZapImbuementTool.Status.IsEquipped)
            {
                // 返回默认的电刀 tick 数（根据游戏逻辑调整）
                return 1; // 典型的电伤害 tick 数
            }
        }
        catch (System.Exception e)
        {
            Log.Debug($"Failed to check ZapImbuementTool: {e.Message}");
        }
        return 0;
    }

    /// <summary>
    /// 同步单个 damager 的动态效果
    /// </summary>
    private void SyncSingleDamagerDynamicEffects(
        GameObject damagerObject, 
        NailImbuementConfig nailImbuement, 
        NailElements nailElement,
        int poisonTicks,
        int zapTicks,
        string damagerName)
    {
        DamageEnemies targetDamager = damagerObject.GetComponent<DamageEnemies>();
        if (targetDamager == null)
        {
            Log.Warn($"DamageEnemies component not found on {damagerName} for dynamic sync");
            return;
        }

        // 同步 NailImbuement 和 NailElement（公开属性，可直接设置）
        targetDamager.NailImbuement = nailImbuement;
        targetDamager.NailElement = nailElement;

        // 同步毒刀 ticks（使用公开方法）
        if (poisonTicks > 0)
        {
            targetDamager.OverridePoisonDamage(poisonTicks);
        }

        // 同步电刀 ticks（需要反射设置私有字段）
        if (zapTicks > 0)
        {
            var damagerType = typeof(DamageEnemies);
            var zapTicksField = damagerType.GetField("zapDamageTicks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (zapTicksField != null)
            {
                zapTicksField.SetValue(targetDamager, zapTicks);
            }
        }
    }
    /// <summary>
    /// 更新预制体缩放大小
    /// </summary>
    private void UpdatePrefabScale(GameObject prefab)
    {
        if (prefab != null)
        {
            prefab.transform.localScale = Vector3.one * Plugin.CrossSlashScale.Value;
        }
    }

    /// <summary>
    /// 更新ReaperSilk吸收范围
    /// </summary>
    private void UpdateReaperSilkRange()
    {
        // 更新预制体上的组件
        if (_assetManager == null)
        {
            Log.Warn("AssetManager is not ready for range update.");
            return;
        }
        var reaperSilkBundle = _assetManager.Get<GameObject>("Reaper Silk Bundle");
        if (reaperSilkBundle != null)
        {
            var modifier = reaperSilkBundle.GetComponent<ReaperSilkRangeModifier>();
            if (modifier != null)
            {
                modifier.CollectRange = Plugin.CollectRange.Value;
                modifier.CollectMaxSpeed = Plugin.CollectMaxSpeed.Value;
                modifier.CollectAcceleration = Plugin.CollectAcceleration.Value;
            }
        }

        // 更新现有实例
        StartCoroutine(UpdateExistingBundlesRange());
    }

    /// <summary>
    /// 更新现有ReaperSilk实例的范围
    /// </summary>
    private IEnumerator UpdateExistingBundlesRange()
    {
        yield return null; // 等待一帧

        var allBundles = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(obj => obj.name.Contains("Reaper Silk Bundle") && obj.scene.IsValid())
            .ToArray();

        int updatedCount = 0;
        foreach (var bundle in allBundles)
        {
            var modifier = bundle.GetComponent<ReaperSilkRangeModifier>();
            if (modifier != null)
            {
                modifier.CollectRange = Plugin.CollectRange.Value;
                modifier.CollectMaxSpeed = Plugin.CollectMaxSpeed.Value;
                modifier.CollectAcceleration = Plugin.CollectAcceleration.Value;
                updatedCount++;
            }
        }
    }
    /// <summary>
    /// 更新预制体伤害值 - 重新配置十字斩 DamageEnemies 组件
    /// </summary>
    private void UpdatePrefabDamage(GameObject prefab)
    {
        try
        {
            // 重新配置十字斩（主要是更新 nailDamageMultiplier 和其他配置）
            ConfigureCrossSlashDamagers(prefab);
        }
        catch (System.Exception e)
        {
            Log.Error($"响应式伤害更新失败: {e}");
        }
    }
    /// <summary>
    /// 获取生成时的旋转角度，根据英雄面对的方向
    /// </summary>
    private Quaternion GetSpawnRotation()
    {

        // 获取英雄的transform
        Transform heroTransform = HeroController.instance.transform;

        // 检查英雄的朝向 - 通过localScale.x的正负来判断
        // 通常：localScale.x > 0 表示朝右，localScale.x < 0 表示朝左
        float heroScaleX = heroTransform.localScale.x;

        if (heroScaleX < 0) // 英雄朝右
        {
            // 英雄朝右时，让CrossSlash也朝右
            // 通常需要绕Y轴旋转180度来面向右侧
            return Quaternion.Euler(0, 180, 0);
        }
        else if (heroScaleX > 0) // 英雄朝左
        {
            // 英雄朝左时，使用默认朝向（通常是朝左）
            return Quaternion.identity;
        }
        else // 无法确定方向，使用默认
        {
            Log.Warn("Cannot determine hero facing direction, using default rotation");
            return Quaternion.identity;
        }
    }

    /// <summary>
    /// Check if the ChangeReaper component is initialized.
    /// </summary>
    public bool IsInitialized()
    {
        return _isInitialized;
    }

    /// <summary>
    /// Check if the CrossSlash prefab is cached in AssetPool.
    /// </summary>
    public bool IsPrefabCached()
    {
        if (_assetManager == null) return false;
        return _assetManager.IsPrefabCached(CROSS_SLASH_PREFAB_NAME);
    }

    /// <summary>
    /// 显式配置十字斩的 DamageEnemies 组件（不再从英雄反射复制）
    /// </summary>
    private void ConfigureCrossSlashDamagers(GameObject crossSlashPrefabOrInstance)
    {
        try
        {
            // 处理 Damager1
            Transform damager1 = crossSlashPrefabOrInstance.transform.Find("Damager1");
            if (damager1 != null)
            {
                ConfigureSingleCrossSlashDamager(damager1.gameObject, "Damager1");
            }
            else
            {
                Log.Warn("Damager1 not found in CrossSlash");
            }

            // 处理 Damager2
            Transform damager2 = crossSlashPrefabOrInstance.transform.Find("Damager2");
            if (damager2 != null)
            {
                ConfigureSingleCrossSlashDamager(damager2.gameObject, "Damager2");
            }
            else
            {
                Log.Warn("Damager2 not found in CrossSlash");
            }

            Log.Info("Successfully configured CrossSlash damagers with explicit settings");
        }
        catch (System.Exception e)
        {
            Log.Error($"Failed to configure CrossSlash damagers: {e}");
        }
    }

    /// <summary>
    /// 配置单个十字斩 DamageEnemies 组件
    /// 仅配置 nailDamageMultiplier
    /// </summary>
    private void ConfigureSingleCrossSlashDamager(GameObject damagerObject, string damagerName)
    {
        DamageEnemies targetDamager = damagerObject.GetComponent<DamageEnemies>();
        if (targetDamager == null)
        {
            Log.Warn($"DamageEnemies component not found on {damagerName}");
            return;
        }

        // 设置 GameObject tag 为 "Nail Attack"（保证 Heavy 同时被当作 nail hit）
        damagerObject.tag = "Nail Attack";

        // 显式设置公开属性
        targetDamager.useNailDamage = true;
        targetDamager.nailDamageMultiplier = Plugin.CrossSlashDamage.Value;
        targetDamager.attackType = AttackTypes.Heavy;
        
        // 十字斩基础 stunDamage 为 1，乘以配置的倍率
        float baseStunDamage = 1f;
        targetDamager.stunDamage = baseStunDamage * Plugin.StunDamageMultiplier.Value;
        targetDamager.canWeakHit = false;
        targetDamager.magnitudeMult = 1f;
        targetDamager.direction = 0f;
        targetDamager.moveDirection = false;

        // 使用反射设置私有字段
        var damagerType = typeof(DamageEnemies);

        // 设置 silkGeneration 为 Full (HitSilkGeneration.Full = 0)
        var silkGenerationField = damagerType.GetField("silkGeneration", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (silkGenerationField != null)
        {
            // HitSilkGeneration.Full = 0
            silkGenerationField.SetValue(targetDamager, 0);
        }
        
        // 设置 directionSourceOverride 为 CircleDirection (1)
        var directionSourceOverrideField = damagerType.GetField("directionSourceOverride", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (directionSourceOverrideField != null)
        {
            // DirectionSourceOverrides.CircleDirection = 1
            directionSourceOverrideField.SetValue(targetDamager, 1);
        }
        
        // 设置 isHeroDamage 为 true
        var isHeroDamageField = damagerType.GetField("isHeroDamage", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (isHeroDamageField != null)
        {
            isHeroDamageField.SetValue(targetDamager, true);
        }
        
        // 设置 ignoreInvuln 为 false
        var ignoreInvulnField = damagerType.GetField("ignoreInvuln", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (ignoreInvulnField != null)
        {
            ignoreInvulnField.SetValue(targetDamager, false);
        }

    }

    #endregion
    #region modifyReaperHeavyAttack
    /// <summary>
    /// 修改Reaper的HeavyAttack伤害
    /// </summary>
    private void ModifyReaperHeavy()
    {
        // 添加全局开关检查
        if (!Plugin.IsReaperBalanceEnabled)
        {
            return;
        }
        
        // 检查 EnableCrossSlash 开关
        if (!Plugin.EnableCrossSlash.Value)
        {
            return;
        }
        
        if (_heroController == null)
        {
            Log.Error("HeroController is null! Cannot modify FSM.");
            return;
        }

        PlayMakerFSM nailArtsFSM = null;
        PlayMakerFSM[] fsms = _heroController.GetComponents<PlayMakerFSM>();
        foreach (var fsm in fsms)
        {
            if (fsm.FsmName == "Nail Arts")
            {
                nailArtsFSM = fsm;
                break;
            }
        }

        if (nailArtsFSM != null)
        {
            var doSlashState = nailArtsFSM.FsmStates.FirstOrDefault(s => s.Name == "Do Slash");
            if (doSlashState != null)
            {
                // 保存原始动作（仅在首次修改时保存）
                if (_originalDoSlashActions == null)
                {
                    _originalDoSlashActions = doSlashState.Actions.ToArray();
                }
                var actionsList = doSlashState.Actions.ToList();
                for (int i = 0; i < actionsList.Count; i++)
                {
                    var action = actionsList[i];

                    // 禁用ActivateGameObject，使用安全空动作
                    if (action is ActivateGameObject)
                    {
                        Log.Info("禁用Do Slash状态中的ActivateGameObject动作");
                        var emptyAction = new SafeEmptyAction();
                        actionsList[i] = emptyAction;
                    }
                    // 替换OnSlashStarting为自定义动作
                    else if (action is SendMessageV2 sendMessageV2)
                    {
                        string message = sendMessageV2.functionCall?.FunctionName;

                        if (message == "OnSlashStarting")
                        {
                            Log.Info("替换OnSlashStarting为自定义攻击动作");
                            var customAction = new CustomSpawnAction
                            {
                                Owner = nailArtsFSM.gameObject,
                                Fsm = nailArtsFSM,
                                Reaper = this
                            };
                            actionsList[i] = customAction;
                        }
                    }
                }


                doSlashState.Actions = actionsList.ToArray();
                Log.Info("已修改Do Slash状态");
            }
            else
            {
                Log.Warn("Do Slash状态未找到");
            }
        }
        else
        {
            Log.Warn("Nail Arts FSM not found on HeroController");
        }
    }
    // 安全的空动作，避免NullReferenceException
    private class SafeEmptyAction : FsmStateAction
    {
        public override void OnEnter()
        {
            // 什么都不做，直接完成
            Finish();
        }
    }
    // 改进的自定义生成动作
    private class CustomSpawnAction : FsmStateAction
    {
        public GameObject Owner { get; set; }
        public PlayMakerFSM Fsm { get; set; }
        public ChangeReaper Reaper { get; set; }

        public override void OnEnter()
        {
            try
            {
                if (Reaper != null && Reaper.IsInitialized())
                {
                    Reaper.SpawnCrossSlash();
                }
                else
                {
                    ReaperBalance.Source.Log.Error("ChangeReaper未初始化，无法生成攻击");
                }

                Finish();
                ReaperBalance.Source.Log.Info("自定义攻击动作执行完成");
            }
            catch (System.Exception e)
            {
                ReaperBalance.Source.Log.Error($"自定义攻击动作执行出错: {e}");
                Finish();
            }
        }
    }
    #endregion
    #region modifyNormalAttack
    /// <summary>
    /// 修改Reaper的普攻Attack伤害
    /// </summary>
    private void ModifyReaperNormal()
    { // 添加全局开关检查
        if (!Plugin.IsReaperBalanceEnabled)
        {
            Log.Info("全局开关关闭，跳过NormalAttack修改");
            return;
        }
        if (_heroController == null)
        {
            Log.Error("HeroController is null! Cannot modify FSM.");
            return;
        }
        var Attack = _heroController.gameObject.transform.Find("Attacks");
        if (Attack != null)
        {
            Log.Info("找到Attack GameObject");
            Transform Scythe = Attack.gameObject.transform.Find("Scythe");
            if (Scythe != null)
            {
                ModifyDamageMultiplierRecursive(Scythe, Plugin.NormalAttackMultiplier.Value);
                Transform DownSlash = Scythe.gameObject.transform.Find("DownSlash New");
                if (DownSlash != null)
                {
                    DamageEnemies damageEnemies = DownSlash.GetComponent<DamageEnemies>();
                    if (damageEnemies != null)
                    {
                        damageEnemies.damageMultiplier = Plugin.DownSlashMultiplier.Value;
                        
                        // 应用眩晕值倍率
                        ApplyStunDamageMultiplier(damageEnemies, DownSlash.name);
                        
                        Log.Info($"修改 {DownSlash.name} 的DamageEnemies: damageMultiplier={damageEnemies.damageMultiplier}, stunDamage={damageEnemies.stunDamage}");
                    }
                    else
                    {
                        Log.Warn("DownSlash的DamageEnemies组件未找到");
                    }

                }
                else
                {
                    Log.Warn("DownSlash GameObject not found");
                }
            }
            else
            {
                Log.Warn("Scythe GameObject not found");
            }
        }
        else
        {
            Log.Warn("Attack GameObject not found");
        }
    }
    /// <summary>
    /// 递归修改Transform及其所有子物体的DamageEnemies组件的damageMultiplier和stunDamage值
    /// </summary>
    /// <param name="transform">要处理的Transform</param>
    /// <param name="multiplier">要设置的伤害乘数值</param>
    private void ModifyDamageMultiplierRecursive(Transform transform, float multiplier)
    {
        // 修改当前对象的DamageEnemies组件
        DamageEnemies damageEnemies = transform.GetComponent<DamageEnemies>();
        if (damageEnemies != null && transform.name != "DownSlash New")
        {
            damageEnemies.damageMultiplier = multiplier;
            
            // 应用眩晕值倍率（避免叠乘：使用缓存的基础值）
            ApplyStunDamageMultiplier(damageEnemies, transform.name);
            
            Log.Info($"修改 {transform.name} 的DamageEnemies.damageMultiplier为 {multiplier}, stunDamage为 {damageEnemies.stunDamage}");
        }

        // 递归处理所有子物体
        foreach (Transform child in transform)
        {
            ModifyDamageMultiplierRecursive(child, multiplier);
        }
    }

    /// <summary>
    /// 应用眩晕值倍率到指定的 DamageEnemies 组件
    /// 使用缓存的基础值避免重复应用时叠乘
    /// </summary>
    private void ApplyStunDamageMultiplier(DamageEnemies damageEnemies, string objectName)
    {
        int instanceId = damageEnemies.GetInstanceID();
        
        // 如果没有缓存过基础值，先记录当前值作为基础值
        if (!_baseStunDamage.ContainsKey(instanceId))
        {
            _baseStunDamage[instanceId] = damageEnemies.stunDamage;
            Log.Debug($"记录 {objectName} 的基础 stunDamage: {damageEnemies.stunDamage}");
        }
        
        // 使用基础值乘以倍率
        float baseValue = _baseStunDamage[instanceId];
        damageEnemies.stunDamage = baseValue * Plugin.StunDamageMultiplier.Value;
    }

    /// <summary>
    /// 重置指定 DamageEnemies 组件的 stunDamage 为基础值
    /// </summary>
    private void ResetStunDamageToBase(DamageEnemies damageEnemies)
    {
        int instanceId = damageEnemies.GetInstanceID();
        if (_baseStunDamage.TryGetValue(instanceId, out float baseValue))
        {
            damageEnemies.stunDamage = baseValue;
        }
    }
    #endregion
    #region modifyPrefabReaperSilk
    /// <summary>
    /// 修改Reaper的吸收范围为8单位 - 运行时动态修改版本
    /// </summary>
    private void ModifyReaperSilk()
    {  // 添加全局开关检查
        if (!Plugin.IsReaperBalanceEnabled)
        {
            Log.Info("全局开关关闭，跳过ReaperSilk修改");
            return;
        }

        if (_assetManager == null)
        {
            Log.Error("AssetManager instance is null!");
            return;
        }

        var ReaperSilkBundle = _assetManager.Get<GameObject>("Reaper Silk Bundle");
        if (ReaperSilkBundle == null)
        {
            Log.Error("Reaper Silk Bundle not found!");
            return;
        }

        // 方法2：在预制体上添加组件，在实例化时动态修改
        var modifier = ReaperSilkBundle.GetComponent<ReaperSilkRangeModifier>();
        if (modifier == null)
        {
            modifier = ReaperSilkBundle.AddComponent<ReaperSilkRangeModifier>();
        }
        modifier.CollectRange = Plugin.CollectRange.Value;
        modifier.CollectMaxSpeed = Plugin.CollectMaxSpeed.Value;
        modifier.CollectAcceleration = Plugin.CollectAcceleration.Value;

        Log.Info("已为Reaper Silk Bundle添加范围修改组件");

        // 同时为已经存在的实例添加组件
        StartCoroutine(ModifyExistingBundles());
    }

    // 为已经存在的实例添加组件
    private IEnumerator ModifyExistingBundles()
    {
        // 等待一帧确保所有对象都已加载
        yield return null;

        Log.Info("开始查找并修改已存在的Reaper Silk Bundle实例");

        // 查找所有Reaper Silk Bundle实例
        var allBundles = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(obj => obj.name.Contains("Reaper Silk Bundle") && obj.scene.IsValid())
            .ToArray();

        Log.Info($"找到 {allBundles.Length} 个Reaper Silk Bundle实例");

        int modifiedCount = 0;
        foreach (var bundle in allBundles)
        {
            // 检查是否已经有组件
            var existingModifier = bundle.GetComponent<ReaperSilkRangeModifier>();
            if (existingModifier == null)
            {
                // 添加组件
                var modifier = bundle.AddComponent<ReaperSilkRangeModifier>();
                modifier.CollectRange = Plugin.CollectRange.Value;
                modifier.CollectMaxSpeed = Plugin.CollectMaxSpeed.Value;
                modifier.CollectAcceleration = Plugin.CollectAcceleration.Value;
                modifiedCount++;
                Log.Info($"为实例 {bundle.name} 添加范围修改组件");
            }
            else
            { // 修复：即使已有组件，也要更新范围值
                existingModifier.CollectRange = Plugin.CollectRange.Value;
                existingModifier.CollectMaxSpeed = Plugin.CollectMaxSpeed.Value;
                existingModifier.CollectAcceleration = Plugin.CollectAcceleration.Value;
                modifiedCount++;
                Log.Info($"实例 {bundle.name} 已有范围修改组件");
            }
        }

        Log.Info($"成功为 {modifiedCount} 个现有实例添加范围修改组件");
    }

    // 运行时修改组件
    private class ReaperSilkRangeModifier : MonoBehaviour
    {
        public float CollectRange = 8f;
        public float CollectMaxSpeed = 20f;
        public float CollectAcceleration = 800f;
        private bool _isModified = false;
        private RangeCollectAction _rangeAction = null;

        private void Start()
        {
            // 在Start中修改，确保FSM已初始化
            ModifyFSM();
        }

        private void Update()
        {
            // 如果还没有修改成功，继续尝试
            if (!_isModified)
            {
                ModifyFSM();
            }
            else if (_rangeAction != null)
            {
                // 修复：动态更新RangeCollectAction中的范围值
                _rangeAction.CollectRange = CollectRange;
                _rangeAction.MaxSpeed = CollectMaxSpeed;
                _rangeAction.Acceleration = CollectAcceleration;
            }
        }

        private void ModifyFSM()
        {
            var fsm = GetComponent<PlayMakerFSM>();
            if (fsm == null || fsm.Fsm == null)
                return;

            var collectableState = fsm.FsmStates.FirstOrDefault(state => state.Name == "Collectable");
            if (collectableState == null)
                return;

            var actionsList = collectableState.Actions.ToList();

            // 检查是否已经添加了范围检测动作
            bool hasRangeAction = actionsList.Any(action => action is RangeCollectAction);

            if (!hasRangeAction)
            {
                Log.Info("为Reaper Silk Bundle添加范围检测功能（保留原有碰撞检测）");

                // 新增范围检测动作，而不是替换
                _rangeAction = new RangeCollectAction
                {
                    Owner = gameObject,
                    Fsm = fsm,
                    CollectRange = CollectRange,
                    MaxSpeed = CollectMaxSpeed,
                    Acceleration = CollectAcceleration
                };

                actionsList.Add(_rangeAction);
                collectableState.Actions = actionsList.ToArray();
                _isModified = true;
                Log.Info("范围检测功能添加成功");
            }
            else
            {
                // 修复：如果已有动作，找到并更新它
                var existingAction = actionsList.FirstOrDefault(action => action is RangeCollectAction) as RangeCollectAction;
                if (existingAction != null)
                {
                    _rangeAction = existingAction;
                    _rangeAction.CollectRange = CollectRange;
                    _rangeAction.MaxSpeed = CollectMaxSpeed;
                    _rangeAction.Acceleration = CollectAcceleration;
                    _isModified = true;
                    Log.Info($"更新现有范围检测动作，范围: {CollectRange}");
                }
            }
        }
    }

    // 改进的范围检测动作 - 使用更平滑的物理移动
    private class RangeCollectAction : FsmStateAction
    {
        public GameObject Owner { get; set; }
        public PlayMakerFSM Fsm { get; set; }
        public float CollectRange { get; set; } = 8f;
        public float MaxSpeed { get; set; } = 20f;
        public float Acceleration { get; set; } = 800f;

        private GameObject _hero;
        private Rigidbody2D _rb;
        private bool _isInRange = false;
        private float _checkInterval = 0.1f;
        private float _lastCheckTime = 0f;

        public override void OnEnter()
        {
            _hero = HeroController.instance?.gameObject;
            if (_hero == null)
            {
                Finish();
                return;
            }

            // 获取Rigidbody2D组件
            _rb = Owner.GetComponent<Rigidbody2D>();
            if (_rb == null)
            {
                Source.Log.Warn("Reaper Silk Bundle没有Rigidbody2D组件，无法实现移动效果");
                Finish();
                return;
            }

            Source.Log.Info($"开始范围检测，吸收范围: {CollectRange}");
        }

        public override void OnUpdate()
        {
            if (_hero == null || Owner == null || Fsm == null || _rb == null)
            {
                Finish();
                return;
            }

            // 检查 EnableSilkAttraction 开关
            if (!Plugin.EnableSilkAttraction.Value)
            {
                // 开关关闭时，停止吸引效果
                if (_isInRange && _rb.linearVelocity.magnitude > 0.1f)
                {
                    _rb.linearVelocity = Vector2.zero;
                    _isInRange = false;
                }
                return;
            }

            // 限制检测频率
            if (Time.time - _lastCheckTime < _checkInterval)
                return;

            _lastCheckTime = Time.time;

            try
            {
                // 计算与英雄的距离
                float distance = Vector3.Distance(Owner.transform.position, _hero.transform.position);

                if (distance <= CollectRange)
                {
                    if (!_isInRange)
                    {
                        Source.Log.Info($"进入吸收范围，距离: {distance:F2}");
                        _isInRange = true;
                    }

                    // 计算朝向玩家的方向
                    Vector2 direction = (_hero.transform.position - Owner.transform.position).normalized;

                    // 使用更平滑的物理移动：AddForce方式
                    Vector2 currentVelocity = _rb.linearVelocity;

                    // 计算目标速度
                    Vector2 targetVelocity = direction * MaxSpeed;

                    // 计算需要的速度变化
                    Vector2 velocityChange = targetVelocity - currentVelocity;

                    // 限制加速度，确保不超过最大加速度
                    float maxAcceleration = Acceleration * Time.deltaTime;
                    if (velocityChange.magnitude > maxAcceleration)
                    {
                        velocityChange = velocityChange.normalized * maxAcceleration;
                    }

                    // 应用力
                    _rb.linearVelocity += velocityChange;

                    // 限制最大速度
                    if (_rb.linearVelocity.magnitude > MaxSpeed)
                    {
                        _rb.linearVelocity = _rb.linearVelocity.normalized * MaxSpeed;
                    }


                }
                else
                {
                    if (_isInRange)
                    {
                        Source.Log.Info("离开吸收范围");
                        _isInRange = false;

                        // 离开范围时逐渐减速 - 使用更平滑的减速
                        if (_rb.linearVelocity.magnitude > 0.1f)
                        {
                            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, 2f * Time.deltaTime);
                        }
                        else
                        {
                            _rb.linearVelocity = Vector2.zero;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Source.Log.Error($"范围检测过程中出错: {e}");
                Finish();
            }
        }

        public override void OnExit()
        {
            _hero = null;
            _rb = null;
            _isInRange = false;
        }
    }

    #endregion
    #region 重置修改
    /// <summary>
    /// 重置所有对FSM的修改
    /// </summary>
    /// <remarks>
    /// 调用此方法后，组件将恢复到未修改的状态
    /// </remarks>
    // 在 ChangeReaper.cs 中添加以下方法
    public void ResetModifications()
    {
        try
        {
            // 重置普攻伤害倍率
            ResetNormalAttackModifiers();
            // 重置其他修改
            ResetHeavyAttackModifiers();
            ResetReaperSilk();
            
            // 清空 stunDamage 基础值缓存
            _baseStunDamage.Clear();
            
            Log.Info("已重置所有Reaper修改");
        }
        catch (System.Exception e)
        {
            Log.Error($"重置修改时出错: {e}");
        }
    }

    private void ResetNormalAttackModifiers()
    {
        if (_heroController == null) return;

        var Attack = _heroController.gameObject.transform.Find("Attacks");
        if (Attack != null)
        {
            Transform Scythe = Attack.gameObject.transform.Find("Scythe");
            if (Scythe != null)
            {
                // 重置为默认倍率 (1.0f)
                ResetDamageMultiplierRecursive(Scythe, 1.0f);
            }
        }
    }

    private void ResetDamageMultiplierRecursive(Transform transform, float multiplier)
    {
        // 重置当前对象的DamageEnemies组件
        DamageEnemies damageEnemies = transform.GetComponent<DamageEnemies>();
        if (damageEnemies != null)
        {
            damageEnemies.damageMultiplier = multiplier;
            
            // 重置 stunDamage 为基础值
            ResetStunDamageToBase(damageEnemies);
            
            Log.Info($"重置 {transform.name} 的DamageEnemies: damageMultiplier={multiplier}, stunDamage={damageEnemies.stunDamage}");
        }

        // 递归处理所有子物体
        foreach (Transform child in transform)
        {
            ResetDamageMultiplierRecursive(child, multiplier);
        }
    }

    private void ResetHeavyAttackModifiers()
    {
        // 重置重攻击的修改
        if (_heroController == null) return;

        PlayMakerFSM nailArtsFSM = null;
        PlayMakerFSM[] fsms = _heroController.GetComponents<PlayMakerFSM>();
        foreach (var fsm in fsms)
        {
            if (fsm.FsmName == "Nail Arts")
            {
                nailArtsFSM = fsm;
                break;
            }
        }

        if (nailArtsFSM != null)
        {
            var doSlashState = nailArtsFSM.FsmStates.FirstOrDefault(s => s.Name == "Do Slash");
            if (doSlashState != null)
            {
                // 恢复原始动作
                if (_originalDoSlashActions != null)
                {
                    doSlashState.Actions = _originalDoSlashActions;
                }
                Log.Info("重置重攻击修改");
            }
        }
    }
    private void ResetReaperSilk()
    {
        if (_assetManager == null)
        {
            Log.Error("AssetManager instance is null!");
            return;
        }

        var ReaperSilkBundle = _assetManager.Get<GameObject>("Reaper Silk Bundle");
        if (ReaperSilkBundle == null)
        {
            Log.Error("Reaper Silk Bundle not found!");
            return;
        }

        Destroy(ReaperSilkBundle.GetComponent<ReaperSilkRangeModifier>());
        StartCoroutine(ResetExistingBundles());
    }
    private IEnumerator ResetExistingBundles()
    {
        yield return null;

        Log.Info("开始查找并修改已存在的Reaper Silk Bundle实例");

        // 查找所有Reaper Silk Bundle实例
        var allBundles = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(obj => obj.name.Contains("Reaper Silk Bundle") && obj.scene.IsValid())
            .ToArray();

        Log.Info($"找到 {allBundles.Length} 个Reaper Silk Bundle实例");

        int modifiedCount = 0;
        foreach (var bundle in allBundles)
        {
            Destroy(bundle.GetComponent<ReaperSilkRangeModifier>());
            modifiedCount++;
        }
    }
    #endregion
}