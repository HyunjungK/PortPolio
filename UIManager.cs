using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 진입점. UI 루트 캔버스를 관리하고 팝업을 스택으로 열고 닫는다.
/// 프리팹은 CResourceManager 로 로드한다(기본 Resources 폴더 "UI/").
/// </summary>
public class UIManager : SingletonMono<UIManager>
{
        [SerializeField] private string _uiResourceFolder = "UI/Prefabs/";
    [SerializeField] private Vector2 _referenceResolution = new(1920, 1080);

    private Transform _uiRoot;
    // 생성된 UI 캐시 (키 -> 인스턴스). 닫아도 파괴하지 않고 재사용.
    private readonly Dictionary<string, UIBase> _cache = new();
    // 현재 열려 있는 팝업 스택 (마지막이 최상단).
    private readonly List<UIPopup> _popupStack = new();
    // 현재 씬에서 상주하는 페이지 목록.
    private readonly List<UIPage> _pages = new();

    public bool IsAnyPopupOpen => _popupStack.Count > 0;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        EnsureRoot();
    }

    private void OnEnable()
    {
       // CInputManager.Instance.OnCancelPressed += OnCancelPressed;
    }

    private void OnDisable()
    {
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            OnCancelPressed();
    }

    private void OnCancelPressed()
    {
        if (_popupStack.Count > 0)
        {
            CloseTopPopup();
            return;
        }

        OpenPopup<PopupExit>();
    }

    #region 페이지 열기/등록

    /// <summary>씬에 배치된 페이지를 등록하고 항상 표시한다.</summary>
    public void RegisterPage(UIPage page)
    {
        if (page == null)
            return;

        if (!_pages.Contains(page))
            _pages.Add(page);

        if (page.transform.parent != _uiRoot)
            page.transform.SetParent(_uiRoot, false);

        page.Open();
    }

    /// <summary>페이지 프리팹을 생성해 현재 씬의 상주 페이지로 연다.</summary>
    public T OpenPage<T>(string key = null) where T : UIPage
    {
        T page = GetOrCreate<T>(key ?? typeof(T).Name);
        if (page == null)
            return null;

        if (!_pages.Contains(page))
            _pages.Add(page);

        page.transform.SetAsLastSibling();
        page.Open();
        return page;
    }

    public void ClosePage(UIPage page)
    {
        if (page == null)
            return;

        _pages.Remove(page);
        page.Close();
    }

    #endregion

    #region 팝업 열기/닫기

    /// <summary>팝업을 열고 스택 최상단에 올린다. key 생략 시 타입 이름을 사용.</summary>
    public T OpenPopup<T>(string key = null) where T : UIPopup
    {
        T popup = GetOrCreate<T>(key ?? typeof(T).Name);
        if (popup == null)
            return null;

        PushAndOpen(popup);
        return popup;
    }

    /// <summary>Addressable UI 프리팹을 비동기로 로드해 팝업을 연다. key 는 Addressable 주소.</summary>
    public void OpenPopupAsync<T>(string key = null, Action<T> onOpened = null) where T : UIPopup
    {
        GetOrCreateAsync<T>(key ?? typeof(T).Name, popup =>
        {
            if (popup != null)
                PushAndOpen(popup);

            onOpened?.Invoke(popup);
        });
    }

    private void PushAndOpen(UIPopup popup)
    {
        if (!_popupStack.Contains(popup))
            _popupStack.Add(popup);

        popup.transform.SetAsLastSibling();
        popup.Open();
    }

    public void ClosePopup(UIPopup popup)
    {
        if (popup == null)
            return;

        _popupStack.Remove(popup);
        popup.Close();
    }

    public void CloseTopPopup()
    {
        if (_popupStack.Count == 0)
            return;

        ClosePopup(_popupStack[_popupStack.Count - 1]);
    }

    public void CloseAllPopups()
    {
        for (int i = _popupStack.Count - 1; i >= 0; i--)
            _popupStack[i].Close();

        _popupStack.Clear();
    }

    #endregion

    #region 일반 UI (스택 미사용)

    /// <summary>HUD 등 스택에 쌓이지 않는 UI 를 연다.</summary>
    public T Open<T>(string key = null) where T : UIBase
    {
        T ui = GetOrCreate<T>(key ?? typeof(T).Name);
        ui?.Open();
        return ui;
    }

    /// <summary>Addressable UI 프리팹을 비동기로 로드해 연다. key 는 Addressable 주소.</summary>
    public void OpenAsync<T>(string key = null, Action<T> onOpened = null) where T : UIBase
    {
        GetOrCreateAsync<T>(key ?? typeof(T).Name, ui =>
        {
            ui?.Open();
            onOpened?.Invoke(ui);
        });
    }

    public void Close<T>(string key = null) where T : UIBase
    {
        if (_cache.TryGetValue(key ?? typeof(T).Name, out var ui) && ui != null)
            ui.Close();
    }

    #endregion

    #region 내부

    private T GetOrCreate<T>(string key) where T : UIBase
    {
        if (_cache.TryGetValue(key, out var existing) && existing != null)
            return existing as T;

        GameObject go = CResourceManager.Instance.Instantiate(_uiResourceFolder + key, _uiRoot);
        return Setup<T>(go, _uiResourceFolder + key, key);
    }

    /// <summary>Addressable 주소로 UI 프리팹을 비동기 로드 후 인스턴스화한다.</summary>
    private void GetOrCreateAsync<T>(string key, Action<T> onReady) where T : UIBase
    {
        if (_cache.TryGetValue(key, out var existing) && existing != null)
        {
            onReady?.Invoke(existing as T);
            return;
        }

        CResourceManager.Instance.LoadFromAddressable<GameObject>(key, prefab =>
        {
            GameObject go = prefab != null ? Instantiate(prefab, _uiRoot) : null;
            onReady?.Invoke(Setup<T>(go, key, key));
        });
    }

    private T Setup<T>(GameObject go, string loadPath, string cacheKey) where T : UIBase
    {
        if (go == null)
        {
            Debug.LogError($"[UIManager] UI 프리팹 로드 실패: {loadPath}");
            return null;
        }

        T ui = go.GetComponent<T>();
        if (ui == null)
        {
            Debug.LogError($"[UIManager] {typeof(T).Name} 컴포넌트를 찾을 수 없음: {cacheKey}");
            Destroy(go);
            return null;
        }

        ui.Init();
        ui.gameObject.SetActive(false);
        _cache[cacheKey] = ui;
        return ui;
    }

    private void EnsureRoot()
    {
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _referenceResolution;
        }

        _uiRoot = canvas.transform;
        EnsureEventSystem();
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.transform.SetParent(transform, false);

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    #endregion
}
