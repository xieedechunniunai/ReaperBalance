using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ReaperBalance.Source;

/// <summary>
/// 简化版资源管理器 - 从全局已加载的 AssetBundle 中获取资源
/// </summary>
internal sealed class AssetManager : MonoBehaviour
{
    #region Fields
    /// <summary>
    /// 需要加载的资源名称列表
    /// </summary>
    private static readonly string[] RequiredAssets = {
        "Song Knight CrossSlash Friendly",
        "Reaper Silk Bundle",
    };

    /// <summary>
    /// 已加载的全局资源缓存
    /// </summary>
    private readonly Dictionary<string, GameObject> _loadedAssets = new();

    /// <summary>
    /// AssetPool 中的缓存预制体
    /// </summary>
    private readonly Dictionary<string, GameObject> _cachedPrefabs = new();

    /// <summary>
    /// AssetPool GameObject
    /// </summary>
    private GameObject _assetPool = null!;

    private bool _initialized = false;
    private bool _initializing = false;
    #endregion

    #region Lifecycle
    private void Awake()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
        CreateAssetPool();
        StartCoroutine(Initialize());
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        // 返回主菜单时清理 AssetPool
        if (newScene.name == "Menu_Title")
        {
            CleanupAssetPool();
        }
    }
    #endregion

    #region AssetPool
    /// <summary>
    /// 创建 AssetPool 子物体
    /// </summary>
    private void CreateAssetPool()
    {
        var existing = transform.Find("AssetPool");
        if (existing != null)
        {
            _assetPool = existing.gameObject;
            return;
        }

        _assetPool = new GameObject("AssetPool");
        _assetPool.transform.SetParent(transform);
        _assetPool.SetActive(false);
        Log.Info("Created AssetPool");
    }

    /// <summary>
    /// 清理 AssetPool 中的所有子物体
    /// </summary>
    public void CleanupAssetPool()
    {
        if (_assetPool == null) return;

        // Ensure we can re-initialize cleanly next time (e.g. after returning to title).
        // Stop any ongoing initialization coroutine(s).
        StopAllCoroutines();
        _initializing = false;

        int count = _assetPool.transform.childCount;
        for (int i = count - 1; i >= 0; i--)
        {
            Destroy(_assetPool.transform.GetChild(i).gameObject);
        }

        _cachedPrefabs.Clear();
        _loadedAssets.Clear();
        _initialized = false;
        Log.Info($"Cleaned up {count} objects from AssetPool");
    }

    /// <summary>
    /// 存储预制体到 AssetPool
    /// </summary>
    public void StorePrefabInPool(string name, GameObject prefab)
    {
        if (_assetPool == null) return;

        prefab.transform.SetParent(_assetPool.transform);
        prefab.SetActive(false);
        _cachedPrefabs[name] = prefab;
        Log.Info($"Stored prefab '{name}' in AssetPool");
    }

    /// <summary>
    /// 从 AssetPool 获取缓存的预制体
    /// </summary>
    public GameObject GetCachedPrefab(string name)
    {
        return _cachedPrefabs.TryGetValue(name, out var prefab) ? prefab : null!;
    }

    /// <summary>
    /// 检查预制体是否已缓存
    /// </summary>
    public bool IsPrefabCached(string name)
    {
        return _cachedPrefabs.ContainsKey(name) && _cachedPrefabs[name] != null;
    }
    #endregion

    #region Global Asset Loading
    /// <summary>
    /// 初始化 - 从已加载的 AssetBundle 中查找资源
    /// </summary>
    public IEnumerator Initialize()
    {
        if (_initialized || _initializing) yield break;
        _initializing = true;

        Log.Info("AssetManager initializing...");

        // 等待一帧确保游戏 AssetBundle 已加载
        yield return null;

        // 从所有已加载的 AssetBundle 中查找需要的资源
        LoadAssetsFromBundles();

        _initialized = true;
        _initializing = false;
        Log.Info($"AssetManager initialized with {_loadedAssets.Count} assets");
    }

    /// <summary>
    /// 从已加载的 AssetBundle 中加载资源
    /// </summary>
    private void LoadAssetsFromBundles()
    {
        _loadedAssets.Clear();

        foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (bundle == null) continue;

            try
            {
                var assetPaths = bundle.GetAllAssetNames();
                if (assetPaths == null) continue;

                foreach (var path in assetPaths)
                {
                    string assetName = Path.GetFileNameWithoutExtension(path);

                    // 检查是否是我们需要的资源
                    foreach (var required in RequiredAssets)
                    {
                        if (assetName.Contains(required, StringComparison.OrdinalIgnoreCase))
                        {
                            var asset = bundle.LoadAsset<GameObject>(path);
                            if (asset != null && !_loadedAssets.ContainsKey(required))
                            {
                                _loadedAssets[required] = asset;
                                Log.Info($"Loaded asset: {required}");
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Error processing bundle: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 获取全局资源
    /// </summary>
    public T Get<T>(string assetName) where T : Object
    {
        // 先尝试从缓存获取
        if (_loadedAssets.TryGetValue(assetName, out var cached))
        {
            return cached as T;
        }

        // 如果没有，尝试重新加载
        Log.Warn($"Asset '{assetName}' not in cache, reloading...");
        LoadAssetsFromBundles();

        if (_loadedAssets.TryGetValue(assetName, out cached))
        {
            return cached as T;
        }

        Log.Error($"Asset '{assetName}' not found");
        return null!;
    }

    /// <summary>
    /// 获取所有已加载的资源名称
    /// </summary>
    public IEnumerable<string> GetAllAssetNames() => _loadedAssets.Keys;

    /// <summary>
    /// 检查是否已初始化
    /// </summary>
    public bool IsInitialized() => _initialized;
    #endregion
}
