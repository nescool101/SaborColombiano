using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SaborColombiano.Core;

namespace SaborColombiano.UI
{
    /// <summary>
    /// Controls the always-visible gameplay HUD for Sabor Colombiano.
    /// Displays the player's currency balances, restaurant level and XP
    /// progress, reputation stars, the current in-game day, and game-speed
    /// indicator. Also hosts shortcut buttons that open the Shop, Menu Book,
    /// Staff, and Settings panels via <see cref="UIManager"/>.
    /// <para>
    /// Currency values animate smoothly (counting up/down) and a floating text
    /// popup can be spawned to show income events such as "+50 pesos!".
    /// Notification badges can be toggled on any HUD button.
    /// </para>
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector fields -- Currency display
        // ------------------------------------------------------------------ //

        [Header("Currency Display")]

        [SerializeField]
        [Tooltip("Text component showing the current pesos balance.")]
        private Text _pesosText;

        [SerializeField]
        [Tooltip("Image of the coin icon next to the pesos display. " +
                 "Plays a scale-pulse animation when the balance changes.")]
        private Image _coinIcon;

        [SerializeField]
        [Tooltip("Text component showing the current estrellas (premium currency) balance.")]
        private Text _estrellasText;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Level & Reputation
        // ------------------------------------------------------------------ //

        [Header("Level & Reputation")]

        [SerializeField]
        [Tooltip("Text component showing the restaurant level number.")]
        private Text _levelText;

        [SerializeField]
        [Tooltip("Slider representing the experience bar towards the next level.")]
        private Slider _xpBar;

        [SerializeField]
        [Tooltip("Array of Image components representing reputation stars (1-5). " +
                 "Filled stars use the filled sprite; empty use the outline sprite.")]
        private Image[] _reputationStars;

        [SerializeField]
        [Tooltip("Sprite used for a fully filled reputation star.")]
        private Sprite _starFilledSprite;

        [SerializeField]
        [Tooltip("Sprite used for a half-filled reputation star.")]
        private Sprite _starHalfSprite;

        [SerializeField]
        [Tooltip("Sprite used for an empty reputation star outline.")]
        private Sprite _starEmptySprite;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Day & Speed
        // ------------------------------------------------------------------ //

        [Header("Day & Speed")]

        [SerializeField]
        [Tooltip("Text component showing the current in-game day number.")]
        private Text _dayText;

        [SerializeField]
        [Tooltip("Text component showing the current game speed multiplier (1x, 2x, 3x).")]
        private Text _speedText;

        [SerializeField]
        [Tooltip("Button that cycles through game-speed settings.")]
        private Button _speedButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Navigation buttons
        // ------------------------------------------------------------------ //

        [Header("Navigation Buttons")]

        [SerializeField]
        [Tooltip("Button that opens the Shop panel.")]
        private Button _shopButton;

        [SerializeField]
        [Tooltip("Button that opens the Menu Book panel.")]
        private Button _menuButton;

        [SerializeField]
        [Tooltip("Button that opens the Staff panel.")]
        private Button _staffButton;

        [SerializeField]
        [Tooltip("Button that opens the Settings panel.")]
        private Button _settingsButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Notification badges
        // ------------------------------------------------------------------ //

        [Header("Notification Badges")]

        [SerializeField]
        [Tooltip("Badge GameObject on the Shop button (set active to show).")]
        private GameObject _shopBadge;

        [SerializeField]
        [Tooltip("Badge GameObject on the Menu button.")]
        private GameObject _menuBadge;

        [SerializeField]
        [Tooltip("Badge GameObject on the Staff button.")]
        private GameObject _staffBadge;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Floating text
        // ------------------------------------------------------------------ //

        [Header("Floating Text")]

        [SerializeField]
        [Tooltip("Prefab for the floating income/expense text popup. " +
                 "Must contain a Text component on its root or first child.")]
        private GameObject _floatingTextPrefab;

        [SerializeField]
        [Tooltip("RectTransform parent under which floating texts are spawned.")]
        private RectTransform _floatingTextParent;

        [SerializeField]
        [Tooltip("How far (in local Y units) the floating text travels upward.")]
        private float _floatingTextRiseDistance = 80f;

        [SerializeField]
        [Tooltip("Duration of the floating text lifecycle in seconds.")]
        private float _floatingTextDuration = 1.2f;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Notification toast
        // ------------------------------------------------------------------ //

        [Header("Notification Toast")]

        [SerializeField]
        [Tooltip("Text component used for temporary notification messages.")]
        private Text _notificationText;

        [SerializeField]
        [Tooltip("CanvasGroup on the notification toast for alpha fading.")]
        private CanvasGroup _notificationCanvasGroup;

        [SerializeField]
        [Tooltip("Duration in seconds the notification remains visible before fading.")]
        private float _notificationDisplayDuration = 2.5f;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Animation tuning
        // ------------------------------------------------------------------ //

        [Header("Animation")]

        [SerializeField]
        [Tooltip("Speed of the smooth number-counting animation (higher = faster).")]
        [Range(1f, 20f)]
        private float _countSpeed = 8f;

        [SerializeField]
        [Tooltip("Scale multiplier applied during the coin-icon pulse.")]
        private float _coinPulseScale = 1.3f;

        [SerializeField]
        [Tooltip("Duration of the coin-icon pulse in seconds.")]
        private float _coinPulseDuration = 0.25f;

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        /// <summary>The value currently rendered on the pesos display (animating toward <see cref="_targetPesos"/>).</summary>
        private float _displayedPesos;

        /// <summary>The true pesos value we are counting toward.</summary>
        private int _targetPesos;

        /// <summary>The value currently rendered on the estrellas display.</summary>
        private float _displayedEstrellas;

        /// <summary>The true estrellas value we are counting toward.</summary>
        private int _targetEstrellas;

        /// <summary>Available speed steps cycled by the speed button.</summary>
        private readonly float[] _speedSteps = { 1f, 2f, 3f };

        /// <summary>Index into <see cref="_speedSteps"/>.</summary>
        private int _currentSpeedIndex;

        /// <summary>Running coroutine for the notification toast fade.</summary>
        private Coroutine _notificationCoroutine;

        /// <summary>Running coroutine for the coin pulse animation.</summary>
        private Coroutine _coinPulseCoroutine;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            WireButtons();
        }

        private void Start()
        {
            SubscribeToManagers();
        }

        private void Update()
        {
            AnimateCurrencyDisplays();
        }

        private void OnDestroy()
        {
            UnsubscribeFromManagers();
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Currency
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Sets the target pesos value. The HUD will animate the displayed
        /// number toward this value using a smooth counting effect.
        /// </summary>
        /// <param name="pesos">New pesos balance.</param>
        public void UpdatePesos(int pesos)
        {
            int previous = _targetPesos;
            _targetPesos = pesos;

            if (pesos != previous)
            {
                PlayCoinPulse();
            }
        }

        /// <summary>
        /// Sets the target estrellas (premium currency) value.
        /// The HUD animates toward this value with a counting effect.
        /// </summary>
        /// <param name="estrellas">New estrellas balance.</param>
        public void UpdateEstrellas(int estrellas)
        {
            _targetEstrellas = estrellas;
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Level & Reputation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Updates the restaurant level display and XP progress bar.
        /// </summary>
        /// <param name="level">Current restaurant level.</param>
        /// <param name="xpProgress">
        /// Normalised progress toward the next level (0 to 1).
        /// </param>
        public void UpdateLevel(int level, float xpProgress)
        {
            if (_levelText != null)
                _levelText.text = $"Nv. {level}";

            if (_xpBar != null)
                _xpBar.value = Mathf.Clamp01(xpProgress);
        }

        /// <summary>
        /// Updates the reputation star display. Supports full, half, and empty
        /// stars on a 0-5 scale.
        /// </summary>
        /// <param name="reputation">Reputation value from 0 to 5.</param>
        public void UpdateReputation(float reputation)
        {
            if (_reputationStars == null)
                return;

            reputation = Mathf.Clamp(reputation, 0f, 5f);

            for (int i = 0; i < _reputationStars.Length; i++)
            {
                if (_reputationStars[i] == null)
                    continue;

                float starThreshold = i + 1f;

                if (reputation >= starThreshold)
                {
                    _reputationStars[i].sprite = _starFilledSprite;
                }
                else if (reputation >= starThreshold - 0.5f)
                {
                    _reputationStars[i].sprite = _starHalfSprite;
                }
                else
                {
                    _reputationStars[i].sprite = _starEmptySprite;
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Day
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Updates the displayed day number.
        /// </summary>
        /// <param name="day">The current in-game day (1-based).</param>
        public void UpdateDay(int day)
        {
            if (_dayText != null)
                _dayText.text = $"Dia {day}";
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Notifications
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Shows a temporary notification message on the HUD that fades out
        /// after <see cref="_notificationDisplayDuration"/> seconds.
        /// </summary>
        /// <param name="message">The message to display.</param>
        public void ShowNotification(string message)
        {
            if (_notificationText == null)
                return;

            _notificationText.text = message;

            if (_notificationCoroutine != null)
                StopCoroutine(_notificationCoroutine);

            _notificationCoroutine = StartCoroutine(NotificationFadeRoutine());
        }

        /// <summary>
        /// Toggles the notification badge on one of the HUD buttons.
        /// </summary>
        /// <param name="panel">Which panel button to badge.</param>
        /// <param name="visible">Whether the badge should be visible.</param>
        public void SetBadgeVisible(PanelType panel, bool visible)
        {
            switch (panel)
            {
                case PanelType.Shop:
                    if (_shopBadge != null) _shopBadge.SetActive(visible);
                    break;
                case PanelType.Menu:
                    if (_menuBadge != null) _menuBadge.SetActive(visible);
                    break;
                case PanelType.Staff:
                    if (_staffBadge != null) _staffBadge.SetActive(visible);
                    break;
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Floating text
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Spawns a floating text popup that rises and fades out, typically
        /// used to show income such as "+50 pesos!".
        /// </summary>
        /// <param name="message">The text to display (e.g. "+50 pesos!").</param>
        /// <param name="color">Color of the floating text.</param>
        public void SpawnFloatingText(string message, Color color)
        {
            if (_floatingTextPrefab == null || _floatingTextParent == null)
                return;

            GameObject instance = Instantiate(_floatingTextPrefab, _floatingTextParent);
            Text textComponent = instance.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                textComponent.text = message;
                textComponent.color = color;
            }

            StartCoroutine(FloatingTextRoutine(instance));
        }

        /// <summary>
        /// Convenience overload that spawns green text for positive income.
        /// </summary>
        /// <param name="amount">The pesos amount earned.</param>
        public void SpawnIncomeText(int amount)
        {
            SpawnFloatingText($"+{amount} pesos!", new Color(0.2f, 0.8f, 0.2f, 1f));
        }

        /// <summary>
        /// Convenience overload that spawns red text for expenses.
        /// </summary>
        /// <param name="amount">The pesos amount spent (positive number).</param>
        public void SpawnExpenseText(int amount)
        {
            SpawnFloatingText($"-{amount} pesos", new Color(0.9f, 0.2f, 0.2f, 1f));
        }

        // ------------------------------------------------------------------ //
        //  Button wiring
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Binds button click listeners.
        /// </summary>
        private void WireButtons()
        {
            if (_shopButton != null)
                _shopButton.onClick.AddListener(OnShopButtonClicked);

            if (_menuButton != null)
                _menuButton.onClick.AddListener(OnMenuButtonClicked);

            if (_staffButton != null)
                _staffButton.onClick.AddListener(OnStaffButtonClicked);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsButtonClicked);

            if (_speedButton != null)
                _speedButton.onClick.AddListener(OnSpeedButtonClicked);
        }

        private void OnShopButtonClicked()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.TogglePanel(PanelType.Shop);
        }

        private void OnMenuButtonClicked()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.TogglePanel(PanelType.Menu);
        }

        private void OnStaffButtonClicked()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.TogglePanel(PanelType.Staff);
        }

        private void OnSettingsButtonClicked()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.TogglePanel(PanelType.Settings);
        }

        /// <summary>
        /// Cycles through speed steps: 1x -> 2x -> 3x -> 1x.
        /// </summary>
        private void OnSpeedButtonClicked()
        {
            _currentSpeedIndex = (_currentSpeedIndex + 1) % _speedSteps.Length;
            float newSpeed = _speedSteps[_currentSpeedIndex];

            if (GameManager.Instance != null)
                GameManager.Instance.SetGameSpeed(newSpeed);

            if (_speedText != null)
                _speedText.text = $"{newSpeed:F0}x";
        }

        // ------------------------------------------------------------------ //
        //  Manager subscriptions
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Subscribes to relevant events on the GameManager and RestaurantManager
        /// to keep the HUD up to date automatically.
        /// </summary>
        private void SubscribeToManagers()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayChanged += UpdateDay;
            }

            if (GameManager.Instance != null && GameManager.Instance.Restaurant != null)
            {
                RestaurantManager rm = GameManager.Instance.Restaurant;
                rm.OnLevelUp += OnRestaurantLevelUp;
                rm.OnReputationChanged += UpdateReputation;
                rm.OnExperienceChanged += OnExperienceChanged;
            }

            // Set initial values.
            RefreshAllDisplays();
        }

        /// <summary>
        /// Unsubscribes from manager events to prevent leaks.
        /// </summary>
        private void UnsubscribeFromManagers()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayChanged -= UpdateDay;
            }

            if (GameManager.Instance != null && GameManager.Instance.Restaurant != null)
            {
                RestaurantManager rm = GameManager.Instance.Restaurant;
                rm.OnLevelUp -= OnRestaurantLevelUp;
                rm.OnReputationChanged -= UpdateReputation;
                rm.OnExperienceChanged -= OnExperienceChanged;
            }
        }

        /// <summary>
        /// Handler for <see cref="RestaurantManager.OnLevelUp"/>.
        /// </summary>
        private void OnRestaurantLevelUp(int newLevel)
        {
            RestaurantManager rm = GameManager.Instance?.Restaurant;
            if (rm != null)
            {
                float progress = (float)rm.Experience / Mathf.Max(1, rm.ExperienceForNextLevel);
                UpdateLevel(newLevel, progress);
            }

            ShowNotification($"Subiste al nivel {newLevel}!");
        }

        /// <summary>
        /// Handler for <see cref="RestaurantManager.OnExperienceChanged"/>.
        /// </summary>
        private void OnExperienceChanged(int currentXp, int xpForNext)
        {
            RestaurantManager rm = GameManager.Instance?.Restaurant;
            if (rm != null)
            {
                float progress = (float)currentXp / Mathf.Max(1, xpForNext);
                UpdateLevel(rm.Level, progress);
            }
        }

        /// <summary>
        /// Pulls current values from managers and refreshes every HUD element.
        /// </summary>
        private void RefreshAllDisplays()
        {
            if (GameManager.Instance == null)
                return;

            UpdateDay(GameManager.Instance.CurrentDay);

            RestaurantManager rm = GameManager.Instance.Restaurant;
            if (rm != null)
            {
                float progress = (float)rm.Experience / Mathf.Max(1, rm.ExperienceForNextLevel);
                UpdateLevel(rm.Level, progress);
                UpdateReputation(rm.Reputation);
            }

            // Speed display.
            if (_speedText != null)
            {
                float speed = GameManager.Instance.GameSpeed;
                _speedText.text = $"{speed:F0}x";
            }
        }

        // ------------------------------------------------------------------ //
        //  Smooth number animation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called every frame to animate the displayed currency numbers toward
        /// their target values.
        /// </summary>
        private void AnimateCurrencyDisplays()
        {
            // Pesos.
            if (!Mathf.Approximately(_displayedPesos, _targetPesos))
            {
                _displayedPesos = Mathf.MoveTowards(
                    _displayedPesos,
                    _targetPesos,
                    Mathf.Max(1f, Mathf.Abs(_targetPesos - _displayedPesos) * _countSpeed * Time.unscaledDeltaTime));

                if (_pesosText != null)
                    _pesosText.text = FormatCurrency(Mathf.RoundToInt(_displayedPesos));
            }

            // Estrellas.
            if (!Mathf.Approximately(_displayedEstrellas, _targetEstrellas))
            {
                _displayedEstrellas = Mathf.MoveTowards(
                    _displayedEstrellas,
                    _targetEstrellas,
                    Mathf.Max(1f, Mathf.Abs(_targetEstrellas - _displayedEstrellas) * _countSpeed * Time.unscaledDeltaTime));

                if (_estrellasText != null)
                    _estrellasText.text = FormatCurrency(Mathf.RoundToInt(_displayedEstrellas));
            }
        }

        /// <summary>
        /// Formats a currency value with thousands separators using dots
        /// (Colombian convention: 1.000.000).
        /// </summary>
        private static string FormatCurrency(int value)
        {
            return value.ToString("N0").Replace(",", ".");
        }

        // ------------------------------------------------------------------ //
        //  Coin pulse animation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Plays a quick scale-up-then-down pulse on the coin icon to draw
        /// attention when the balance changes.
        /// </summary>
        private void PlayCoinPulse()
        {
            if (_coinIcon == null)
                return;

            if (_coinPulseCoroutine != null)
                StopCoroutine(_coinPulseCoroutine);

            _coinPulseCoroutine = StartCoroutine(CoinPulseRoutine());
        }

        /// <summary>
        /// Coroutine that scales the coin icon up then back to 1.
        /// </summary>
        private IEnumerator CoinPulseRoutine()
        {
            RectTransform rt = _coinIcon.rectTransform;
            Vector3 originalScale = Vector3.one;
            Vector3 targetScale = Vector3.one * _coinPulseScale;
            float halfDuration = _coinPulseDuration * 0.5f;

            // Scale up.
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                rt.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }

            // Scale down.
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                rt.localScale = Vector3.Lerp(targetScale, originalScale, t);
                yield return null;
            }

            rt.localScale = originalScale;
            _coinPulseCoroutine = null;
        }

        // ------------------------------------------------------------------ //
        //  Floating text routine
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Coroutine that moves a floating text instance upward while fading
        /// its alpha to zero, then destroys it.
        /// </summary>
        private IEnumerator FloatingTextRoutine(GameObject instance)
        {
            RectTransform rt = instance.GetComponent<RectTransform>();
            CanvasGroup cg = instance.GetComponent<CanvasGroup>();

            // Add a CanvasGroup at runtime if the prefab does not have one.
            if (cg == null)
                cg = instance.AddComponent<CanvasGroup>();

            Vector2 startPos = rt.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < _floatingTextDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _floatingTextDuration);

                rt.anchoredPosition = startPos + new Vector2(0f, _floatingTextRiseDistance * t);
                cg.alpha = 1f - t;

                yield return null;
            }

            Destroy(instance);
        }

        // ------------------------------------------------------------------ //
        //  Notification toast routine
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Shows the notification text at full opacity, waits, then fades out.
        /// </summary>
        private IEnumerator NotificationFadeRoutine()
        {
            if (_notificationCanvasGroup != null)
                _notificationCanvasGroup.alpha = 1f;

            yield return new WaitForSecondsRealtime(_notificationDisplayDuration);

            // Fade out over 0.5 seconds.
            float fadeDuration = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                if (_notificationCanvasGroup != null)
                    _notificationCanvasGroup.alpha = 1f - t;
                yield return null;
            }

            if (_notificationCanvasGroup != null)
                _notificationCanvasGroup.alpha = 0f;

            _notificationCoroutine = null;
        }
    }
}
