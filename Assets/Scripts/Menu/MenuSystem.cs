using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaborColombiano.Menu
{
    /// <summary>
    /// Central manager for the restaurant's menu. Tracks which recipes have been
    /// unlocked, which dishes are actively served, daily specials, and per-recipe
    /// popularity statistics. Attach to a persistent manager GameObject.
    /// <para>
    /// Designed to integrate with:
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>SaborColombiano.Core.GameManager</c> -- supplies the current
    ///     restaurant level and day/night cycle callbacks.
    ///   </description></item>
    ///   <item><description>
    ///     <c>SaborColombiano.Data.InventoryManager</c> -- queried to verify
    ///     ingredient availability before cooking.
    ///   </description></item>
    ///   <item><description>
    ///     <c>SaborColombiano.Grid.GridManager</c> -- queried to find
    ///     placed <see cref="CookingStation"/> objects matching the
    ///     recipe's equipment requirement.
    ///   </description></item>
    /// </list>
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSystem : MonoBehaviour
    {
        // ================================================================== //
        //  Inspector Configuration
        // ================================================================== //

        /// <summary>
        /// Base number of menu slots available at restaurant level 1.
        /// One additional slot is granted per level above 1.
        /// </summary>
        [Header("Configuration")]
        [SerializeField]
        [Min(1)]
        [Tooltip("Menu slots at level 1. Grows by 1 per additional level.")]
        private int baseMenuSlots = 4;

        /// <summary>Maximum menu slots the player can ever have.</summary>
        [SerializeField]
        [Min(1)]
        [Tooltip("Hard cap on active menu slots regardless of level.")]
        private int maxMenuSlots = 16;

        /// <summary>
        /// Bonus pesos multiplier applied to the daily special's selling price.
        /// E.g. 1.25 means +25 % revenue.
        /// </summary>
        [Header("Daily Special")]
        [SerializeField]
        [Min(1f)]
        [Tooltip("Revenue multiplier for the daily special dish.")]
        private float dailySpecialMultiplier = 1.25f;

        // ================================================================== //
        //  Events
        // ================================================================== //

        /// <summary>Fired when a new recipe is permanently unlocked.</summary>
        public event Action<Recipe> OnRecipeUnlocked;

        /// <summary>
        /// Fired when the active menu changes (dish added or removed).
        /// Subscribers receive the full list of currently active recipes.
        /// </summary>
        public event Action<IReadOnlyList<Recipe>> OnMenuChanged;

        /// <summary>Fired when the daily special is set or rotated.</summary>
        public event Action<Recipe> OnDailySpecialSet;

        // ================================================================== //
        //  Runtime State
        // ================================================================== //

        /// <summary>All recipes the player has unlocked so far.</summary>
        private readonly List<Recipe> unlockedRecipes = new List<Recipe>();

        /// <summary>Subset of unlocked recipes currently on the active menu.</summary>
        private readonly List<Recipe> activeMenu = new List<Recipe>();

        /// <summary>
        /// Popularity tracker -- maps recipe instance ID to order count.
        /// Persisted through <c>SaborColombiano.Data.SaveManager</c>.
        /// </summary>
        private readonly Dictionary<int, int> popularityMap = new Dictionary<int, int>();

        /// <summary>Current daily special, or <c>null</c> if none is set.</summary>
        private Recipe dailySpecial;

        /// <summary>
        /// Cached restaurant level used to compute available menu slots.
        /// Updated externally via <see cref="SetRestaurantLevel"/>.
        /// </summary>
        private int currentRestaurantLevel = 1;

        // ================================================================== //
        //  Public Properties
        // ================================================================== //

        /// <summary>Read-only view of every unlocked recipe.</summary>
        public IReadOnlyList<Recipe> UnlockedRecipes => unlockedRecipes;

        /// <summary>Read-only view of the active (served) menu.</summary>
        public IReadOnlyList<Recipe> ActiveMenu => activeMenu;

        /// <summary>The dish currently featured as the daily special.</summary>
        public Recipe DailySpecial => dailySpecial;

        /// <summary>Revenue multiplier applied to the daily special.</summary>
        public float DailySpecialMultiplier => dailySpecialMultiplier;

        /// <summary>
        /// Number of menu slots available at the current restaurant level.
        /// </summary>
        public int AvailableMenuSlots =>
            Mathf.Min(baseMenuSlots + (currentRestaurantLevel - 1), maxMenuSlots);

        /// <summary>Number of free (unused) menu slots.</summary>
        public int FreeMenuSlots => Mathf.Max(0, AvailableMenuSlots - activeMenu.Count);

        // ================================================================== //
        //  Level Integration
        // ================================================================== //

        /// <summary>
        /// Called by <c>SaborColombiano.Core.GameManager</c> whenever the
        /// restaurant levels up so that menu slot calculations stay current.
        /// </summary>
        /// <param name="level">New restaurant level (1-based).</param>
        public void SetRestaurantLevel(int level)
        {
            currentRestaurantLevel = Mathf.Max(1, level);
        }

        // ================================================================== //
        //  Recipe Unlock
        // ================================================================== //

        /// <summary>
        /// Permanently unlocks a recipe, making it available for the active menu.
        /// Does nothing if the recipe is already unlocked or is <c>null</c>.
        /// </summary>
        /// <param name="recipe">Recipe to unlock.</param>
        /// <returns><c>true</c> if the recipe was newly unlocked.</returns>
        public bool UnlockRecipe(Recipe recipe)
        {
            if (recipe == null)
            {
                Debug.LogWarning("[MenuSystem] Attempted to unlock a null recipe.");
                return false;
            }

            if (unlockedRecipes.Contains(recipe))
            {
                return false;
            }

            unlockedRecipes.Add(recipe);
            OnRecipeUnlocked?.Invoke(recipe);
            return true;
        }

        /// <summary>
        /// Checks whether a recipe has been unlocked.
        /// </summary>
        /// <param name="recipe">Recipe to test.</param>
        /// <returns><c>true</c> if unlocked.</returns>
        public bool IsUnlocked(Recipe recipe)
        {
            return recipe != null && unlockedRecipes.Contains(recipe);
        }

        // ================================================================== //
        //  Active Menu Management
        // ================================================================== //

        /// <summary>
        /// Adds a recipe to the active menu (what customers can order).
        /// The recipe must already be unlocked and there must be a free slot.
        /// </summary>
        /// <param name="recipe">Recipe to add.</param>
        /// <returns><c>true</c> if the recipe was added to the menu.</returns>
        public bool AddToMenu(Recipe recipe)
        {
            if (recipe == null)
            {
                Debug.LogWarning("[MenuSystem] Attempted to add a null recipe to the menu.");
                return false;
            }

            if (!unlockedRecipes.Contains(recipe))
            {
                Debug.LogWarning(
                    $"[MenuSystem] Cannot add '{recipe.RecipeName}' -- recipe is not unlocked.");
                return false;
            }

            if (activeMenu.Contains(recipe))
            {
                Debug.LogWarning(
                    $"[MenuSystem] '{recipe.RecipeName}' is already on the active menu.");
                return false;
            }

            if (activeMenu.Count >= AvailableMenuSlots)
            {
                Debug.LogWarning(
                    "[MenuSystem] No free menu slots. Remove a dish or level up the restaurant.");
                return false;
            }

            activeMenu.Add(recipe);
            OnMenuChanged?.Invoke(activeMenu);
            return true;
        }

        /// <summary>
        /// Removes a recipe from the active menu. If the removed recipe was
        /// the daily special, the special is cleared.
        /// </summary>
        /// <param name="recipe">Recipe to remove.</param>
        /// <returns><c>true</c> if the recipe was removed.</returns>
        public bool RemoveFromMenu(Recipe recipe)
        {
            if (recipe == null)
            {
                return false;
            }

            bool removed = activeMenu.Remove(recipe);

            if (removed)
            {
                // Clear daily special if we just removed it.
                if (dailySpecial == recipe)
                {
                    dailySpecial = null;
                }

                OnMenuChanged?.Invoke(activeMenu);
            }

            return removed;
        }

        /// <summary>
        /// Returns <c>true</c> if the recipe is currently on the active menu.
        /// </summary>
        /// <param name="recipe">Recipe to check.</param>
        public bool IsOnMenu(Recipe recipe)
        {
            return recipe != null && activeMenu.Contains(recipe);
        }

        /// <summary>
        /// Returns the active menu as a read-only list.
        /// Convenience wrapper matching the specification signature.
        /// </summary>
        /// <returns>The active menu.</returns>
        public IReadOnlyList<Recipe> GetActiveMenu()
        {
            return activeMenu;
        }

        // ================================================================== //
        //  Popularity Tracking
        // ================================================================== //

        /// <summary>
        /// Records that a customer ordered <paramref name="recipe"/>.
        /// Call this from the order / serving pipeline.
        /// </summary>
        /// <param name="recipe">The ordered recipe.</param>
        public void RecordOrder(Recipe recipe)
        {
            if (recipe == null) return;

            int id = recipe.GetInstanceID();
            if (popularityMap.ContainsKey(id))
            {
                popularityMap[id]++;
            }
            else
            {
                popularityMap[id] = 1;
            }
        }

        /// <summary>
        /// Returns the total number of times <paramref name="recipe"/> has been ordered.
        /// </summary>
        /// <param name="recipe">Recipe to query.</param>
        /// <returns>Order count, or 0 if never ordered / null.</returns>
        public int GetPopularity(Recipe recipe)
        {
            if (recipe == null) return 0;
            popularityMap.TryGetValue(recipe.GetInstanceID(), out int count);
            return count;
        }

        /// <summary>
        /// Returns the most popular recipe on the active menu, or <c>null</c>
        /// if no orders have been recorded.
        /// </summary>
        public Recipe GetMostPopularDish()
        {
            Recipe best = null;
            int bestCount = -1;

            for (int i = 0; i < activeMenu.Count; i++)
            {
                int count = GetPopularity(activeMenu[i]);
                if (count > bestCount)
                {
                    bestCount = count;
                    best = activeMenu[i];
                }
            }

            return best;
        }

        /// <summary>
        /// Resets all popularity counters (e.g. at the start of a new week).
        /// </summary>
        public void ResetPopularity()
        {
            popularityMap.Clear();
        }

        // ================================================================== //
        //  Daily Specials
        // ================================================================== //

        /// <summary>
        /// Sets a recipe as the daily special. The dish must already be on the
        /// active menu. Customers ordering the daily special generate bonus
        /// revenue equal to <see cref="DailySpecialMultiplier"/>.
        /// </summary>
        /// <param name="recipe">Recipe to feature. Pass <c>null</c> to clear.</param>
        /// <returns><c>true</c> if the special was set (or cleared).</returns>
        public bool SetDailySpecial(Recipe recipe)
        {
            if (recipe != null && !activeMenu.Contains(recipe))
            {
                Debug.LogWarning(
                    $"[MenuSystem] Cannot set '{recipe.RecipeName}' as daily special -- " +
                    "it is not on the active menu.");
                return false;
            }

            dailySpecial = recipe;
            OnDailySpecialSet?.Invoke(recipe);
            return true;
        }

        /// <summary>
        /// Picks a random active-menu dish as the daily special. Useful for
        /// auto-rotation at the start of each in-game day.
        /// </summary>
        /// <returns>The selected recipe, or <c>null</c> if the menu is empty.</returns>
        public Recipe PickRandomDailySpecial()
        {
            if (activeMenu.Count == 0)
            {
                dailySpecial = null;
                OnDailySpecialSet?.Invoke(null);
                return null;
            }

            Recipe picked = activeMenu[UnityEngine.Random.Range(0, activeMenu.Count)];
            SetDailySpecial(picked);
            return picked;
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="recipe"/> is the current daily special.
        /// </summary>
        public bool IsDailySpecial(Recipe recipe)
        {
            return recipe != null && dailySpecial == recipe;
        }

        /// <summary>
        /// Calculates the effective selling price for a recipe, applying the
        /// daily-special multiplier when appropriate.
        /// </summary>
        /// <param name="recipe">The recipe being sold.</param>
        /// <returns>Effective price in pesos.</returns>
        public int GetEffectivePrice(Recipe recipe)
        {
            if (recipe == null) return 0;

            float multiplier = IsDailySpecial(recipe) ? dailySpecialMultiplier : 1f;
            return Mathf.RoundToInt(recipe.SellingPrice * multiplier);
        }

        // ================================================================== //
        //  Kitchen Readiness Check
        // ================================================================== //

        /// <summary>
        /// Determines whether the kitchen can currently prepare the given recipe.
        /// Checks both ingredient availability (via a callback) and whether a
        /// matching <see cref="CookingStation"/> exists and is idle.
        /// <para>
        /// Because the <c>InventoryManager</c> and <c>GridManager</c> live in
        /// other namespaces, readiness checks are resolved through injectable
        /// delegates so that this class remains decoupled.
        /// </para>
        /// </summary>
        /// <param name="recipe">Recipe to evaluate.</param>
        /// <param name="hasIngredients">
        /// Delegate that returns <c>true</c> when the inventory contains
        /// all required ingredients in sufficient quantity. Typically bound
        /// to <c>InventoryManager.HasIngredients</c>.
        /// </param>
        /// <param name="hasEquipment">
        /// Delegate that returns <c>true</c> when at least one
        /// <see cref="CookingStation"/> of the required type is placed on the
        /// grid and is in the <see cref="CookingStationState.Idle"/> state.
        /// Typically bound to <c>GridManager.HasAvailableStation</c>.
        /// </param>
        /// <returns><c>true</c> if both ingredients and equipment are available.</returns>
        public bool CanCookRecipe(
            Recipe recipe,
            Func<IReadOnlyList<IngredientAmount>, bool> hasIngredients,
            Func<EquipmentType, bool> hasEquipment)
        {
            if (recipe == null) return false;

            // Equipment check.
            if (recipe.RequiredEquipment != EquipmentType.None)
            {
                if (hasEquipment == null || !hasEquipment(recipe.RequiredEquipment))
                {
                    return false;
                }
            }

            // Ingredient check.
            if (hasIngredients == null || !hasIngredients(recipe.RequiredIngredients))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Simplified readiness check that only verifies the recipe is on the
        /// menu and uses the registered station list. Suitable for quick UI
        /// greying-out without full inventory queries.
        /// </summary>
        /// <param name="recipe">Recipe to evaluate.</param>
        /// <param name="availableStations">
        /// Set of equipment types currently placed and idle on the grid.
        /// </param>
        /// <returns><c>true</c> if the recipe is on the menu and a station matches.</returns>
        public bool CanCookRecipeSimple(Recipe recipe, HashSet<EquipmentType> availableStations)
        {
            if (recipe == null || !activeMenu.Contains(recipe))
            {
                return false;
            }

            if (recipe.RequiredEquipment == EquipmentType.None)
            {
                return true;
            }

            return availableStations != null && availableStations.Contains(recipe.RequiredEquipment);
        }
    }
}
