using System.Collections;
using System.Linq;
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
        Log.Info($"应用ReaperBalance更新: {reason}");
        ForceUpdateConfig();
    }
    /// <summary>
    /// 强制更新所有配置
    /// </summary>
    public void ForceUpdateConfig()
    {
        if (!_isInitialized) return;

        Log.Info("强制更新所有ReaperBalance配置");

        // 重置配置监听值，强制更新
        _lastCrossSlashScale = -1f;
        _lastCollectRange = -1f;
        _lastDamageMultiplier = -1f;

        // 从AssetPool获取缓存的预制体
        var cachedPrefab = _assetManager.GetCachedPrefab(CROSS_SLASH_PREFAB_NAME);
        if (cachedPrefab != null)
        {
            UpdatePrefabScale(cachedPrefab);
            UpdatePrefabDamage(cachedPrefab);
        }

        // 更新普通攻击倍率
        ModifyReaperNormal();

        // 更新ReaperSilk范围
        UpdateReaperSilkRange();

        _lastCrossSlashScale = Plugin.CrossSlashScale.Value;
        _lastCollectRange = Plugin.CollectRange.Value;
        _lastDamageMultiplier = Plugin.DamageMultiplier.Value;
        _lastNailUpgrades = GetCurrentNailUpgrades();

        Log.Info("强制配置更新完成");
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

    // 响应式伤害计算相关
    private int _lastNailUpgrades = -1;
    // 配置监听相关
    private float _lastCrossSlashScale = -1f;
    private float _lastCollectRange = -1f;
    private float _lastDamageMultiplier = -1f;
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
        // CleanupDuplicateBundles();
        Log.Info("ChangeReaper singleton component initialized successfully");
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
                Log.Info("CrossSlash prefab already cached in AssetPool, skipping preload");
                yield break;
            }
        }

        Log.Info("Preloading and caching CrossSlash prefab to AssetPool...");

        // 调试所有可用的资源
        DebugAvailableAssets();

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

        // 修改预制体的DamageEnemies组件 - 确保 IsUsingNeedleDamageMult = true
        ModifyPrefabDamageEnemiesComponents(cachedPrefab);

        // 存储到AssetPool（会自动设置parent并保持inactive）
        _assetManager.StorePrefabInPool(CROSS_SLASH_PREFAB_NAME, cachedPrefab);

        _lastCrossSlashScale = Plugin.CrossSlashScale.Value;
        _lastDamageMultiplier = Plugin.DamageMultiplier.Value;

        Log.Info("CrossSlash prefab cached to AssetPool and modified successfully");
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

        Log.Info($"Spawned Song Knight CrossSlash from AssetPool at hero position: {heroPosition}");

        // 实例已经包含了修改后的组件（包括 IsUsingNeedleDamageMult = true），无需再次修改
        Log.Debug("CrossSlash instance spawned with pre-modified components (IsUsingNeedleDamageMult = true)");
    }
    /// <summary>
    /// 响应式配置更新
    /// </summary>
    private void UpdateConfigIfNeeded()
    {
        if (!Plugin.IsReaperBalanceEnabled)
            return;

        var cachedPrefab = _assetManager?.GetCachedPrefab(CROSS_SLASH_PREFAB_NAME);

        // 检查十字斩缩放大小是否变化
        if (Plugin.CrossSlashScale.Value != _lastCrossSlashScale && cachedPrefab != null)
        {
            _lastCrossSlashScale = Plugin.CrossSlashScale.Value;
            UpdatePrefabScale(cachedPrefab);
            Log.Info($"响应式更新十字斩缩放大小: {_lastCrossSlashScale}");
        }

        // 检查伤害倍率是否变化
        if (Plugin.DamageMultiplier.Value != _lastDamageMultiplier && cachedPrefab != null)
        {
            _lastDamageMultiplier = Plugin.DamageMultiplier.Value;
            UpdatePrefabDamage(cachedPrefab);
            Log.Info($"响应式更新伤害倍率: {_lastDamageMultiplier}");
        }

        // 检查吸收范围是否变化
        if (Plugin.CollectRange.Value != _lastCollectRange)
        {
            _lastCollectRange = Plugin.CollectRange.Value;
            UpdateReaperSilkRange();
            Log.Info($"响应式更新吸收范围: {_lastCollectRange}");
        }
    }

    /// <summary>
    /// 响应式更新伤害值
    /// </summary>
    private void UpdateDamageIfNeeded()
    {
        if (!Plugin.IsReaperBalanceEnabled)
            return;

        int currentNailUpgrades = GetCurrentNailUpgrades();
        if (currentNailUpgrades < 0)
            return;

        var cachedPrefab = _assetManager?.GetCachedPrefab(CROSS_SLASH_PREFAB_NAME);

        // 如果nailUpgrades发生变化，更新预制体伤害
        if (currentNailUpgrades != _lastNailUpgrades && cachedPrefab != null)
        {
            _lastNailUpgrades = currentNailUpgrades;
            UpdatePrefabDamage(cachedPrefab);
            Log.Info($"响应式更新伤害值：nailUpgrades = {currentNailUpgrades}");
        }
    }

    private int GetCurrentNailUpgrades()
    {
        if (GameManager.instance == null || GameManager.instance.playerData == null)
        {
            return -1;
        }

        return GameManager.instance.playerData.nailUpgrades;
    }
    /// <summary>
    /// 更新预制体缩放大小
    /// </summary>
    private void UpdatePrefabScale(GameObject prefab)
    {
        if (prefab != null)
        {
            prefab.transform.localScale = Vector3.one * Plugin.CrossSlashScale.Value;
            Log.Info($"更新预制体缩放大小: {Plugin.CrossSlashScale.Value}");
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
                updatedCount++;
            }
        }
    }
    /// <summary>
    /// 更新预制体伤害值
    /// </summary>
    private void UpdatePrefabDamage(GameObject prefab)
    {
        try
        {
            // 查找Damager1和Damager2子对象
            Transform damager1 = prefab.transform.Find("Damager1");
            Transform damager2 = prefab.transform.Find("Damager2");

            if (damager1 != null)
            {
                UpdateSingleDamage(damager1.gameObject, "Damager1");
            }

            if (damager2 != null)
            {
                UpdateSingleDamage(damager2.gameObject, "Damager2");
            }

            Log.Info("响应式伤害更新完成");
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

        Log.Info($"Hero localScale: {heroTransform.localScale}, scaleX: {heroScaleX}");

        if (heroScaleX < 0) // 英雄朝右
        {
            Log.Info("Hero facing right, rotating CrossSlash to face right");
            // 英雄朝右时，让CrossSlash也朝右
            // 通常需要绕Y轴旋转180度来面向右侧
            return Quaternion.Euler(0, 180, 0);
        }
        else if (heroScaleX > 0) // 英雄朝左
        {
            Log.Info("Hero facing left, using default rotation (facing left)");
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
    /// 修改预制体中的DamageEnemies组件（在实例化之前）
    /// </summary>
    private void ModifyPrefabDamageEnemiesComponents(GameObject prefab)
    {
        try
        {
            // 查找Damager1和Damager2子对象
            Transform damager1 = prefab.transform.Find("Damager1");
            Transform damager2 = prefab.transform.Find("Damager2");

            if (damager1 != null)
            {
                ModifySingleDamageEnemies(damager1.gameObject, "Damager1");
            }
            else
            {
                Log.Warn("Damager1 child object not found in CrossSlash prefab");
            }

            if (damager2 != null)
            {
                ModifySingleDamageEnemies(damager2.gameObject, "Damager2");
            }
            else
            {
                Log.Warn("Damager2 child object not found in CrossSlash prefab");
            }

            Log.Info("Successfully modified DamageEnemies components in prefab");
        }
        catch (System.Exception e)
        {
            Log.Error($"Failed to modify DamageEnemies components in prefab: {e}");
        }
    }

    /// <summary>
    /// 修改单个GameObject中的DamageEnemies组件
    /// 关键：确保 hitInstance.IsUsingNeedleDamageMult = true
    /// </summary>
    private void ModifySingleDamageEnemies(GameObject damagerObject, string damagerName)
    {
        // 获取DamageEnemies组件
        var damageEnemies = damagerObject.GetComponent<DamageEnemies>();
        if (damageEnemies == null)
        {
            Log.Warn($"DamageEnemies component not found on {damagerName}");
            return;
        }

        // 关键设置：确保 IsUsingNeedleDamageMult = true
        // 在 DamageEnemies.DoDamage() 中: IsUsingNeedleDamageMult = (this.useNailDamage || this.useHeroDamageAffectors)
        damageEnemies.useNailDamage = true;           // 启用钉子伤害计算
        damageEnemies.useHeroDamageAffectors = true;  // 启用英雄伤害影响因子
        damageEnemies.isHeroDamage = true;            // 标记为英雄伤害
        Log.Info($"Enabled useNailDamage + useHeroDamageAffectors on {damagerName} (IsUsingNeedleDamageMult = true)");

        // 修改damageAsset的IntReference.Value
        if (damageEnemies.damageAsset != null)
        {
            // 计算伤害值：12 + nailUpgrades * 9
            int nailUpgrades = 0;
            if (GameManager.instance != null && GameManager.instance.playerData != null)
            {
                nailUpgrades = GameManager.instance.playerData.nailUpgrades;
            }

            int calculatedDamage = 12 + nailUpgrades * 9;

            // 直接设置value字段
            damageEnemies.damageAsset.value = calculatedDamage;
            Log.Info($"Set {damagerName} damage to {calculatedDamage} (nailUpgrades: {nailUpgrades})");
        }
        else
        {
            Log.Warn($"damageAsset is null on {damagerName}");
        }

        // 修改attackType为Nail
        damageEnemies.attackType = AttackTypes.Nail;
        Log.Info($"Set {damagerName} attackType to Nail");
    }
    /// <summary>
    /// 更新单个GameObject的伤害值
    /// </summary>
    private void UpdateSingleDamage(GameObject damagerObject, string damagerName)
    {
        var damageEnemies = damagerObject.GetComponent<DamageEnemies>();
        if (damageEnemies == null || damageEnemies.damageAsset == null)
            return;

        // 计算伤害值：12 + nailUpgrades * 9
        int nailUpgrades = 0;
        if (GameManager.instance != null && GameManager.instance.playerData != null)
        {

            nailUpgrades = GameManager.instance.playerData.nailUpgrades;
        }

        float baseDamage = 12f + (float)nailUpgrades * 9f;
        float calculatedDamage = baseDamage * Plugin.DamageMultiplier.Value; // 使用配置值
                                                                             // 修复：同时更新damageAsset.value和damageDealt
        damageEnemies.damageAsset.value = Mathf.RoundToInt(calculatedDamage);
        damageEnemies.damageDealt = Mathf.RoundToInt(calculatedDamage);
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

    private void DebugAvailableAssets()
    {
        Log.Info("=== Available Assets ===");
        var allAssetNames = _assetManager.GetAllAssetNames();
        foreach (var assetName in allAssetNames)
        {
            Log.Info($"Asset: {assetName}");

            // 尝试获取这个资源来检查类型
            var asset = _assetManager.Get<Object>(assetName);
            if (asset != null)
            {
                Log.Info($"  Type: {asset.GetType().Name}, Name: {asset.name}");
            }
        }
        Log.Info("=== End of Available Assets ===");
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
            Log.Info("全局开关关闭，跳过HeavyAttack修改");
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
                Log.Info("找到Do Slash状态 - 这很可能是真正的攻击释放点");
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
                        Log.Info($"修改 {DownSlash.name} 的DamageEnemies.damageMultiplier为 {damageEnemies.damageMultiplier}");
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
    /// 递归修改Transform及其所有子物体的DamageEnemies组件的damageMultiplier值
    /// </summary>
    /// <param name="transform">要处理的Transform</param>
    /// <param name="multiplier">要设置的乘数值</param>
    private void ModifyDamageMultiplierRecursive(Transform transform, float multiplier)
    {
        // 修改当前对象的DamageEnemies组件
        DamageEnemies damageEnemies = transform.GetComponent<DamageEnemies>();
        if (damageEnemies != null && transform.name != "DownSlash New")
        {
            damageEnemies.damageMultiplier = multiplier;
            Log.Info($"修改 {transform.name} 的DamageEnemies.damageMultiplier为 {multiplier}");
        }

        // 递归处理所有子物体
        foreach (Transform child in transform)
        {
            ModifyDamageMultiplierRecursive(child, multiplier);
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
                modifiedCount++;
                Log.Info($"为实例 {bundle.name} 添加范围修改组件");
            }
            else
            { // 修复：即使已有组件，也要更新范围值
                existingModifier.CollectRange = Plugin.CollectRange.Value;
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
                    CollectRange = CollectRange
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

        private GameObject _hero;
        private Rigidbody2D _rb;
        private bool _isInRange = false;
        private float _checkInterval = 0.1f;
        private float _lastCheckTime = 0f;
        private float _maxSpeed = 20f;
        private float _acceleration = 800f;

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
                    Vector2 targetVelocity = direction * _maxSpeed;

                    // 计算需要的速度变化
                    Vector2 velocityChange = targetVelocity - currentVelocity;

                    // 限制加速度，确保不超过最大加速度
                    float maxAcceleration = _acceleration * Time.deltaTime;
                    if (velocityChange.magnitude > maxAcceleration)
                    {
                        velocityChange = velocityChange.normalized * maxAcceleration;
                    }

                    // 应用力
                    _rb.linearVelocity += velocityChange;

                    // 限制最大速度
                    if (_rb.linearVelocity.magnitude > _maxSpeed)
                    {
                        _rb.linearVelocity = _rb.linearVelocity.normalized * _maxSpeed;
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
            Log.Info($"重置 {transform.name} 的DamageEnemies.damageMultiplier为 {multiplier}");
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