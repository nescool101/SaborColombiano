using System;
using System.Collections.Generic;
using UnityEngine;
using SaborColombiano.Data;

namespace SaborColombiano.Core
{
    /// <summary>
    /// Manages everything that defines the player's restaurant: its name,
    /// level, experience, reputation, cleanliness, and lifetime statistics.
    /// Other systems query this manager to determine capacity, unlocked
    /// recipes, and customer-satisfaction modifiers.
    /// </summary>
    public class RestaurantManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Raised when the restaurant gains a level.
        /// Subscribers receive the new level.
        /// </summary>
        public event Action<int> OnLevelUp;

        /// <summary>
        /// Raised when the restaurant reputation changes.
        /// Subscribers receive the new reputation value (0-5).
        /// </summary>
        public event Action<float> OnReputationChanged;

        /// <summary>
        /// Raised whenever experience is added, even if no level-up occurs.
        /// Subscribers receive the current experience and the threshold for the next level.
        /// </summary>
        public event Action<int, int> OnExperienceChanged;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("Identity")]

        [SerializeField]
        [Tooltip("Display name of the restaurant.")]
        private string _restaurantName = "Mi Fonda Colombiana";

        [Header("Progression")]

        [SerializeField]
        [Tooltip("Starting level of the restaurant.")]
        [Range(1, 100)]
        private int _startingLevel = 1;

        [SerializeField]
        [Tooltip("Base experience required for level 2.  Each subsequent level " +
                 "requires baseExpPerLevel * level * levelExpScaling.")]
        private int _baseExpPerLevel = 100;

        [SerializeField]
        [Tooltip("Scaling factor applied to the experience curve per level.")]
        [Range(1f, 3f)]
        private float _levelExpScaling = 1.25f;

        [Header("Capacity")]

        [SerializeField]
        [Tooltip("Number of seats available at level 1.")]
        private int _baseSeats = 4;

        [SerializeField]
        [Tooltip("Additional seats unlocked per level.")]
        private int _seatsPerLevel = 2;

        [Header("Cleanliness")]

        [SerializeField]
        [Tooltip("Rate at which cleanliness decays per served customer (0-1).")]
        [Range(0f, 0.05f)]
        private float _cleanlinessDecayPerCustomer = 0.01f;

        [SerializeField]
        [Tooltip("Amount of cleanliness restored by one cleaning action (0-1).")]
        [Range(0f, 1f)]
        private float _cleanlinessRestoreAmount = 0.25f;

        [Header("Level Unlock Thresholds")]

        [SerializeField]
        [Tooltip("Levels at which new recipe tiers are unlocked. " +
                 "Index 0 = first unlock level, etc.")]
        private int[] _recipeUnlockLevels = new int[] { 1, 3, 5, 8, 12, 16, 20, 25, 30 };

        [SerializeField]
        [Tooltip("Levels at which new furniture tiers are unlocked.")]
        private int[] _furnitureUnlockLevels = new int[] { 1, 2, 4, 7, 10, 14, 18, 22, 28 };

        // ------------------------------------------------------------------ //
        //  Public properties
        // ------------------------------------------------------------------ //

        /// <summary>Display name of the restaurant.</summary>
        public string RestaurantName
        {
            get => _restaurantName;
            set => _restaurantName = value;
        }

        /// <summary>Current restaurant level (1-based).</summary>
        public int Level { get; private set; } = 1;

        /// <summary>Current accumulated experience points.</summary>
        public int Experience { get; private set; }

        /// <summary>
        /// Restaurant reputation on a 0-5 star scale (supports half-stars
        /// via float precision).
        /// </summary>
        public float Reputation { get; private set; } = 3f;

        /// <summary>
        /// Cleanliness factor from 0 (filthy) to 1 (spotless).
        /// Affects customer satisfaction and reputation.
        /// </summary>
        public float Cleanliness { get; private set; } = 1f;

        /// <summary>
        /// Maximum number of customers that can be seated simultaneously,
        /// derived from level and placed furniture.
        /// </summary>
        public int MaxCustomers => _baseSeats + (_seatsPerLevel * (Level - 1)) + _bonusSeats;

        /// <summary>Lifetime count of customers served.</summary>
        public int TotalCustomersServed { get; private set; }

        /// <summary>Lifetime revenue earned (in Pesos).</summary>
        public long TotalRevenue { get; private set; }

        /// <summary>Experience required to reach the next level.</summary>
        public int ExperienceForNextLevel => CalculateExpForLevel(Level + 1);

        // ------------------------------------------------------------------ //
        //  Satisfaction tracking
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Running average of the last N customer satisfaction scores (0-1).
        /// Used as input to <see cref="CalculateReputation"/>.
        /// </summary>
        public float AverageSatisfaction
        {
            get
            {
                if (_satisfactionHistory.Count == 0)
                    return 0.7f; // default when no data yet
                float sum = 0f;
                foreach (float s in _satisfactionHistory)
                    sum += s;
                return sum / _satisfactionHistory.Count;
            }
        }

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        /// <summary>Bonus seats contributed by placed furniture (set externally).</summary>
        private int _bonusSeats;

        /// <summary>Rolling window of recent customer satisfaction scores.</summary>
        private readonly Queue<float> _satisfactionHistory = new Queue<float>();

        /// <summary>Maximum number of satisfaction entries to keep.</summary>
        private const int SatisfactionWindowSize = 50;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            Level = _startingLevel;
        }

        // ------------------------------------------------------------------ //
        //  Experience and levelling
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Awards experience points to the restaurant. Automatically checks
        /// for level-ups, which may fire <see cref="OnLevelUp"/>.
        /// </summary>
        /// <param name="amount">Non-negative experience to add.</param>
        public void AddExperience(int amount)
        {
            if (amount <= 0)
                return;

            Experience += amount;
            OnExperienceChanged?.Invoke(Experience, ExperienceForNextLevel);
            CheckLevelUp();
        }

        /// <summary>
        /// Repeatedly levels up the restaurant while accumulated experience
        /// meets or exceeds the next-level threshold.
        /// </summary>
        public void CheckLevelUp()
        {
            while (Experience >= ExperienceForNextLevel)
            {
                Experience -= ExperienceForNextLevel;
                Level++;
                Debug.Log($"[RestaurantManager] Level up! Now level {Level}.");
                OnLevelUp?.Invoke(Level);
            }
        }

        /// <summary>
        /// Returns the total experience required to advance from
        /// <paramref name="level"/> - 1 to <paramref name="level"/>.
        /// </summary>
        private int CalculateExpForLevel(int level)
        {
            return Mathf.RoundToInt(_baseExpPerLevel * level * _levelExpScaling);
        }

        // ------------------------------------------------------------------ //
        //  Reputation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Recalculates the restaurant's reputation based on average customer
        /// satisfaction and cleanliness. The result is clamped to [0, 5].
        /// </summary>
        /// <returns>The newly calculated reputation value.</returns>
        public float CalculateReputation()
        {
            // Weighted formula:
            //   70 % average satisfaction + 30 % cleanliness
            // Mapped onto a 0-5 star scale.
            float raw = (AverageSatisfaction * 0.7f + Cleanliness * 0.3f) * 5f;
            float newReputation = Mathf.Clamp(Mathf.Round(raw * 2f) / 2f, 0f, 5f); // round to nearest 0.5

            if (!Mathf.Approximately(newReputation, Reputation))
            {
                Reputation = newReputation;
                OnReputationChanged?.Invoke(Reputation);
                Debug.Log($"[RestaurantManager] Reputation changed to {Reputation:F1} stars.");
            }

            return Reputation;
        }

        // ------------------------------------------------------------------ //
        //  Customer satisfaction
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Records a single customer's satisfaction score and updates
        /// lifetime statistics. Should be called when a customer finishes
        /// their meal and leaves.
        /// </summary>
        /// <param name="satisfaction">Satisfaction score in the range [0, 1].</param>
        /// <param name="revenue">Pesos earned from this customer (before tips).</param>
        public void RecordCustomerServed(float satisfaction, long revenue)
        {
            satisfaction = Mathf.Clamp01(satisfaction);

            // Rolling window.
            _satisfactionHistory.Enqueue(satisfaction);
            if (_satisfactionHistory.Count > SatisfactionWindowSize)
                _satisfactionHistory.Dequeue();

            TotalCustomersServed++;
            TotalRevenue += revenue;

            // Degrade cleanliness.
            Cleanliness = Mathf.Clamp01(Cleanliness - _cleanlinessDecayPerCustomer);

            // Recalculate reputation after each customer.
            CalculateReputation();
        }

        // ------------------------------------------------------------------ //
        //  Cleanliness
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Performs a cleaning action, restoring a fixed amount of
        /// cleanliness. Can be called by the player or an employee.
        /// </summary>
        public void Clean()
        {
            Cleanliness = Mathf.Clamp01(Cleanliness + _cleanlinessRestoreAmount);
            Debug.Log($"[RestaurantManager] Cleaned! Cleanliness now {Cleanliness:P0}.");
        }

        // ------------------------------------------------------------------ //
        //  Furniture / Seats
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called by the grid/furniture system when the number of bonus seats
        /// from placed furniture changes.
        /// </summary>
        /// <param name="seats">Total bonus seats from furniture.</param>
        public void SetBonusSeats(int seats)
        {
            _bonusSeats = Mathf.Max(0, seats);
        }

        // ------------------------------------------------------------------ //
        //  Unlock queries
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the highest recipe tier currently unlocked based on level.
        /// Tier indices are 0-based.
        /// </summary>
        public int GetUnlockedRecipeTier()
        {
            int tier = 0;
            for (int i = 0; i < _recipeUnlockLevels.Length; i++)
            {
                if (Level >= _recipeUnlockLevels[i])
                    tier = i;
                else
                    break;
            }
            return tier;
        }

        /// <summary>
        /// Returns the highest furniture tier currently unlocked based on level.
        /// Tier indices are 0-based.
        /// </summary>
        public int GetUnlockedFurnitureTier()
        {
            int tier = 0;
            for (int i = 0; i < _furnitureUnlockLevels.Length; i++)
            {
                if (Level >= _furnitureUnlockLevels[i])
                    tier = i;
                else
                    break;
            }
            return tier;
        }

        // ------------------------------------------------------------------ //
        //  Serialisation helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Writes the restaurant's current state into the supplied
        /// <see cref="GameData"/> object for persistence.
        /// </summary>
        public void WriteToData(GameData data)
        {
            data.restaurantName = _restaurantName;
            data.restaurantLevel = Level;
            data.restaurantExperience = Experience;
            data.reputation = Reputation;
            data.cleanliness = Cleanliness;
            data.totalCustomersServed = TotalCustomersServed;
            data.totalRevenue = TotalRevenue;
        }

        /// <summary>
        /// Restores the restaurant's state from a previously saved
        /// <see cref="GameData"/> object.
        /// </summary>
        public void LoadFromData(GameData data)
        {
            _restaurantName = string.IsNullOrEmpty(data.restaurantName)
                ? "Mi Fonda Colombiana"
                : data.restaurantName;

            Level = Mathf.Max(1, data.restaurantLevel);
            Experience = Mathf.Max(0, data.restaurantExperience);
            Reputation = Mathf.Clamp(data.reputation, 0f, 5f);
            Cleanliness = Mathf.Clamp01(data.cleanliness);
            TotalCustomersServed = Mathf.Max(0, data.totalCustomersServed);
            TotalRevenue = Math.Max(0L, data.totalRevenue);

            Debug.Log($"[RestaurantManager] Loaded: \"{_restaurantName}\" Lv.{Level} " +
                      $"({Reputation:F1} stars).");
        }
    }
}
