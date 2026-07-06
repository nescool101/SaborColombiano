using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SaborColombiano.Core;
using UInput = UnityEngine.Input;

namespace SaborColombiano.UI
{
    /// <summary>
    /// Identifies each UI panel that <see cref="UIManager"/> can show or hide.
    /// </summary>
    public enum PanelType
    {
        /// <summary>No panel (used as a default / null value).</summary>
        None,
        /// <summary>The item and furniture shop.</summary>
        Shop,
        /// <summary>The recipe book and active menu editor.</summary>
        Menu,
        /// <summary>The staff hiring and management panel.</summary>
        Staff,
        /// <summary>Game settings (audio, graphics, language).</summary>
        Settings,
        /// <summary>Pause / quit overlay.</summary>
        Pause
    }

    /// <summary>
    /// Central singleton that owns every major UI panel in Sabor Colombiano.
    /// Provides show/hide/toggle helpers, a stack-based navigation history for
    /// the Android back button, an overlay dimming layer, and coroutine-driven
    /// slide-in/out animations (no external tweening dependency).
    /// <para>
    /// The HUD panel is always visible during gameplay and is <b>not</b> part of
    /// the panel stack. Only overlay panels (Shop, Menu, Staff, Settings, Pause)
    /// participate in the show/hide system.
    /// </para>
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Singleton
        // ------------------------------------------------------------------ //

        private static UIManager _instance;

        /// <summary>
        /// Global access point for the UIManager singleton.
        /// Returns <c>null</c> if no instance exists in the scene.
        /// </summary>
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UIManager>();
                    if (_instance == null)
                    {
                        Debug.LogError("[UIManager] No UIManager instance found in the scene.");
                    }
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Raised after a panel finishes its open animation and becomes fully visible.
        /// Subscribers receive the <see cref="PanelType"/> that was opened.
        /// </summary>
        public event Action<PanelType> OnPanelOpened;

        /// <summary>
        /// Raised after a panel finishes its close animation and is deactivated.
        /// Subscribers receive the <see cref="PanelType"/> that was closed.
        /// </summary>
        public event Action<PanelType> OnPanelClosed;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("Panel References")]

        [SerializeField]
        [Tooltip("The always-visible gameplay HUD (not managed by the panel stack).")]
        private GameObject _hudPanel;

        [SerializeField]
        [Tooltip("The shop / store panel root.")]
        private GameObject _shopPanel;

        [SerializeField]
        [Tooltip("The recipe book / menu management panel root.")]
        private GameObject _menuPanel;

        [SerializeField]
        [Tooltip("The staff management panel root.")]
        private GameObject _staffPanel;

        [SerializeField]
        [Tooltip("The settings panel root.")]
        private GameObject _settingsPanel;

        [SerializeField]
        [Tooltip("The pause / quit overlay panel root.")]
        private GameObject _pausePanel;

        [Header("Overlay")]

        [SerializeField]
        [Tooltip("Semi-transparent dark overlay shown behind open panels.")]
        private GameObject _overlayDimmer;

        [SerializeField]
        [Tooltip("Target alpha for the overlay dimmer when a panel is open.")]
        [Range(0f, 1f)]
        private float _overlayTargetAlpha = 0.6f;

        [Header("Animation")]

        [SerializeField]
        [Tooltip("Duration in seconds for panel slide-in / slide-out animations.")]
        [Range(0.05f, 1f)]
        private float _animationDuration = 0.25f;

        [SerializeField]
        [Tooltip("Slide offset in local-space units applied when a panel enters/exits.")]
        private Vector2 _slideOffset = new Vector2(0f, -600f);

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        /// <summary>Maps each panel type to its root GameObject for quick lookup.</summary>
        private Dictionary<PanelType, GameObject> _panelMap;

        /// <summary>
        /// Stack of panels currently open, most recent on top. Only the top
        /// panel is visible; the rest remain logically "in the stack" but could
        /// be hidden if desired.
        /// </summary>
        private readonly Stack<PanelType> _panelHistory = new Stack<PanelType>();

        /// <summary>Tracks running animation coroutines so we can cancel them.</summary>
        private readonly Dictionary<PanelType, Coroutine> _activeAnimations =
            new Dictionary<PanelType, Coroutine>();

        /// <summary>Cached Image component on the overlay dimmer for alpha fading.</summary>
        private Image _overlayImage;

        /// <summary>Coroutine handle for the overlay fade.</summary>
        private Coroutine _overlayFadeCoroutine;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            // Enforce singleton.
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[UIManager] Duplicate UIManager destroyed.", gameObject);
                Destroy(gameObject);
                return;
            }

            _instance = this;

            BuildPanelMap();

            if (_overlayDimmer != null)
            {
                _overlayImage = _overlayDimmer.GetComponent<Image>();
            }

            // Ensure everything starts hidden.
            HideAllPanelsImmediate();
        }

        private void Update()
        {
            HandleAndroidBackButton();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens the requested panel with a slide-in animation, pushing it
        /// onto the navigation stack.  If the panel is already the topmost
        /// panel, this call is ignored.
        /// </summary>
        /// <param name="panel">The panel to open.</param>
        public void ShowPanel(PanelType panel)
        {
            if (panel == PanelType.None)
                return;

            // Already on top.
            if (_panelHistory.Count > 0 && _panelHistory.Peek() == panel)
                return;

            // If a different panel is on top, hide it first (without removing
            // from the stack -- we just visually hide it).
            if (_panelHistory.Count > 0)
            {
                PanelType current = _panelHistory.Peek();
                SetPanelVisible(current, false);
            }

            _panelHistory.Push(panel);
            SetPanelVisible(panel, true);
            ShowOverlay(true);

            // Notify the GameManager that a menu is open.
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing &&
                panel != PanelType.Pause)
            {
                GameManager.Instance.SetGameState(GameState.MenuOpen);
            }

            OnPanelOpened?.Invoke(panel);
            Debug.Log($"[UIManager] Opened panel: {panel}");
        }

        /// <summary>
        /// Closes a specific panel.  If it is the topmost panel the one
        /// beneath it (if any) becomes visible again.  If it is buried in the
        /// stack it is simply removed.
        /// </summary>
        /// <param name="panel">The panel to close.</param>
        public void HidePanel(PanelType panel)
        {
            if (panel == PanelType.None)
                return;

            if (_panelHistory.Count == 0)
                return;

            // Top of stack -- simple pop.
            if (_panelHistory.Peek() == panel)
            {
                _panelHistory.Pop();
                SetPanelVisible(panel, false);

                // Reveal the panel beneath if one exists.
                if (_panelHistory.Count > 0)
                {
                    SetPanelVisible(_panelHistory.Peek(), true);
                }
                else
                {
                    ShowOverlay(false);
                    RestoreGameState();
                }

                OnPanelClosed?.Invoke(panel);
                Debug.Log($"[UIManager] Closed panel: {panel}");
                return;
            }

            // Panel is deeper in the stack -- rebuild without it.
            Stack<PanelType> temp = new Stack<PanelType>();
            while (_panelHistory.Count > 0)
            {
                PanelType p = _panelHistory.Pop();
                if (p != panel)
                    temp.Push(p);
            }
            while (temp.Count > 0)
                _panelHistory.Push(temp.Pop());

            SetPanelVisible(panel, false);
            OnPanelClosed?.Invoke(panel);
        }

        /// <summary>
        /// Closes every open panel immediately (no animation) and clears the
        /// navigation stack.
        /// </summary>
        public void HideAllPanels()
        {
            while (_panelHistory.Count > 0)
            {
                PanelType panel = _panelHistory.Pop();
                SetPanelVisibleImmediate(panel, false);
                OnPanelClosed?.Invoke(panel);
            }

            ShowOverlay(false);
            RestoreGameState();
        }

        /// <summary>
        /// Toggles a panel: opens it if closed, or closes it if it is
        /// currently the topmost panel.
        /// </summary>
        /// <param name="panel">The panel to toggle.</param>
        public void TogglePanel(PanelType panel)
        {
            if (_panelHistory.Count > 0 && _panelHistory.Peek() == panel)
                HidePanel(panel);
            else
                ShowPanel(panel);
        }

        /// <summary>
        /// Returns <c>true</c> if any overlay panel (Shop, Menu, Staff,
        /// Settings, Pause) is currently open.
        /// </summary>
        public bool IsAnyPanelOpen()
        {
            return _panelHistory.Count > 0;
        }

        /// <summary>
        /// Returns <c>true</c> if the specified panel is anywhere in the
        /// navigation stack (open but possibly hidden behind another panel).
        /// </summary>
        /// <param name="panel">The panel to query.</param>
        public bool IsPanelInStack(PanelType panel)
        {
            return _panelHistory.Contains(panel);
        }

        /// <summary>
        /// Returns the currently visible (topmost) panel, or
        /// <see cref="PanelType.None"/> if no panel is open.
        /// </summary>
        public PanelType GetCurrentPanel()
        {
            return _panelHistory.Count > 0 ? _panelHistory.Peek() : PanelType.None;
        }

        // ------------------------------------------------------------------ //
        //  Android back button
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Checks for the Android/device back button each frame. If a panel is
        /// open the topmost panel is closed; otherwise the pause panel is shown.
        /// </summary>
        private void HandleAndroidBackButton()
        {
            if (UInput.GetKeyDown(KeyCode.Escape))
            {
                if (_panelHistory.Count > 0)
                {
                    PanelType top = _panelHistory.Peek();
                    HidePanel(top);
                }
                else
                {
                    ShowPanel(PanelType.Pause);
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Panel map
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Builds the lookup dictionary from <see cref="PanelType"/> to
        /// <see cref="GameObject"/> using the serialized references.
        /// </summary>
        private void BuildPanelMap()
        {
            _panelMap = new Dictionary<PanelType, GameObject>
            {
                { PanelType.Shop,     _shopPanel },
                { PanelType.Menu,     _menuPanel },
                { PanelType.Staff,    _staffPanel },
                { PanelType.Settings, _settingsPanel },
                { PanelType.Pause,    _pausePanel }
            };
        }

        // ------------------------------------------------------------------ //
        //  Panel visibility with animation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Shows or hides a panel with a slide animation coroutine.
        /// </summary>
        private void SetPanelVisible(PanelType panel, bool visible)
        {
            if (!_panelMap.TryGetValue(panel, out GameObject go) || go == null)
                return;

            // Cancel any running animation on this panel.
            if (_activeAnimations.TryGetValue(panel, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
                _activeAnimations.Remove(panel);
            }

            if (visible)
            {
                go.SetActive(true);
                Coroutine c = StartCoroutine(AnimateSlideIn(panel, go.GetComponent<RectTransform>()));
                _activeAnimations[panel] = c;
            }
            else
            {
                Coroutine c = StartCoroutine(AnimateSlideOut(panel, go.GetComponent<RectTransform>()));
                _activeAnimations[panel] = c;
            }
        }

        /// <summary>
        /// Immediately sets a panel's visibility without animation.
        /// </summary>
        private void SetPanelVisibleImmediate(PanelType panel, bool visible)
        {
            if (!_panelMap.TryGetValue(panel, out GameObject go) || go == null)
                return;

            // Cancel any running animation.
            if (_activeAnimations.TryGetValue(panel, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
                _activeAnimations.Remove(panel);
            }

            go.SetActive(visible);
            if (visible)
            {
                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// Immediately hides all panels. Called during <see cref="Awake"/>.
        /// </summary>
        private void HideAllPanelsImmediate()
        {
            foreach (KeyValuePair<PanelType, GameObject> kvp in _panelMap)
            {
                if (kvp.Value != null)
                    kvp.Value.SetActive(false);
            }

            if (_overlayDimmer != null)
                _overlayDimmer.SetActive(false);
        }

        // ------------------------------------------------------------------ //
        //  Slide animations (DOTween-style, zero dependencies)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Coroutine that slides a panel from <see cref="_slideOffset"/> to
        /// <c>Vector2.zero</c> using a smooth-step easing curve.
        /// </summary>
        private IEnumerator AnimateSlideIn(PanelType panel, RectTransform rt)
        {
            if (rt == null)
                yield break;

            Vector2 start = _slideOffset;
            Vector2 end = Vector2.zero;
            float elapsed = 0f;

            rt.anchoredPosition = start;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                t = SmoothStep(t);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, t);
                yield return null;
            }

            rt.anchoredPosition = end;
            _activeAnimations.Remove(panel);
        }

        /// <summary>
        /// Coroutine that slides a panel from its current position to
        /// <see cref="_slideOffset"/> and then deactivates it.
        /// </summary>
        private IEnumerator AnimateSlideOut(PanelType panel, RectTransform rt)
        {
            if (rt == null)
                yield break;

            Vector2 start = rt.anchoredPosition;
            Vector2 end = _slideOffset;
            float elapsed = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                t = SmoothStep(t);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, t);
                yield return null;
            }

            rt.anchoredPosition = end;
            rt.gameObject.SetActive(false);
            _activeAnimations.Remove(panel);
        }

        /// <summary>
        /// Attempt at a smooth step function: <c>3t^2 - 2t^3</c>.
        /// Provides ease-in-out behaviour similar to DOTween's InOutQuad.
        /// </summary>
        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ //
        //  Overlay dimmer
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Fades the overlay dimmer in or out.
        /// </summary>
        /// <param name="show">
        /// <c>true</c> to fade in to <see cref="_overlayTargetAlpha"/>;
        /// <c>false</c> to fade out and deactivate.
        /// </param>
        private void ShowOverlay(bool show)
        {
            if (_overlayDimmer == null)
                return;

            if (_overlayFadeCoroutine != null)
            {
                StopCoroutine(_overlayFadeCoroutine);
                _overlayFadeCoroutine = null;
            }

            if (show)
            {
                _overlayDimmer.SetActive(true);
                _overlayFadeCoroutine = StartCoroutine(FadeOverlay(_overlayTargetAlpha));
            }
            else
            {
                _overlayFadeCoroutine = StartCoroutine(FadeOverlayOut());
            }
        }

        /// <summary>
        /// Fades the overlay to the target alpha.
        /// </summary>
        private IEnumerator FadeOverlay(float targetAlpha)
        {
            if (_overlayImage == null)
                yield break;

            Color c = _overlayImage.color;
            float startAlpha = c.a;
            float elapsed = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
                _overlayImage.color = c;
                yield return null;
            }

            c.a = targetAlpha;
            _overlayImage.color = c;
            _overlayFadeCoroutine = null;
        }

        /// <summary>
        /// Fades the overlay to fully transparent and deactivates it.
        /// </summary>
        private IEnumerator FadeOverlayOut()
        {
            if (_overlayImage == null)
            {
                if (_overlayDimmer != null)
                    _overlayDimmer.SetActive(false);
                yield break;
            }

            Color c = _overlayImage.color;
            float startAlpha = c.a;
            float elapsed = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                c.a = Mathf.Lerp(startAlpha, 0f, t);
                _overlayImage.color = c;
                yield return null;
            }

            c.a = 0f;
            _overlayImage.color = c;
            _overlayDimmer.SetActive(false);
            _overlayFadeCoroutine = null;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Restores the <see cref="GameManager"/> state to
        /// <see cref="GameState.Playing"/> when all panels are closed.
        /// </summary>
        private void RestoreGameState()
        {
            if (GameManager.Instance == null)
                return;

            GameState current = GameManager.Instance.CurrentState;
            if (current == GameState.MenuOpen)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
            }
        }
    }
}
