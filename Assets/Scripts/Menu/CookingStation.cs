using System;
using UnityEngine;

namespace SaborColombiano.Menu
{
    /// <summary>
    /// Possible operational states for a <see cref="CookingStation"/>.
    /// </summary>
    public enum CookingStationState
    {
        /// <summary>Station is idle and ready to accept a new dish.</summary>
        Idle,
        /// <summary>A dish is currently being prepared.</summary>
        Cooking,
        /// <summary>Cooking is complete; the dish awaits collection.</summary>
        Ready,
        /// <summary>Station is dirty and must be cleaned before reuse.</summary>
        NeedsCleaning
    }

    /// <summary>
    /// MonoBehaviour representing a single piece of kitchen equipment placed on
    /// the restaurant grid. Each station handles one recipe type at a time,
    /// progresses through cooking states, and fires events that drive UI,
    /// audio, and particle feedback.
    /// <para>
    /// Attach this component to a prefab that also carries a
    /// <c>SaborColombiano.Grid.GridObject</c> component so that the station
    /// can be positioned on the restaurant floor plan.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CookingStation : MonoBehaviour
    {
        // ================================================================== //
        //  Inspector -- Station Identity
        // ================================================================== //

        /// <summary>
        /// The type of equipment this station provides, determining which
        /// recipes it can cook. Must match <see cref="Recipe.RequiredEquipment"/>.
        /// </summary>
        [Header("Station Identity")]
        [SerializeField]
        [Tooltip("Equipment type. Must match Recipe.RequiredEquipment to accept a dish.")]
        private EquipmentType stationType = EquipmentType.Stove;

        // ================================================================== //
        //  Inspector -- Cooking Parameters
        // ================================================================== //

        /// <summary>
        /// Multiplier applied to a recipe's base <see cref="Recipe.CookingTime"/>.
        /// Lower values mean faster cooking. Affected by upgrades.
        /// </summary>
        [Header("Cooking Parameters")]
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Speed multiplier (lower = faster). 1.0 = normal speed.")]
        private float cookingSpeedMultiplier = 1f;

        /// <summary>
        /// How many dishes this station can prepare simultaneously.
        /// Extra capacity unlocks via upgrades.
        /// </summary>
        [SerializeField]
        [Min(1)]
        [Tooltip("Simultaneous dishes this station can handle.")]
        private int capacity = 1;

        /// <summary>
        /// Seconds after cooking completes before the dish burns. Once burned
        /// the dish is lost and a satisfaction penalty is applied.
        /// </summary>
        [SerializeField]
        [Min(1f)]
        [Tooltip("Grace period (seconds) before a ready dish burns.")]
        private float burnGracePeriod = 30f;

        /// <summary>
        /// Customer satisfaction penalty applied when a dish burns.
        /// </summary>
        [SerializeField]
        [Min(0f)]
        [Tooltip("Satisfaction penalty when a dish burns.")]
        private float burnSatisfactionPenalty = 15f;

        /// <summary>
        /// Time in seconds required to clean the station after a burn.
        /// </summary>
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Seconds needed to clean the station.")]
        private float cleaningDuration = 5f;

        // ================================================================== //
        //  Inspector -- Upgrade
        // ================================================================== //

        /// <summary>Current upgrade level of this station (1-based).</summary>
        [Header("Upgrade")]
        [SerializeField]
        [Min(1)]
        [Tooltip("Current upgrade level. Higher = faster cooking, more capacity.")]
        private int upgradeLevel = 1;

        /// <summary>Maximum level this station can reach.</summary>
        [SerializeField]
        [Min(1)]
        [Tooltip("Maximum upgrade level.")]
        private int maxUpgradeLevel = 5;

        /// <summary>
        /// Cooking speed improvement per upgrade level (subtracted from multiplier).
        /// E.g. 0.1 means each level reduces the multiplier by 0.1.
        /// </summary>
        [SerializeField]
        [Min(0f)]
        [Tooltip("Speed multiplier reduction per upgrade level.")]
        private float speedBonusPerLevel = 0.1f;

        /// <summary>
        /// Number of upgrade levels between each capacity increase.
        /// E.g. 2 means capacity grows by 1 every 2 levels.
        /// </summary>
        [SerializeField]
        [Min(1)]
        [Tooltip("Upgrade levels needed for +1 capacity.")]
        private int levelsPerCapacityIncrease = 2;

        // ================================================================== //
        //  Inspector -- Visual Feedback References
        // ================================================================== //

        /// <summary>
        /// Particle system played while the station is actively cooking.
        /// Typically steam, smoke, or bubbles depending on the station type.
        /// </summary>
        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Particle system activated during cooking (steam, smoke, etc.).")]
        private ParticleSystem cookingParticles;

        /// <summary>
        /// GameObject enabled when the dish is ready for collection
        /// (e.g. a glowing plate icon or exclamation mark).
        /// </summary>
        [SerializeField]
        [Tooltip("GameObject shown when a dish is ready for pickup.")]
        private GameObject readyIndicator;

        /// <summary>
        /// GameObject enabled when the dish is about to burn
        /// (e.g. a flashing warning icon or fire particles).
        /// </summary>
        [SerializeField]
        [Tooltip("Warning indicator shown when a dish is close to burning.")]
        private GameObject burningWarning;

        // ================================================================== //
        //  Events
        // ================================================================== //

        /// <summary>
        /// Fired when cooking begins. Provides the recipe being prepared.
        /// </summary>
        public event Action<Recipe> OnCookingStarted;

        /// <summary>
        /// Fired when cooking finishes and the dish enters the Ready state.
        /// Provides the completed recipe.
        /// </summary>
        public event Action<Recipe> OnCookingComplete;

        /// <summary>
        /// Fired when a ready dish burns because it was not collected in time.
        /// Provides the burned recipe.
        /// </summary>
        public event Action<Recipe> OnDishBurned;

        /// <summary>
        /// Fired when the station is cleaned and returns to Idle.
        /// </summary>
        public event Action OnStationCleaned;

        /// <summary>
        /// Fired when the station's upgrade level changes.
        /// Provides the new level.
        /// </summary>
        public event Action<int> OnStationUpgraded;

        // ================================================================== //
        //  Runtime State
        // ================================================================== //

        /// <summary>Current operational state of the station.</summary>
        private CookingStationState currentState = CookingStationState.Idle;

        /// <summary>Recipe currently being cooked or awaiting collection.</summary>
        private Recipe currentRecipe;

        /// <summary>Elapsed cooking time for the current dish.</summary>
        private float cookingTimer;

        /// <summary>Total effective cooking duration for the current dish.</summary>
        private float currentCookingDuration;

        /// <summary>Elapsed time since the dish became Ready.</summary>
        private float readyTimer;

        /// <summary>Elapsed time into the cleaning process.</summary>
        private float cleaningTimer;

        /// <summary>Number of dishes currently being prepared.</summary>
        private int activeDishCount;

        // ================================================================== //
        //  Public Properties
        // ================================================================== //

        /// <summary>Equipment type this station provides.</summary>
        public EquipmentType StationType => stationType;

        /// <summary>Current operational state.</summary>
        public CookingStationState CurrentState => currentState;

        /// <summary>Recipe currently in progress (or awaiting pickup).</summary>
        public Recipe CurrentRecipe => currentRecipe;

        /// <summary>Current upgrade level.</summary>
        public int UpgradeLevel => upgradeLevel;

        /// <summary>Maximum upgrade level.</summary>
        public int MaxUpgradeLevel => maxUpgradeLevel;

        /// <summary>Whether the station can accept another upgrade.</summary>
        public bool CanUpgrade => upgradeLevel < maxUpgradeLevel;

        /// <summary>
        /// Effective cooking speed multiplier after accounting for upgrades.
        /// </summary>
        public float EffectiveCookingSpeed =>
            Mathf.Max(0.1f, cookingSpeedMultiplier - speedBonusPerLevel * (upgradeLevel - 1));

        /// <summary>
        /// Effective capacity after accounting for upgrades.
        /// </summary>
        public int EffectiveCapacity =>
            capacity + (upgradeLevel - 1) / levelsPerCapacityIncrease;

        /// <summary>
        /// Cooking progress normalised to 0 (just started) .. 1 (complete).
        /// Returns 0 when not cooking.
        /// </summary>
        public float CookingProgress
        {
            get
            {
                if (currentState != CookingStationState.Cooking || currentCookingDuration <= 0f)
                    return 0f;
                return Mathf.Clamp01(cookingTimer / currentCookingDuration);
            }
        }

        /// <summary>
        /// Fraction of the burn grace period that has elapsed (0..1).
        /// Returns 0 when the dish is not in the Ready state.
        /// </summary>
        public float BurnProgress
        {
            get
            {
                if (currentState != CookingStationState.Ready || burnGracePeriod <= 0f)
                    return 0f;
                return Mathf.Clamp01(readyTimer / burnGracePeriod);
            }
        }

        /// <summary>Whether the station is idle and can accept a new dish.</summary>
        public bool IsAvailable =>
            currentState == CookingStationState.Idle && activeDishCount < EffectiveCapacity;

        // ================================================================== //
        //  Unity Lifecycle
        // ================================================================== //

        private void Awake()
        {
            SetVisualState(CookingStationState.Idle);
        }

        private void Update()
        {
            switch (currentState)
            {
                case CookingStationState.Cooking:
                    UpdateCooking();
                    break;

                case CookingStationState.Ready:
                    UpdateReady();
                    break;

                case CookingStationState.NeedsCleaning:
                    // Cleaning is driven by the Clean() method, not auto-timer.
                    break;
            }
        }

        // ================================================================== //
        //  Core Actions
        // ================================================================== //

        /// <summary>
        /// Begins cooking a dish. The station must be <see cref="CookingStationState.Idle"/>
        /// and the recipe's required equipment must match <see cref="StationType"/>.
        /// <para>
        /// Ingredient deduction is the caller's responsibility (typically
        /// <c>SaborColombiano.Data.InventoryManager</c>) and should happen
        /// <i>before</i> calling this method.
        /// </para>
        /// </summary>
        /// <param name="recipe">Recipe to prepare.</param>
        /// <returns><c>true</c> if cooking was successfully started.</returns>
        public bool StartCooking(Recipe recipe)
        {
            if (recipe == null)
            {
                Debug.LogWarning("[CookingStation] Cannot cook a null recipe.");
                return false;
            }

            if (currentState != CookingStationState.Idle)
            {
                Debug.LogWarning(
                    $"[CookingStation] Station is {currentState}, not Idle. " +
                    "Cannot start cooking.");
                return false;
            }

            if (recipe.RequiredEquipment != EquipmentType.None &&
                recipe.RequiredEquipment != stationType)
            {
                Debug.LogWarning(
                    $"[CookingStation] This station is a {stationType} but " +
                    $"'{recipe.RecipeName}' requires {recipe.RequiredEquipment}.");
                return false;
            }

            if (activeDishCount >= EffectiveCapacity)
            {
                Debug.LogWarning("[CookingStation] Station is at full capacity.");
                return false;
            }

            // Begin cooking.
            currentRecipe = recipe;
            currentCookingDuration = recipe.CookingTime * EffectiveCookingSpeed;
            cookingTimer = 0f;
            readyTimer = 0f;
            activeDishCount++;

            TransitionTo(CookingStationState.Cooking);
            OnCookingStarted?.Invoke(recipe);
            return true;
        }

        /// <summary>
        /// Collects the finished dish from the station. Only valid in the
        /// <see cref="CookingStationState.Ready"/> state.
        /// </summary>
        /// <returns>
        /// The completed <see cref="Recipe"/>, or <c>null</c> if the station
        /// is not in the Ready state.
        /// </returns>
        public Recipe CollectDish()
        {
            if (currentState != CookingStationState.Ready)
            {
                Debug.LogWarning(
                    $"[CookingStation] Cannot collect -- station is {currentState}.");
                return null;
            }

            Recipe collected = currentRecipe;
            currentRecipe = null;
            activeDishCount = Mathf.Max(0, activeDishCount - 1);

            TransitionTo(CookingStationState.Idle);
            return collected;
        }

        /// <summary>
        /// Initiates or completes the cleaning process. In the
        /// <see cref="CookingStationState.NeedsCleaning"/> state this
        /// progresses the cleaning timer. When cleaning finishes the station
        /// returns to <see cref="CookingStationState.Idle"/>.
        /// <para>
        /// Call this once to begin cleaning (the timer starts advancing in
        /// the coroutine), or call it with <paramref name="instant"/> = true
        /// to skip the timer (e.g. for a paid speed-up).
        /// </para>
        /// </summary>
        /// <param name="instant">If <c>true</c>, skip the cleaning duration.</param>
        /// <returns><c>true</c> if cleaning was initiated or completed.</returns>
        public bool Clean(bool instant = false)
        {
            if (currentState != CookingStationState.NeedsCleaning)
            {
                Debug.LogWarning(
                    $"[CookingStation] Station does not need cleaning (state: {currentState}).");
                return false;
            }

            if (instant)
            {
                FinishCleaning();
                return true;
            }

            // Start cleaning timer -- advances in a coroutine.
            StartCoroutine(CleaningRoutine());
            return true;
        }

        // ================================================================== //
        //  Upgrade System
        // ================================================================== //

        /// <summary>
        /// Upgrades the station by one level. Does nothing if already at max.
        /// Upgrade cost handling is delegated to the economy system.
        /// </summary>
        /// <returns><c>true</c> if the upgrade was applied.</returns>
        public bool Upgrade()
        {
            if (!CanUpgrade)
            {
                Debug.LogWarning(
                    $"[CookingStation] Already at max level ({maxUpgradeLevel}).");
                return false;
            }

            upgradeLevel++;
            OnStationUpgraded?.Invoke(upgradeLevel);
            return true;
        }

        // ================================================================== //
        //  State Updates (called from Update)
        // ================================================================== //

        /// <summary>
        /// Advances the cooking timer and transitions to Ready on completion.
        /// </summary>
        private void UpdateCooking()
        {
            cookingTimer += Time.deltaTime;

            if (cookingTimer >= currentCookingDuration)
            {
                cookingTimer = currentCookingDuration;
                TransitionTo(CookingStationState.Ready);
                OnCookingComplete?.Invoke(currentRecipe);
            }
        }

        /// <summary>
        /// Advances the burn timer while the dish waits for collection.
        /// If the grace period expires, the dish is destroyed and the station
        /// transitions to NeedsCleaning.
        /// </summary>
        private void UpdateReady()
        {
            readyTimer += Time.deltaTime;

            // Show warning when more than half the grace period has elapsed.
            if (burningWarning != null)
            {
                burningWarning.SetActive(readyTimer > burnGracePeriod * 0.5f);
            }

            if (readyTimer >= burnGracePeriod)
            {
                BurnDish();
            }
        }

        // ================================================================== //
        //  Internal Helpers
        // ================================================================== //

        /// <summary>
        /// Destroys the current dish, applies the satisfaction penalty, and
        /// transitions to NeedsCleaning.
        /// </summary>
        private void BurnDish()
        {
            Recipe burned = currentRecipe;
            currentRecipe = null;
            activeDishCount = Mathf.Max(0, activeDishCount - 1);

            TransitionTo(CookingStationState.NeedsCleaning);
            OnDishBurned?.Invoke(burned);

            // Satisfaction penalty is communicated via the event.
            // Subscribers (e.g. SaborColombiano.Core.SatisfactionManager) should
            // deduct burnSatisfactionPenalty from the restaurant's rating.
            Debug.Log(
                $"[CookingStation] '{burned?.RecipeName}' burned! " +
                $"Satisfaction penalty: {burnSatisfactionPenalty}");
        }

        /// <summary>
        /// Transitions the station to a new state and updates visual feedback.
        /// </summary>
        /// <param name="newState">Target state.</param>
        private void TransitionTo(CookingStationState newState)
        {
            currentState = newState;
            SetVisualState(newState);
        }

        /// <summary>
        /// Enables / disables visual feedback objects to match the given state.
        /// </summary>
        /// <param name="state">State to reflect visually.</param>
        private void SetVisualState(CookingStationState state)
        {
            // Cooking particles.
            if (cookingParticles != null)
            {
                if (state == CookingStationState.Cooking)
                {
                    if (!cookingParticles.isPlaying) cookingParticles.Play();
                }
                else
                {
                    if (cookingParticles.isPlaying) cookingParticles.Stop();
                }
            }

            // Ready indicator.
            if (readyIndicator != null)
            {
                readyIndicator.SetActive(state == CookingStationState.Ready);
            }

            // Burning warning (initial state -- UpdateReady handles mid-grace activation).
            if (burningWarning != null && state != CookingStationState.Ready)
            {
                burningWarning.SetActive(false);
            }
        }

        /// <summary>
        /// Coroutine that waits for <see cref="cleaningDuration"/> seconds
        /// then finishes cleaning.
        /// </summary>
        private System.Collections.IEnumerator CleaningRoutine()
        {
            cleaningTimer = 0f;

            while (cleaningTimer < cleaningDuration)
            {
                cleaningTimer += Time.deltaTime;
                yield return null;
            }

            FinishCleaning();
        }

        /// <summary>
        /// Resets state after cleaning and fires the cleaned event.
        /// </summary>
        private void FinishCleaning()
        {
            cleaningTimer = 0f;
            readyTimer = 0f;
            cookingTimer = 0f;
            currentRecipe = null;

            TransitionTo(CookingStationState.Idle);
            OnStationCleaned?.Invoke();
        }

        // ================================================================== //
        //  Public Queries
        // ================================================================== //

        /// <summary>
        /// Returns the burn satisfaction penalty configured for this station.
        /// Used by satisfaction managers to deduct points on burn events.
        /// </summary>
        public float BurnSatisfactionPenalty => burnSatisfactionPenalty;

        /// <summary>
        /// Returns the cleaning progress normalised to 0..1.
        /// Only meaningful in the <see cref="CookingStationState.NeedsCleaning"/> state.
        /// </summary>
        public float CleaningProgress
        {
            get
            {
                if (currentState != CookingStationState.NeedsCleaning || cleaningDuration <= 0f)
                    return 0f;
                return Mathf.Clamp01(cleaningTimer / cleaningDuration);
            }
        }

        // ================================================================== //
        //  Editor Validation
        // ================================================================== //

#if UNITY_EDITOR
        private void OnValidate()
        {
            upgradeLevel = Mathf.Clamp(upgradeLevel, 1, maxUpgradeLevel);
            if (levelsPerCapacityIncrease < 1) levelsPerCapacityIncrease = 1;
        }
#endif
    }
}
