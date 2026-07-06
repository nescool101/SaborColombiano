using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaborColombiano.Data
{
    // ====================================================================== //
    //  Nested serialisable structures
    // ====================================================================== //

    /// <summary>
    /// Persisted state of the restaurant itself: identity, progression, and
    /// condition metrics.
    /// </summary>
    [Serializable]
    public class RestaurantData
    {
        /// <summary>Player-chosen name for the restaurant.</summary>
        public string name = "Mi Fonda Colombiana";

        /// <summary>Current restaurant level (1-based).</summary>
        public int level = 1;

        /// <summary>Accumulated experience towards the next level.</summary>
        public int experience;

        /// <summary>Star rating from 0 to 5 (supports half-stars via float).</summary>
        public float reputation = 3f;

        /// <summary>Cleanliness factor from 0 (filthy) to 1 (spotless).</summary>
        public float cleanliness = 1f;
    }

    /// <summary>
    /// Persisted currency balances and lifetime revenue tracking.
    /// </summary>
    [Serializable]
    public class EconomyData
    {
        /// <summary>Current Pesos balance (primary soft currency).</summary>
        public long pesos = 5000;

        /// <summary>Current Estrellas balance (premium hard currency).</summary>
        public int estrellas = 5;

        /// <summary>Lifetime revenue earned in Pesos.</summary>
        public long totalRevenue;
    }

    /// <summary>
    /// Persisted menu configuration: unlocked recipes, active menu, daily special,
    /// and per-recipe popularity tracking.
    /// </summary>
    [Serializable]
    public class MenuData
    {
        /// <summary>IDs of all recipes the player has unlocked.</summary>
        public List<string> unlockedRecipeIds = new List<string>();

        /// <summary>IDs of recipes currently offered on the active menu.</summary>
        public List<string> activeMenuRecipeIds = new List<string>();

        /// <summary>
        /// ID of the recipe designated as today's daily special.
        /// Empty string if no daily special is active.
        /// </summary>
        public string dailySpecialId = string.Empty;

        /// <summary>
        /// Parallel-list key for recipe popularity. Unity's <c>JsonUtility</c>
        /// cannot serialise <c>Dictionary</c>, so we store two aligned lists.
        /// </summary>
        public List<string> popularityRecipeIds = new List<string>();

        /// <summary>Popularity scores aligned with <see cref="popularityRecipeIds"/>.</summary>
        public List<float> popularityValues = new List<float>();

        /// <summary>
        /// Rebuilds a dictionary from the parallel-list representation.
        /// </summary>
        /// <returns>A new dictionary mapping recipe IDs to popularity scores.</returns>
        public Dictionary<string, float> GetPopularityDictionary()
        {
            Dictionary<string, float> dict = new Dictionary<string, float>();
            int count = Mathf.Min(popularityRecipeIds.Count, popularityValues.Count);
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(popularityRecipeIds[i]))
                {
                    dict[popularityRecipeIds[i]] = popularityValues[i];
                }
            }
            return dict;
        }

        /// <summary>
        /// Writes a dictionary into the parallel-list representation for serialisation.
        /// </summary>
        /// <param name="popularity">The dictionary to serialise.</param>
        public void SetPopularityDictionary(Dictionary<string, float> popularity)
        {
            popularityRecipeIds.Clear();
            popularityValues.Clear();

            if (popularity == null)
                return;

            foreach (KeyValuePair<string, float> kvp in popularity)
            {
                popularityRecipeIds.Add(kvp.Key);
                popularityValues.Add(kvp.Value);
            }
        }
    }

    /// <summary>
    /// Describes a single placed object (furniture, equipment, decoration) on the
    /// restaurant grid.
    /// </summary>
    [Serializable]
    public class PlacedObjectData
    {
        /// <summary>Unique identifier of the furniture/equipment item definition.</summary>
        public string objectId = string.Empty;

        /// <summary>Grid cell where the object is anchored (bottom-left corner).</summary>
        public Vector2Int gridPosition;

        /// <summary>Rotation in degrees (0, 90, 180, or 270).</summary>
        public int rotation;
    }

    /// <summary>
    /// Persisted layout of all placed objects on the restaurant grid.
    /// </summary>
    [Serializable]
    public class GridData
    {
        /// <summary>Every object currently placed on the grid.</summary>
        public List<PlacedObjectData> placedObjects = new List<PlacedObjectData>();
    }

    /// <summary>
    /// Persisted state of a single staff member.
    /// </summary>
    [Serializable]
    public class StaffSaveData
    {
        /// <summary>Display name of the staff member.</summary>
        public string name = string.Empty;

        /// <summary>Role type identifier (e.g. "Chef", "Waiter", "Cleaner").</summary>
        public string type = string.Empty;

        /// <summary>Current level of the staff member (1-based).</summary>
        public int level = 1;

        /// <summary>Movement / work speed multiplier (1.0 = baseline).</summary>
        public float speed = 1f;

        /// <summary>Skill rating that affects quality of work (0 to 1).</summary>
        public float skill = 0.5f;

        /// <summary>
        /// Optional specialisation identifier (e.g. a cuisine style or equipment
        /// type the staff member excels at). Empty string if none.
        /// </summary>
        public string specialization = string.Empty;
    }

    /// <summary>
    /// Persisted roster of all hired staff members.
    /// </summary>
    [Serializable]
    public class StaffData
    {
        /// <summary>All currently employed staff members.</summary>
        public List<StaffSaveData> staff = new List<StaffSaveData>();
    }

    /// <summary>
    /// Persisted statistics and progression milestones.
    /// </summary>
    [Serializable]
    public class GameProgressData
    {
        /// <summary>Lifetime number of customers served.</summary>
        public int totalCustomersServed;

        /// <summary>Current in-game day (1-based).</summary>
        public int currentDay = 1;

        /// <summary>Total real-time play time in seconds.</summary>
        public float totalPlayTime;
    }

    /// <summary>
    /// Persisted player settings and preferences.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        /// <summary>Music volume from 0 (muted) to 1 (full).</summary>
        public float musicVolume = 0.8f;

        /// <summary>Sound effects volume from 0 (muted) to 1 (full).</summary>
        public float sfxVolume = 1f;

        /// <summary>Game speed multiplier (1 = normal).</summary>
        public float gameSpeed = 1f;
    }

    // ====================================================================== //
    //  Root save-game data
    // ====================================================================== //

    /// <summary>
    /// Root serialisable class that bundles every piece of persisted game state
    /// into a single object. Used by the save system for writing to and reading
    /// from disk via <c>JsonUtility</c>.
    /// <para>
    /// The <see cref="version"/> field enables forward-compatible save migration:
    /// when the data schema changes, increment <see cref="CurrentVersion"/> and add
    /// migration logic in the save system's load path.
    /// </para>
    /// <para>
    /// <b>Backward compatibility:</b> The existing <c>GameManager</c>,
    /// <c>RestaurantManager</c>, and <c>EconomyManager</c> read/write flat fields
    /// (<c>currentDay</c>, <c>pesos</c>, <c>restaurantLevel</c>, etc.) on this
    /// class. Those fields are preserved as convenience properties that delegate
    /// to the appropriate nested data block, so no changes are required in
    /// existing manager code.
    /// </para>
    /// </summary>
    [Serializable]
    public class GameData
    {
        // ------------------------------------------------------------------ //
        //  Version
        // ------------------------------------------------------------------ //

        /// <summary>Latest schema version produced by this build of the game.</summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Schema version of this save file. Compared against
        /// <see cref="CurrentVersion"/> at load time to determine whether
        /// migration is needed.
        /// </summary>
        public int version = CurrentVersion;

        // ------------------------------------------------------------------ //
        //  Metadata
        // ------------------------------------------------------------------ //

        /// <summary>ISO-8601 timestamp of when the save was created or updated.</summary>
        public string saveTimestamp = string.Empty;

        // ------------------------------------------------------------------ //
        //  Sub-data blocks
        // ------------------------------------------------------------------ //

        /// <summary>Restaurant identity and progression.</summary>
        public RestaurantData restaurant = new RestaurantData();

        /// <summary>Currency balances and revenue tracking.</summary>
        public EconomyData economy = new EconomyData();

        /// <summary>Menu configuration and recipe popularity.</summary>
        public MenuData menu = new MenuData();

        /// <summary>Grid layout of placed objects.</summary>
        public GridData grid = new GridData();

        /// <summary>Staff roster and individual stats.</summary>
        public StaffData staff = new StaffData();

        /// <summary>Statistics and progression milestones.</summary>
        public GameProgressData progress = new GameProgressData();

        /// <summary>Player preferences and audio/speed settings.</summary>
        public SettingsData settings = new SettingsData();

        // ------------------------------------------------------------------ //
        //  Legacy flat fields
        //  Kept so that GameManager, RestaurantManager, and EconomyManager
        //  can continue using their existing WriteToData / LoadFromData
        //  contracts without modification. Each property delegates to the
        //  appropriate nested block so both representations stay in sync.
        // ------------------------------------------------------------------ //

        /// <summary>Current in-game day. Delegates to <see cref="progress"/>.</summary>
        public int currentDay
        {
            get => progress.currentDay;
            set => progress.currentDay = value;
        }

        /// <summary>Fractional progress through the current day (seconds elapsed).</summary>
        public float dayTimer;

        /// <summary>Restaurant name. Delegates to <see cref="restaurant"/>.</summary>
        public string restaurantName
        {
            get => restaurant.name;
            set => restaurant.name = value;
        }

        /// <summary>Restaurant level. Delegates to <see cref="restaurant"/>.</summary>
        public int restaurantLevel
        {
            get => restaurant.level;
            set => restaurant.level = value;
        }

        /// <summary>Restaurant experience. Delegates to <see cref="restaurant"/>.</summary>
        public int restaurantExperience
        {
            get => restaurant.experience;
            set => restaurant.experience = value;
        }

        /// <summary>Restaurant reputation. Delegates to <see cref="restaurant"/>.</summary>
        public float reputation
        {
            get => restaurant.reputation;
            set => restaurant.reputation = value;
        }

        /// <summary>Restaurant cleanliness. Delegates to <see cref="restaurant"/>.</summary>
        public float cleanliness
        {
            get => restaurant.cleanliness;
            set => restaurant.cleanliness = value;
        }

        /// <summary>Total customers served. Delegates to <see cref="progress"/>.</summary>
        public int totalCustomersServed
        {
            get => progress.totalCustomersServed;
            set => progress.totalCustomersServed = value;
        }

        /// <summary>Total revenue. Delegates to <see cref="economy"/>.</summary>
        public long totalRevenue
        {
            get => economy.totalRevenue;
            set => economy.totalRevenue = value;
        }

        /// <summary>Pesos balance. Delegates to <see cref="economy"/>.</summary>
        public long pesos
        {
            get => economy.pesos;
            set => economy.pesos = value;
        }

        /// <summary>Estrellas balance. Delegates to <see cref="economy"/>.</summary>
        public int estrellas
        {
            get => economy.estrellas;
            set => economy.estrellas = value;
        }

        // ------------------------------------------------------------------ //
        //  Factory
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Creates a fresh <see cref="GameData"/> instance populated with sensible
        /// starter values for a brand-new game. Suitable as the default when no
        /// save file is found on disk.
        /// </summary>
        /// <returns>A new <see cref="GameData"/> with default starter values.</returns>
        public static GameData CreateDefault()
        {
            GameData data = new GameData
            {
                version = CurrentVersion,
                saveTimestamp = DateTime.UtcNow.ToString("o"),
                dayTimer = 0f
            };

            // Restaurant defaults.
            data.restaurant.name = "Mi Fonda Colombiana";
            data.restaurant.level = 1;
            data.restaurant.experience = 0;
            data.restaurant.reputation = 3f;
            data.restaurant.cleanliness = 1f;

            // Economy defaults.
            data.economy.pesos = 5000;
            data.economy.estrellas = 5;
            data.economy.totalRevenue = 0;

            // Menu defaults -- start with a few basic Colombian recipes unlocked.
            data.menu.unlockedRecipeIds = new List<string>
            {
                "arepa_queso",
                "empanadas",
                "aguapanela"
            };
            data.menu.activeMenuRecipeIds = new List<string>
            {
                "arepa_queso",
                "empanadas",
                "aguapanela"
            };
            data.menu.dailySpecialId = "empanadas";

            // Grid -- empty restaurant to start; the player places their own items.
            data.grid.placedObjects = new List<PlacedObjectData>();

            // Staff -- one starter chef and one waiter.
            data.staff.staff = new List<StaffSaveData>
            {
                new StaffSaveData
                {
                    name = "Carlos",
                    type = "Chef",
                    level = 1,
                    speed = 1f,
                    skill = 0.5f,
                    specialization = string.Empty
                },
                new StaffSaveData
                {
                    name = "Maria",
                    type = "Waiter",
                    level = 1,
                    speed = 1f,
                    skill = 0.5f,
                    specialization = string.Empty
                }
            };

            // Progress.
            data.progress.currentDay = 1;
            data.progress.totalCustomersServed = 0;
            data.progress.totalPlayTime = 0f;

            // Settings.
            data.settings.musicVolume = 0.8f;
            data.settings.sfxVolume = 1f;
            data.settings.gameSpeed = 1f;

            return data;
        }
    }
}
