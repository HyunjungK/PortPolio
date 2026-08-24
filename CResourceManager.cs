using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
 using UnityEngine.AddressableAssets;
 using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

/// <summary>
/// 리소스 로드 진입점. Resources / AssetBundle / Addressables 를 하나의 API 로 다루고
/// SpriteAtlas 에서 개별 스프라이트를 뽑는 것까지 지원하는 베이스 매니저.
/// </summary>
public class CResourceManager : SingletonMono<CResourceManager>
{
    /// <summary>리소스를 어디서 가져올지 결정하는 로드 소스.</summary>
    public enum ELoadSource
    {
        Resources,
        AssetBundle,
        Addressable,
    }

    [SerializeField] private ELoadSource _defaultSource = ELoadSource.Resources;

    // 로드된 에셋 캐시 (같은 키 재요청 시 재사용)
    private readonly Dictionary<string, Object> _assetCache = new();
    // 로드된 에셋번들 캐시
    private readonly Dictionary<string, AssetBundle> _bundleCache = new();
    // 등록된 SpriteAtlas 캐시 (아틀라스명 -> 아틀라스)
    private readonly Dictionary<string, SpriteAtlas> _atlasCache = new();

    // Addressable 핸들 추적 (Release 시 반환용)
    private readonly Dictionary<string, AsyncOperationHandle> _handleCache = new();

    #region 동기 로드 (Resources 전용)

    /// <summary>
    /// Resources 폴더에서 동기 로드한다. (AssetBundle/Addressable 은 비동기 API 사용)
    /// </summary>
    public T Load<T>(string key) where T : Object
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (_assetCache.TryGetValue(key, out var cached))
            return cached as T;

        T asset = Resources.Load<T>(key);
        if (asset == null)
        {
            Debug.LogWarning($"[CResourceManager] Resources 로드 실패: {key}");
            return null;
        }

        _assetCache[key] = asset;
        return asset;
    }

    #endregion

    #region 비동기 로드 (소스 통합 진입점)

    /// <summary>
    /// 설정된 기본 소스로 비동기 로드한다.
    /// </summary>
    public void LoadAsync<T>(string key, Action<T> onLoaded) where T : Object
    {
        LoadAsync(key, _defaultSource, onLoaded);
    }

    /// <summary>
    /// 지정한 소스로 비동기 로드한다.
    /// </summary>
    public void LoadAsync<T>(string key, ELoadSource source, Action<T> onLoaded) where T : Object
    {
        if (string.IsNullOrEmpty(key))
        {
            onLoaded?.Invoke(null);
            return;
        }

        if (_assetCache.TryGetValue(key, out var cached))
        {
            onLoaded?.Invoke(cached as T);
            return;
        }

        switch (source)
        {
            case ELoadSource.Resources:
                StartCoroutine(LoadFromResourcesAsync(key, onLoaded));
                break;
            case ELoadSource.AssetBundle:
                Debug.LogError($"[CResourceManager] AssetBundle 로드는 LoadFromBundle 을 사용하세요: {key}");
                onLoaded?.Invoke(null);
                break;
            case ELoadSource.Addressable:
                LoadFromAddressable(key, onLoaded);
                break;
        }
    }

    private IEnumerator LoadFromResourcesAsync<T>(string key, Action<T> onLoaded) where T : Object
    {
        ResourceRequest request = Resources.LoadAsync<T>(key);
        yield return request;

        T asset = request.asset as T;
        if (asset != null)
            _assetCache[key] = asset;
        else
            Debug.LogWarning($"[CResourceManager] Resources 비동기 로드 실패: {key}");

        onLoaded?.Invoke(asset);
    }

    #endregion

    #region AssetBundle

    /// <summary>
    /// 로컬 경로 또는 URL 에서 에셋번들을 로드한다.
    /// </summary>
    public IEnumerator LoadBundleAsync(string bundleName, string pathOrUrl, Action<AssetBundle> onLoaded = null)
    {
        if (_bundleCache.TryGetValue(bundleName, out var cached))
        {
            onLoaded?.Invoke(cached);
            yield break;
        }

        AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(pathOrUrl);
        yield return request;

        AssetBundle bundle = request.assetBundle;
        if (bundle == null)
        {
            Debug.LogError($"[CResourceManager] AssetBundle 로드 실패: {bundleName} ({pathOrUrl})");
            onLoaded?.Invoke(null);
            yield break;
        }

        _bundleCache[bundleName] = bundle;
        onLoaded?.Invoke(bundle);
    }

    /// <summary>
    /// 이미 로드된 번들에서 에셋을 비동기로 꺼낸다.
    /// </summary>
    public IEnumerator LoadFromBundle<T>(string bundleName, string assetName, Action<T> onLoaded) where T : Object
    {
        if (!_bundleCache.TryGetValue(bundleName, out var bundle) || bundle == null)
        {
            Debug.LogError($"[CResourceManager] 번들이 로드되지 않음: {bundleName}");
            onLoaded?.Invoke(null);
            yield break;
        }

        string cacheKey = $"{bundleName}:{assetName}";
        if (_assetCache.TryGetValue(cacheKey, out var cached))
        {
            onLoaded?.Invoke(cached as T);
            yield break;
        }

        AssetBundleRequest request = bundle.LoadAssetAsync<T>(assetName);
        yield return request;

        T asset = request.asset as T;
        if (asset != null)
            _assetCache[cacheKey] = asset;
        else
            Debug.LogWarning($"[CResourceManager] 번들 에셋 로드 실패: {bundleName}/{assetName}");

        onLoaded?.Invoke(asset);
    }

    /// <summary>번들 언로드. unloadAllLoadedObjects=true 면 로드된 인스턴스까지 파괴.</summary>
    public void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
    {
        if (_bundleCache.TryGetValue(bundleName, out var bundle) && bundle != null)
        {
            bundle.Unload(unloadAllLoadedObjects);
            _bundleCache.Remove(bundleName);
        }
    }

    #endregion

    #region Addressables

    /// <summary>
    /// Addressable 주소로 비동기 로드한다.
    /// </summary>
    public void LoadFromAddressable<T>(string address, Action<T> onLoaded) where T : Object
    {
        if (_handleCache.TryGetValue(address, out var existing))
        {
            onLoaded?.Invoke(existing.Result as T);
            return;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
        handle.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                _assetCache[address] = op.Result;
                _handleCache[address] = handle;
                onLoaded?.Invoke(op.Result);
            }
            else
            {
                Debug.LogError($"[CResourceManager] Addressable 로드 실패: {address}");
                onLoaded?.Invoke(null);
            }
        };
    }

    /// <summary>Addressable 핸들 반환.</summary>
    public void ReleaseAddressable(string address)
    {
        if (_handleCache.TryGetValue(address, out var handle))
        {
            Addressables.Release(handle);
            _handleCache.Remove(address);
            _assetCache.Remove(address);
        }
    }

    #endregion

    #region SpriteAtlas

    /// <summary>
    /// SpriteAtlas 를 이름으로 등록해 캐싱한다. (Resources/번들/Addressable 어디서 로드했든 등록 가능)
    /// </summary>
    public void RegisterAtlas(string atlasName, SpriteAtlas atlas)
    {
        if (atlas == null || string.IsNullOrEmpty(atlasName))
            return;

        _atlasCache[atlasName] = atlas;
    }

    /// <summary>
    /// 스프라이트를 로드한다.
    /// key 가 "atlasName/spriteName" 형식이면 등록된 아틀라스에서 꺼내고,
    /// 아니면 일반 스프라이트로 로드한다.
    /// </summary>
    public Sprite LoadSprite(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        int slash = key.IndexOf('/');
        if (slash > 0)
        {
            string atlasName = key.Substring(0, slash);
            string spriteName = key.Substring(slash + 1);
            return GetSpriteFromAtlas($"Atlas/{atlasName}", spriteName);
        }

        return Load<Sprite>(key);
    }

    /// <summary>
    /// 등록된 SpriteAtlas 에서 개별 스프라이트를 꺼낸다.
    /// 아틀라스가 아직 등록돼 있지 않으면 Resources 에서 로드 후 등록을 시도한다.
    /// </summary>
    public Sprite GetSpriteFromAtlas(string atlasName, string spriteName)
    {
        if (!_atlasCache.TryGetValue(atlasName, out var atlas) || atlas == null)
        {
            atlas = Resources.Load<SpriteAtlas>(atlasName);
            if (atlas == null)
            {
                Debug.LogWarning($"[CResourceManager] SpriteAtlas 를 찾을 수 없음: {atlasName}");
                return null;
            }

            _atlasCache[atlasName] = atlas;
        }

        Sprite sprite = atlas.GetSprite(spriteName);
        if (sprite == null)
            Debug.LogWarning($"[CResourceManager] 아틀라스에 스프라이트 없음: {atlasName}/{spriteName}");

        return sprite;
    }

    #endregion

    #region Instantiate / Release

    /// <summary>
    /// 프리팹을 로드해 인스턴스화한다. (동기, Resources 기준)
    /// </summary>
    public GameObject Instantiate(string key, Transform parent = null)
    {
        GameObject prefab = Load<GameObject>(key);
        if (prefab == null)
            return null;

        return Instantiate(prefab, parent);
    }

    public GameObject Instantiate(GameObject prefab, Transform parent = null)
    {
        if (prefab == null)
            return null;

        return Object.Instantiate(prefab, parent);
    }

    public void Release(GameObject instance)
    {
        if (instance != null)
            Object.Destroy(instance);
    }

    #endregion

    #region 정리

    /// <summary>캐시 및 번들을 모두 정리한다.</summary>
    public void Clear()
    {
        _assetCache.Clear();
        _atlasCache.Clear();

        foreach (var bundle in _bundleCache.Values)
        {
            if (bundle != null)
                bundle.Unload(false);
        }
        _bundleCache.Clear();

        foreach (var handle in _handleCache.Values)
            Addressables.Release(handle);
        _handleCache.Clear();

        Resources.UnloadUnusedAssets();
    }

    protected override void OnDestroy()
    {
        Clear();
        base.OnDestroy();
    }

    #endregion
}
