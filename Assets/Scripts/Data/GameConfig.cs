using UnityEngine;

namespace SaborColombiano.Data
{
    /// <summary>
    /// ScriptableObject holding all global tuning values for Sabor Colombiano.
    /// A single asset of this type should live in the project and be referenced by
    /// systems that need configuration (customer AI, economy, staff, day cycle, etc.).
    /// <para>
    /// Create via <b>Assets > Create > Sabor Colombiano > Data > Game Config</b>.
    /// </para>
    /// <para>
    /// Changing values on the asset at runtime in the editor will take effect
    /// immediately for systems that read from this config each frame or per-event.
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameConfig",
        menuName = "Sabor Colombiano/Data/Game Config",
        order = 1)]
    public class GameConfig : ScriptableObject
    {
        // ------------------------------------------------------------------ //
        //  Customer settings
        // ------------------------------------------------------------------ //

        [Header("Customer")]

        [SerializeField]
        [Tooltip("Starting patience of a customer when they enter the restaurant. " +
                 "Measured in seconds of real time (before game-speed multiplier).")]
        [Range(10f, 300f)]
        private float _basePatience = 60f;

        [SerializeField]
        [Tooltip("Rate at which patience decays per second. A value of 1 means " +
                 "the customer loses 1 patience per second at 1x game speed.")]
        [Range(0.1f, 5f)]
        private float _patienceDecayRate = 1f;

        [SerializeField]
        [Tooltip("Maximum number of customers that can be inside the restaurant " +
                 "at the same time (regardless of available seating).")]
        [Range(1, 50)]
        private int _maxCustomersAtOnce = 10;

        [SerializeField]
        [Tooltip("Minimum interval in seconds between customer spawn attempts.")]
        [Range(1f, 60f)]
        private float _customerSpawnInterval = 8f;

        [SerializeField]
        [Tooltip("Base tip percentage (0-1) when customer satisfaction is perfect.")]
        [Range(0f, 0.5f)]
        private float _baseTipPercentage = 0.15f;

        // ------------------------------------------------------------------ //
        //  Economy settings
        // ------------------------------------------------------------------ //

        [Header("Economy")]

        [SerializeField]
        [Tooltip("Pesos the player starts with on a brand-new game.")]
        [Min(0)]
        private long _startingPesos = 5000;

        [SerializeField]
        [Tooltip("Estrellas the player starts with on a brand-new game.")]
        [Min(0)]
        private int _startingEstrellas = 5;

        [SerializeField]
        [Tooltip("Estrellas awarded each time the restaurant levels up.")]
        [Min(0)]
        private int _estrellasPerLevelUp = 2;

        [SerializeField]
        [Tooltip("Base payment a customer makes for a dish, before modifiers " +
                 "such as the dish's own selling price.")]
        [Min(0)]
        private int _baseCustomerPayment = 1000;

        // ------------------------------------------------------------------ //
        //  Restaurant / progression settings
        // ------------------------------------------------------------------ //

        [Header("Restaurant Progression")]

        [SerializeField]
        [Tooltip("Number of menu slots available at level 1.")]
        [Range(1, 20)]
        private int _baseMenuSlots = 3;

        [SerializeField]
        [Tooltip("Additional menu slots unlocked per restaurant level.")]
        [Range(0, 5)]
        private int _slotsPerLevel = 1;

        [SerializeField]
        [Tooltip("Maximum attainable restaurant level.")]
        [Range(1, 100)]
        private int _maxLevel = 30;

        [SerializeField]
        [Tooltip("Experience awarded for each customer served (base, before bonuses).")]
        [Min(0)]
        private int _xpPerCustomerServed = 10;

        [SerializeField]
        [Tooltip("Bonus experience awarded when a customer leaves at maximum satisfaction.")]
        [Min(0)]
        private int _xpPerPerfectService = 25;

        // ------------------------------------------------------------------ //
        //  Staff settings
        // ------------------------------------------------------------------ //

        [Header("Staff")]

        [SerializeField]
        [Tooltip("Base cost in Pesos to hire the first staff member of any type.")]
        [Min(0)]
        private int _baseHireCost = 1000;

        [SerializeField]
        [Tooltip("Multiplier applied to the hire cost for each additional staff member " +
                 "of the same type (cost = baseHireCost * multiplier ^ existingCount).")]
        [Range(1f, 3f)]
        private float _hireCostMultiplier = 1.5f;

        [SerializeField]
        [Tooltip("Base cost in Pesos to upgrade a staff member one level.")]
        [Min(0)]
        private int _upgradeBaseCost = 500;

        [SerializeField]
        [Tooltip("Maximum level a single staff member can reach.")]
        [Range(1, 20)]
        private int _maxStaffLevel = 10;

        [SerializeField]
        [Tooltip("Maximum number of staff the restaurant can employ at each level. " +
                 "Index 0 = level 1, etc. The array length should match maxLevel.")]
        private int[] _staffPerLevel = new int[]
        {
            2, 3, 3, 4, 4, 5, 5, 6, 6, 7,     // levels  1-10
            7, 8, 8, 9, 9, 10, 10, 11, 11, 12, // levels 11-20
            12, 13, 13, 14, 14, 15, 15, 16, 16, 17 // levels 21-30
        };

        // ------------------------------------------------------------------ //
        //  Day / time settings
        // ------------------------------------------------------------------ //

        [Header("Day Cycle")]

        [SerializeField]
        [Tooltip("Duration of one in-game day in real-time seconds.")]
        [Range(30f, 600f)]
        private float _dayDurationSeconds = 120f;

        [SerializeField]
        [Tooltip("Number of in-game days that constitute a week (for weekly bonuses).")]
        [Range(1, 14)]
        private int _daysPerWeek = 7;

        // ------------------------------------------------------------------ //
        //  Difficulty
        // ------------------------------------------------------------------ //

        [Header("Difficulty Modifiers")]

        [SerializeField]
        [Tooltip("Global multiplier applied to customer patience (< 1 = harder, > 1 = easier).")]
        [Range(0.5f, 2f)]
        private float _patienceModifier = 1f;

        [SerializeField]
        [Tooltip("Global multiplier applied to ingredient spoilage rate " +
                 "(< 1 = spoils slower, > 1 = spoils faster).")]
        [Range(0.5f, 2f)]
        private float _spoilageModifier = 1f;

        [SerializeField]
        [Tooltip("Global multiplier applied to all revenue (< 1 = less money, > 1 = more money).")]
        [Range(0.5f, 2f)]
        private float _revenueModifier = 1f;

        // ------------------------------------------------------------------ //
        //  Level thresholds
        // ------------------------------------------------------------------ //

        [Header("Level Thresholds")]

        [SerializeField]
        [Tooltip("Total experience required to reach each level. " +
                 "Index 0 = XP needed for level 2, index 1 = XP for level 3, etc. " +
                 "If the array is shorter than maxLevel, the last value is repeated.")]
        private int[] _levelThresholds = new int[]
        {
            100,    // level  2
            250,    // level  3
            500,    // level  4
            850,    // level  5
            1300,   // level  6
            1900,   // level  7
            2700,   // level  8
            3700,   // level  9
            5000,   // level 10
            6500,   // level 11
            8500,   // level 12
            11000,  // level 13
            14000,  // level 14
            17500,  // level 15
            22000,  // level 16
            27500,  // level 17
            34000,  // level 18
            42000,  // level 19
            52000,  // level 20
            64000,  // level 21
            78000,  // level 22
            95000,  // level 23
            115000, // level 24
            140000, // level 25
            170000, // level 26
            205000, // level 27
            250000, // level 28
            300000, // level 29
            360000  // level 30
        };

        // ------------------------------------------------------------------ //
        //  Public properties -- Customer
        // ------------------------------------------------------------------ //

        /// <summary>Baseline patience for a new customer (seconds).</summary>
        public float BasePatience => _basePatience;

        /// <summary>Patience decay rate per second.</summary>
        public float PatienceDecayRate => _patienceDecayRate;

        /// <summary>Maximum customers present in the restaurant simultaneously.</summary>
        public int MaxCustomersAtOnce => _maxCustomersAtOnce;

        /// <summary>Seconds between customer spawn attempts.</summary>
        public float CustomerSpawnInterval => _customerSpawnInterval;

        /// <summary>Base tip percentage at maximum satisfaction (0-1).</summary>
        public float BaseTipPercentage => _baseTipPercentage;

        // ------------------------------------------------------------------ //
        //  Public properties -- Economy
        // ------------------------------------------------------------------ //

        /// <summary>Pesos balance for a new game.</summary>
        public long StartingPesos => _startingPesos;

        /// <summary>Estrellas balance for a new game.</summary>
        public int StartingEstrellas => _startingEstrellas;

        /// <summary>Estrellas rewarded per level-up.</summary>
        public int EstrellasPerLevelUp => _estrellasPerLevelUp;

        /// <summary>Base customer payment before dish-price modifiers.</summary>
        public int BaseCustomerPayment => _baseCustomerPayment;

        // ------------------------------------------------------------------ //
        //  Public properties -- Restaurant
        // ------------------------------------------------------------------ //

        /// <summary>Menu slots at level 1.</summary>
        public int BaseMenuSlots => _baseMenuSlots;

        /// <summary>Extra menu slots per level.</summary>
        public int SlotsPerLevel => _slotsPerLevel;

        /// <summary>Highest achievable restaurant level.</summary>
        public int MaxLevel => _maxLevel;

        /// <summary>Base XP per customer served.</summary>
        public int XpPerCustomerServed => _xpPerCustomerServed;

        /// <summary>Bonus XP for a perfect-satisfaction service.</summary>
        public int XpPerPerfectService => _xpPerPerfectService;

        // ------------------------------------------------------------------ //
        //  Public properties -- Staff
        // ------------------------------------------------------------------ //

        /// <summary>Base hire cost for the first staff member.</summary>
        public int BaseHireCost => _baseHireCost;

        /// <summary>Exponential cost multiplier per additional staff of the same type.</summary>
        public float HireCostMultiplier => _hireCostMultiplier;

        /// <summary>Base cost to upgrade a staff member one level.</summary>
        public int UpgradeBaseCost => _upgradeBaseCost;

        /// <summary>Maximum attainable staff level.</summary>
        public int MaxStaffLevel => _maxStaffLevel;

        // ------------------------------------------------------------------ //
        //  Public properties -- Day
        // ------------------------------------------------------------------ //

        /// <summary>Real-time seconds per in-game day.</summary>
        public float DayDurationSeconds => _dayDurationSeconds;

        /// <summary>Number of in-game days in a week (for weekly bonuses).</summary>
        public int DaysPerWeek => _daysPerWeek;

        // ------------------------------------------------------------------ //
        //  Public properties -- Difficulty
        // ------------------------------------------------------------------ //

        /// <summary>Global patience modifier (multiplied with base patience).</summary>
        public float PatienceModifier => _patienceModifier;

        /// <summary>Global spoilage rate modifier.</summary>
        public float SpoilageModifier => _spoilageModifier;

        /// <summary>Global revenue modifier.</summary>
        public float RevenueModifier => _revenueModifier;

        // ------------------------------------------------------------------ //
        //  Level-dependent queries
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the total number of menu slots available at the given
        /// restaurant level.
        /// </summary>
        /// <param name="level">Restaurant level (1-based).</param>
        /// <returns>Menu slot count.</returns>
        public int GetMenuSlots(int level)
        {
            level = Mathf.Clamp(level, 1, _maxLevel);
            return _baseMenuSlots + _slotsPerLevel * (level - 1);
        }

        /// <summary>
        /// Returns the maximum number of staff the restaurant can employ at
        /// the given level.
        /// </summary>
        /// <param name="level">Restaurant level (1-based).</param>
        /// <returns>Maximum staff count.</returns>
        public int GetMaxStaff(int level)
        {
            level = Mathf.Clamp(level, 1, _maxLevel);
            int index = level - 1;

            if (_staffPerLevel == null || _staffPerLevel.Length == 0)
                return 2;

            if (index < _staffPerLevel.Length)
                return _staffPerLevel[index];

            // If the array is shorter than the requested level, return the last entry.
            return _staffPerLevel[_staffPerLevel.Length - 1];
        }

        /// <summary>
        /// Returns the total experience required to advance from the given level
        /// to the next. If the level is at or beyond <see cref="MaxLevel"/>, returns
        /// <c>int.MaxValue</c>.
        /// </summary>
        /// <param name="level">Current restaurant level (1-based).</param>
        /// <returns>XP threshold to reach <c>level + 1</c>.</returns>
        public int GetXpForNextLevel(int level)
        {
            if (level >= _maxLevel)
                return int.MaxValue;

            int index = level - 1; // level 1 -> index 0 -> threshold for level 2

            if (_levelThresholds == null || _levelThresholds.Length == 0)
                return 100 * level;

            if (index < _levelThresholds.Length)
                return _levelThresholds[index];

            return _levelThresholds[_levelThresholds.Length - 1];
        }

        /// <summary>
        /// Calculates the hire cost for recruiting an additional staff member
        /// when the player already has <paramref name="existingCount"/> employees
        /// of the same type.
        /// </summary>
        /// <param name="existingCount">Number of staff of the same type already hired.</param>
        /// <returns>Cost in Pesos.</returns>
        public int GetHireCost(int existingCount)
        {
            existingCount = Mathf.Max(0, existingCount);
            return Mathf.RoundToInt(_baseHireCost * Mathf.Pow(_hireCostMultiplier, existingCount));
        }

        /// <summary>
        /// Calculates the cost to upgrade a staff member from their current level
        /// to the next.
        /// </summary>
        /// <param name="currentStaffLevel">The staff member's current level (1-based).</param>
        /// <returns>Cost in Pesos, or <c>int.MaxValue</c> if already at max.</returns>
        public int GetStaffUpgradeCost(int currentStaffLevel)
        {
            if (currentStaffLevel >= _maxStaffLevel)
                return int.MaxValue;

            return _upgradeBaseCost * currentStaffLevel;
        }

        /// <summary>
        /// Returns the effective patience for a customer, combining the base
        /// patience with the difficulty modifier.
        /// </summary>
        /// <returns>Effective patience in seconds.</returns>
        public float GetEffectivePatience()
        {
            return _basePatience * _patienceModifier;
        }

        // ------------------------------------------------------------------ //
        //  Validation
        // ------------------------------------------------------------------ //

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_levelThresholds == null || _levelThresholds.Length == 0)
            {
                Debug.LogWarning("[GameConfig] Level thresholds array is empty. " +
                                 "A fallback formula will be used at runtime.");
            }

            if (_staffPerLevel == null || _staffPerLevel.Length == 0)
            {
                Debug.LogWarning("[GameConfig] Staff-per-level array is empty. " +
                                 "A default of 2 will be used at runtime.");
            }
        }
#endif
    }
}
