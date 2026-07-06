using System;
using System.Collections.Generic;
using UnityEngine;
using SaborColombiano.Core;
using SaborColombiano.Data;
using SaborColombiano.Menu;

namespace SaborColombiano.Economy
{
    /// <summary>
    /// Tracks a single batch of a perishable ingredient, recording when it was
    /// purchased so the spoilage system can expire it at the correct time.
    /// </summary>
    [Serializable]
    public class IngredientBatch
    {
        /// <summary>Ingredient identifier (ScriptableObject asset name or database ID).</summary>
        public string ingredientId;

        /// <summary>Remaining quantity in this batch.</summary>
        public int quantity;

        /// <summary>In-game day the batch was purchased.</summary>
        public int purchaseDay;

        /// <summary>
        /// Shelf life in in-game days. 0 means the ingredient never spoils.
        /// </summary>
        public int shelfLife;

        /// <summary>
        /// Returns <c>true</c> if the batch has expired on or after the given day.
        /// Non-perishable batches (shelfLife == 0) never expire.
        /// </summary>
        /// <param name="currentDay">The current in-game day.</param>
        public bool IsExpired(int currentDay)
        {
            if (shelfLife <= 0)
                return false;

            return (currentDay - purchaseDay) >= shelfLife;
        }
    }

    /// <summary>
    /// Central shop and inventory manager for Sabor Colombiano. Handles:
    /// <list type="bullet">
    ///   <item>Purchasing and selling furniture / equipment (via <see cref="FurnitureData"/> ScriptableObjects).</item>
    ///   <item>Purchasing consumable ingredients for the kitchen (via <see cref="Ingredient"/> ScriptableObjects or string IDs).</item>
    ///   <item>Tracking owned furniture counts and ingredient stock levels.</item>
    ///   <item>Ingredient spoilage over time based on shelf life using a batch system.</item>
    /// </list>
    /// <para>
    /// Integrates with <see cref="EconomyManager"/> for all monetary transactions.
    /// Also supports the string-based <see cref="ColombianRecipeDatabase"/> API
    /// for systems that do not work with ScriptableObject references directly.
    /// Subscribe to the events on this class to update UI or trigger sound effects.
    /// </para>
    /// </summary>
    public class ShopSystem : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Singleton
        // ------------------------------------------------------------------ //

        /// <summary>Global access point for the ShopSystem singleton.</summary>
        public static ShopSystem Instance { get; private set; }

        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Raised after a furniture/equipment item is successfully purchased.
        /// Parameters: item ID, purchase price.
        /// Also fires <see cref="OnFurnitureDataPurchased"/> when a <see cref="FurnitureData"/>
        /// reference is available.
        /// </summary>
        public event Action<string, int> OnItemPurchased;

        /// <summary>
        /// Raised after a furniture/equipment item is successfully sold.
        /// Parameters: item ID, sell price.
        /// Also fires <see cref="OnFurnitureDataSold"/> when a <see cref="FurnitureData"/>
        /// reference is available.
        /// </summary>
        public event Action<string, int> OnItemSold;

        /// <summary>
        /// Raised after an ingredient purchase is completed.
        /// Parameters: ingredient ID, quantity purchased.
        /// </summary>
        public event Action<string, int> OnIngredientPurchased;

        /// <summary>
        /// Raised when ingredient batches spoil during the daily spoilage check.
        /// Parameters: ingredient ID, total quantity spoiled.
        /// </summary>
        public event Action<string, int> OnIngredientSpoiled;

        /// <summary>
        /// Raised after a <see cref="FurnitureData"/>-based furniture purchase.
        /// </summary>
        public event Action<FurnitureData> OnFurnitureDataPurchased;

        /// <summary>
        /// Raised after a <see cref="FurnitureData"/>-based furniture sale.
        /// </summary>
        public event Action<FurnitureData> OnFurnitureDataSold;

        /// <summary>
        /// Raised after an <see cref="Ingredient"/>-based ingredient purchase.
        /// </summary>
        public event Action<Ingredient, int> OnIngredientAssetPurchased;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("References")]

        [SerializeField]
        [Tooltip("Reference to the EconomyManager. Auto-discovered if left empty.")]
        private EconomyManager _economyManager;

        [Header("Spoilage")]

        [SerializeField]
        [Tooltip("When true, the spoilage system runs automatically at the " +
                 "start of each new in-game day.")]
        private bool _enableSpoilage = true;

        // ------------------------------------------------------------------ //
        //  Private state -- Furniture inventory
        // ------------------------------------------------------------------ //

        /// <summary>Maps furniture item IDs to owned counts.</summary>
        private readonly Dictionary<string, int> _ownedFurniture = new Dictionary<string, int>();

        // ------------------------------------------------------------------ //
        //  Private state -- Ingredient inventory (batch system)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Master list of all ingredient batches. Each batch records quantity,
        /// purchase day, and shelf life for independent expiration tracking.
        /// </summary>
        private readonly List<IngredientBatch> _ingredientBatches = new List<IngredientBatch>();

        /// <summary>
        /// Aggregated ingredient stock cache. Rebuilt after purchases, consumption,
        /// and spoilage. Maps ingredient ID to total available quantity.
        /// </summary>
        private readonly Dictionary<string, int> _ingredientStock = new Dictionary<string, int>();

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            // Enforce singleton.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_economyManager == null)
            {
                _economyManager = FindObjectOfType<EconomyManager>();
            }

            if (_economyManager == null)
            {
                Debug.LogError("[ShopSystem] No EconomyManager found in the scene. " +
                               "Purchases will fail.");
            }
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayChanged += HandleNewDay;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayChanged -= HandleNewDay;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ================================================================== //
        //  Furniture -- ScriptableObject API
        // ================================================================== //

        /// <summary>
        /// Checks whether the player has enough Pesos to purchase the given
        /// <see cref="FurnitureData"/> item.
        /// </summary>
        /// <param name="item">Furniture item to check.</param>
        /// <returns><c>true</c> if the player can afford it.</returns>
        public bool CanAfford(FurnitureData item)
        {
            if (item == null || _economyManager == null)
                return false;

            return _economyManager.CanAfford(item.PurchasePrice);
        }

        /// <summary>
        /// Attempts to purchase a furniture/equipment item using its
        /// <see cref="FurnitureData"/> ScriptableObject reference.
        /// </summary>
        /// <param name="item">The item to purchase.</param>
        /// <returns><c>true</c> if the purchase succeeded.</returns>
        public bool PurchaseItem(FurnitureData item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ShopSystem] PurchaseItem called with null FurnitureData.");
                return false;
            }

            if (_economyManager == null)
            {
                Debug.LogError("[ShopSystem] Cannot purchase -- no EconomyManager.");
                return false;
            }

            if (!_economyManager.SpendPesos(
                    item.PurchasePrice,
                    TransactionType.FurniturePurchase,
                    $"Compra: {item.ItemName}"))
            {
                Debug.Log($"[ShopSystem] Cannot afford {item.ItemName} " +
                          $"({item.PurchasePrice} Pesos).");
                return false;
            }

            AddFurnitureToInventory(item.ItemId, 1);

            OnItemPurchased?.Invoke(item.ItemId, item.PurchasePrice);
            OnFurnitureDataPurchased?.Invoke(item);
            Debug.Log($"[ShopSystem] Purchased {item.ItemName}. " +
                      $"Now own {GetOwnedCount(item.ItemId)}.");
            return true;
        }

        /// <summary>
        /// Sells a furniture/equipment item back at half the purchase price
        /// using its <see cref="FurnitureData"/> ScriptableObject reference.
        /// </summary>
        /// <param name="item">The item to sell.</param>
        /// <returns><c>true</c> if the sale succeeded.</returns>
        public bool SellItem(FurnitureData item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ShopSystem] SellItem called with null FurnitureData.");
                return false;
            }

            if (GetOwnedCount(item.ItemId) <= 0)
            {
                Debug.Log($"[ShopSystem] Cannot sell {item.ItemName} -- none owned.");
                return false;
            }

            if (_economyManager == null)
            {
                Debug.LogError("[ShopSystem] Cannot sell -- no EconomyManager.");
                return false;
            }

            RemoveFurnitureFromInventory(item.ItemId, 1);

            _economyManager.AddPesos(
                item.SellPrice,
                TransactionType.Other,
                $"Venta: {item.ItemName}");

            OnItemSold?.Invoke(item.ItemId, item.SellPrice);
            OnFurnitureDataSold?.Invoke(item);
            Debug.Log($"[ShopSystem] Sold {item.ItemName} for {item.SellPrice} Pesos.");
            return true;
        }

        // ================================================================== //
        //  Furniture -- String-ID API (ColombianRecipeDatabase)
        // ================================================================== //

        /// <summary>
        /// Purchases a furniture item using its string ID from
        /// <see cref="ColombianRecipeDatabase"/>.
        /// </summary>
        /// <param name="itemId">Database furniture ID.</param>
        /// <returns><c>true</c> if the purchase succeeded.</returns>
        public bool PurchaseFurniture(string itemId)
        {
            ColombianRecipeDatabase.FurnitureInfo? info = ColombianRecipeDatabase.GetFurniture(itemId);
            if (!info.HasValue)
            {
                Debug.LogWarning($"[ShopSystem] Unknown furniture: {itemId}");
                return false;
            }

            if (_economyManager == null || !_economyManager.CanAfford(info.Value.price))
            {
                Debug.Log($"[ShopSystem] Not enough pesos for {info.Value.name} (${info.Value.price})");
                return false;
            }

            _economyManager.SpendPesos(
                info.Value.price,
                TransactionType.FurniturePurchase,
                $"Compra: {info.Value.name}");

            AddFurnitureToInventory(itemId, 1);

            Debug.Log($"[ShopSystem] Purchased {info.Value.name} for ${info.Value.price}");
            OnItemPurchased?.Invoke(itemId, info.Value.price);
            return true;
        }

        /// <summary>
        /// Sells a furniture item using its string ID. Refunds half the purchase price.
        /// </summary>
        /// <param name="itemId">Database furniture ID.</param>
        /// <returns><c>true</c> if the sale succeeded.</returns>
        public bool SellFurniture(string itemId)
        {
            if (!_ownedFurniture.ContainsKey(itemId) || _ownedFurniture[itemId] <= 0)
            {
                Debug.Log($"[ShopSystem] No {itemId} in inventory to sell.");
                return false;
            }

            ColombianRecipeDatabase.FurnitureInfo? info = ColombianRecipeDatabase.GetFurniture(itemId);
            int sellPrice = info.HasValue ? info.Value.price / 2 : 0;

            RemoveFurnitureFromInventory(itemId, 1);

            if (_economyManager != null && sellPrice > 0)
            {
                _economyManager.AddPesos(
                    sellPrice,
                    TransactionType.Other,
                    $"Venta: {(info.HasValue ? info.Value.name : itemId)}");
            }

            Debug.Log($"[ShopSystem] Sold {itemId} for ${sellPrice}");
            OnItemSold?.Invoke(itemId, sellPrice);
            return true;
        }

        /// <summary>
        /// Checks if the player can afford a furniture item by its database ID.
        /// </summary>
        /// <param name="itemId">Database furniture ID.</param>
        /// <returns><c>true</c> if the player can afford it.</returns>
        public bool CanAffordFurniture(string itemId)
        {
            ColombianRecipeDatabase.FurnitureInfo? info = ColombianRecipeDatabase.GetFurniture(itemId);
            if (!info.HasValue || _economyManager == null)
                return false;
            return _economyManager.CanAfford(info.Value.price);
        }

        // ================================================================== //
        //  Furniture -- Shared inventory helpers
        // ================================================================== //

        /// <summary>Adds furniture directly to the inventory (e.g. starter items).</summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="count">Number of units to add.</param>
        public void AddFurnitureToInventory(string itemId, int count)
        {
            if (_ownedFurniture.ContainsKey(itemId))
                _ownedFurniture[itemId] += count;
            else
                _ownedFurniture[itemId] = count;
        }

        /// <summary>Removes furniture from inventory (e.g. when placed on grid).</summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="count">Number of units to remove.</param>
        /// <returns><c>true</c> if the removal was successful.</returns>
        public bool RemoveFurnitureFromInventory(string itemId, int count = 1)
        {
            if (!_ownedFurniture.ContainsKey(itemId) || _ownedFurniture[itemId] < count)
                return false;

            _ownedFurniture[itemId] -= count;
            if (_ownedFurniture[itemId] <= 0)
                _ownedFurniture.Remove(itemId);
            return true;
        }

        /// <summary>
        /// Returns the number of units of the given item the player currently owns.
        /// Works with both <see cref="FurnitureData"/> item IDs and database IDs.
        /// </summary>
        /// <param name="itemId">Furniture item ID.</param>
        /// <returns>Owned count (0 if none).</returns>
        public int GetOwnedCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return 0;
            return _ownedFurniture.TryGetValue(itemId, out int count) ? count : 0;
        }

        /// <summary>Alias for <see cref="GetOwnedCount"/> for backward compatibility.</summary>
        public int GetFurnitureCount(string itemId) => GetOwnedCount(itemId);

        /// <summary>Returns a copy of the entire furniture inventory dictionary.</summary>
        public Dictionary<string, int> GetAllFurniture()
        {
            return new Dictionary<string, int>(_ownedFurniture);
        }

        // ================================================================== //
        //  Ingredients -- ScriptableObject API
        // ================================================================== //

        /// <summary>
        /// Purchases a quantity of a consumable ingredient using its
        /// <see cref="Ingredient"/> ScriptableObject reference. Creates a new
        /// <see cref="IngredientBatch"/> for spoilage tracking.
        /// </summary>
        /// <param name="ingredient">The ingredient to buy.</param>
        /// <param name="quantity">Number of units to purchase (must be > 0).</param>
        /// <returns><c>true</c> if the purchase succeeded.</returns>
        public bool PurchaseIngredient(Ingredient ingredient, int quantity)
        {
            if (ingredient == null)
            {
                Debug.LogWarning("[ShopSystem] PurchaseIngredient called with null Ingredient.");
                return false;
            }

            if (quantity <= 0)
            {
                Debug.LogWarning("[ShopSystem] PurchaseIngredient called with non-positive quantity.");
                return false;
            }

            if (_economyManager == null)
            {
                Debug.LogError("[ShopSystem] Cannot purchase ingredient -- no EconomyManager.");
                return false;
            }

            long totalCost = (long)ingredient.PurchasePrice * quantity;

            if (!_economyManager.SpendPesos(
                    totalCost,
                    TransactionType.MenuPurchase,
                    $"Ingredientes: {quantity}x {ingredient.IngredientName}"))
            {
                Debug.Log($"[ShopSystem] Cannot afford {quantity}x {ingredient.IngredientName} " +
                          $"({totalCost} Pesos).");
                return false;
            }

            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            IngredientBatch batch = new IngredientBatch
            {
                ingredientId = ingredient.name,
                quantity = quantity,
                purchaseDay = currentDay,
                shelfLife = ingredient.ShelfLife
            };
            _ingredientBatches.Add(batch);
            RebuildIngredientStock();

            OnIngredientPurchased?.Invoke(ingredient.name, quantity);
            OnIngredientAssetPurchased?.Invoke(ingredient, quantity);
            Debug.Log($"[ShopSystem] Purchased {quantity}x {ingredient.IngredientName}. " +
                      $"Stock: {GetIngredientStock(ingredient.name)}.");
            return true;
        }

        // ================================================================== //
        //  Ingredients -- String-ID API (ColombianRecipeDatabase)
        // ================================================================== //

        /// <summary>
        /// Purchases ingredients using a database string ID from
        /// <see cref="ColombianRecipeDatabase"/>.
        /// </summary>
        /// <param name="ingredientId">Database ingredient ID.</param>
        /// <param name="quantity">Number of units to purchase.</param>
        /// <returns><c>true</c> if the purchase succeeded.</returns>
        public bool PurchaseIngredient(string ingredientId, int quantity = 1)
        {
            ColombianRecipeDatabase.IngredientInfo? info = ColombianRecipeDatabase.GetIngredient(ingredientId);
            if (!info.HasValue)
            {
                Debug.LogWarning($"[ShopSystem] Unknown ingredient: {ingredientId}");
                return false;
            }

            if (quantity <= 0)
            {
                Debug.LogWarning("[ShopSystem] PurchaseIngredient called with non-positive quantity.");
                return false;
            }

            int totalCost = info.Value.price * quantity;

            if (_economyManager == null || !_economyManager.CanAfford(totalCost))
            {
                Debug.Log($"[ShopSystem] Not enough pesos for {quantity}x {info.Value.name} (${totalCost})");
                return false;
            }

            _economyManager.SpendPesos(
                totalCost,
                TransactionType.MenuPurchase,
                $"Ingredientes: {quantity}x {info.Value.name}");

            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            IngredientBatch batch = new IngredientBatch
            {
                ingredientId = ingredientId,
                quantity = quantity,
                purchaseDay = currentDay,
                shelfLife = info.Value.shelfLifeDays
            };
            _ingredientBatches.Add(batch);
            RebuildIngredientStock();

            Debug.Log($"[ShopSystem] Purchased {quantity}x {info.Value.name} for ${totalCost}");
            OnIngredientPurchased?.Invoke(ingredientId, quantity);
            return true;
        }

        // ================================================================== //
        //  Ingredients -- Stock queries and consumption
        // ================================================================== //

        /// <summary>
        /// Checks whether the inventory contains at least the specified quantity
        /// of the given ingredient (across all non-expired batches).
        /// </summary>
        /// <param name="ingredientId">Ingredient identifier.</param>
        /// <param name="quantity">Required minimum quantity.</param>
        /// <returns><c>true</c> if stock is sufficient.</returns>
        public bool HasIngredient(string ingredientId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(ingredientId) || quantity <= 0)
                return false;

            return GetIngredientStock(ingredientId) >= quantity;
        }

        /// <summary>
        /// Returns the total available quantity of the given ingredient across
        /// all non-expired batches.
        /// </summary>
        /// <param name="ingredientId">Ingredient identifier.</param>
        /// <returns>Available quantity (0 if none).</returns>
        public int GetIngredientStock(string ingredientId)
        {
            if (string.IsNullOrEmpty(ingredientId))
                return 0;

            return _ingredientStock.TryGetValue(ingredientId, out int stock) ? stock : 0;
        }

        /// <summary>
        /// Consumes (removes) a specified quantity of an ingredient from the
        /// inventory. Uses oldest batches first (FIFO) to minimise waste.
        /// </summary>
        /// <param name="ingredientId">Ingredient identifier.</param>
        /// <param name="quantity">Quantity to consume (must be > 0).</param>
        /// <returns><c>true</c> if the full quantity was consumed successfully.</returns>
        public bool ConsumeIngredient(string ingredientId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(ingredientId) || quantity <= 0)
                return false;

            if (!HasIngredient(ingredientId, quantity))
            {
                Debug.Log($"[ShopSystem] Insufficient stock of '{ingredientId}' " +
                          $"(need {quantity}, have {GetIngredientStock(ingredientId)}).");
                return false;
            }

            int remaining = quantity;

            // Consume from oldest batches first (FIFO).
            for (int i = 0; i < _ingredientBatches.Count && remaining > 0; i++)
            {
                IngredientBatch batch = _ingredientBatches[i];
                if (batch.ingredientId != ingredientId)
                    continue;

                int take = Mathf.Min(batch.quantity, remaining);
                batch.quantity -= take;
                remaining -= take;
            }

            // Remove fully depleted batches.
            _ingredientBatches.RemoveAll(b => b.quantity <= 0);
            RebuildIngredientStock();
            return true;
        }

        /// <summary>Returns a copy of the entire ingredient stock dictionary.</summary>
        public Dictionary<string, int> GetAllIngredients()
        {
            return new Dictionary<string, int>(_ingredientStock);
        }

        /// <summary>
        /// Adds ingredients directly to the inventory without purchasing (e.g. starter items).
        /// Creates a non-perishable batch.
        /// </summary>
        /// <param name="ingredientId">Ingredient identifier.</param>
        /// <param name="quantity">Number of units to add.</param>
        public void AddIngredientToInventory(string ingredientId, int quantity)
        {
            if (string.IsNullOrEmpty(ingredientId) || quantity <= 0)
                return;

            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            IngredientBatch batch = new IngredientBatch
            {
                ingredientId = ingredientId,
                quantity = quantity,
                purchaseDay = currentDay,
                shelfLife = 0 // non-perishable for gifted items
            };
            _ingredientBatches.Add(batch);
            RebuildIngredientStock();
        }

        /// <summary>
        /// Checks if all ingredients for a recipe from the
        /// <see cref="ColombianRecipeDatabase"/> are available.
        /// </summary>
        /// <param name="recipeId">Database recipe ID.</param>
        /// <returns><c>true</c> if all ingredients are in stock.</returns>
        public bool HasIngredientsForRecipe(string recipeId)
        {
            ColombianRecipeDatabase.RecipeInfo? recipe = ColombianRecipeDatabase.GetRecipe(recipeId);
            if (!recipe.HasValue)
                return false;

            foreach (ColombianRecipeDatabase.IngredientRequirement req in recipe.Value.ingredients)
            {
                if (!HasIngredient(req.ingredientId, req.quantity))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Consumes all ingredients for a recipe from the database.
        /// </summary>
        /// <param name="recipeId">Database recipe ID.</param>
        /// <returns><c>true</c> if all ingredients were consumed.</returns>
        public bool ConsumeIngredientsForRecipe(string recipeId)
        {
            if (!HasIngredientsForRecipe(recipeId))
                return false;

            ColombianRecipeDatabase.RecipeInfo? recipe = ColombianRecipeDatabase.GetRecipe(recipeId);
            if (!recipe.HasValue)
                return false;

            foreach (ColombianRecipeDatabase.IngredientRequirement req in recipe.Value.ingredients)
            {
                ConsumeIngredient(req.ingredientId, req.quantity);
            }
            return true;
        }

        // ================================================================== //
        //  Spoilage
        // ================================================================== //

        /// <summary>
        /// Processes ingredient spoilage for the current day. Expired batches
        /// are removed and <see cref="OnIngredientSpoiled"/> is fired for each
        /// affected ingredient type.
        /// </summary>
        public void ProcessSpoilage()
        {
            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            Dictionary<string, int> spoiledAmounts = new Dictionary<string, int>();

            for (int i = _ingredientBatches.Count - 1; i >= 0; i--)
            {
                IngredientBatch batch = _ingredientBatches[i];
                if (batch.IsExpired(currentDay))
                {
                    if (spoiledAmounts.ContainsKey(batch.ingredientId))
                        spoiledAmounts[batch.ingredientId] += batch.quantity;
                    else
                        spoiledAmounts[batch.ingredientId] = batch.quantity;

                    _ingredientBatches.RemoveAt(i);
                }
            }

            if (spoiledAmounts.Count > 0)
            {
                RebuildIngredientStock();

                foreach (KeyValuePair<string, int> kvp in spoiledAmounts)
                {
                    OnIngredientSpoiled?.Invoke(kvp.Key, kvp.Value);
                    Debug.Log($"[ShopSystem] {kvp.Value}x '{kvp.Key}' se echo a perder (day {currentDay}).");
                }
            }
        }

        /// <summary>Alias for <see cref="ProcessSpoilage"/> for backward compatibility.</summary>
        public void ProcessDailySpoilage()
        {
            ProcessSpoilage();
        }

        // ------------------------------------------------------------------ //
        //  Day change handler
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called at the start of each new in-game day via
        /// <see cref="GameManager.OnDayChanged"/>.
        /// </summary>
        /// <param name="newDay">The new day number.</param>
        private void HandleNewDay(int newDay)
        {
            if (_enableSpoilage)
            {
                ProcessSpoilage();
            }
        }

        // ------------------------------------------------------------------ //
        //  Internal helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Rebuilds the aggregated stock cache from the raw batch list.
        /// Must be called after any mutation to <see cref="_ingredientBatches"/>.
        /// </summary>
        private void RebuildIngredientStock()
        {
            _ingredientStock.Clear();

            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

            foreach (IngredientBatch batch in _ingredientBatches)
            {
                if (batch.IsExpired(currentDay))
                    continue;

                if (_ingredientStock.ContainsKey(batch.ingredientId))
                    _ingredientStock[batch.ingredientId] += batch.quantity;
                else
                    _ingredientStock[batch.ingredientId] = batch.quantity;
            }
        }

        // ================================================================== //
        //  Serialisation
        // ================================================================== //

        /// <summary>
        /// Serialisable save data for the shop inventory.
        /// Supports both furniture and ingredient batch persistence.
        /// </summary>
        [Serializable]
        public class InventorySaveData
        {
            /// <summary>Furniture item IDs (parallel list key).</summary>
            public List<string> furnitureIds = new List<string>();

            /// <summary>Furniture owned counts (parallel list value).</summary>
            public List<int> furnitureCounts = new List<int>();

            /// <summary>Ingredient item IDs (parallel list key, legacy flat inventory).</summary>
            public List<string> ingredientIds = new List<string>();

            /// <summary>Ingredient quantities (parallel list value, legacy flat inventory).</summary>
            public List<int> ingredientCounts = new List<int>();

            /// <summary>Full batch data for the upgraded spoilage system.</summary>
            public List<IngredientBatch> ingredientBatches = new List<IngredientBatch>();
        }

        /// <summary>
        /// Builds a serialisable snapshot of the current inventory state.
        /// </summary>
        /// <returns>A new <see cref="InventorySaveData"/> instance.</returns>
        public InventorySaveData GetSaveData()
        {
            InventorySaveData data = new InventorySaveData();

            // Furniture.
            foreach (KeyValuePair<string, int> kvp in _ownedFurniture)
            {
                data.furnitureIds.Add(kvp.Key);
                data.furnitureCounts.Add(kvp.Value);
            }

            // Ingredients (flat view for backward compatibility).
            foreach (KeyValuePair<string, int> kvp in _ingredientStock)
            {
                data.ingredientIds.Add(kvp.Key);
                data.ingredientCounts.Add(kvp.Value);
            }

            // Batches (full spoilage data).
            foreach (IngredientBatch batch in _ingredientBatches)
            {
                data.ingredientBatches.Add(new IngredientBatch
                {
                    ingredientId = batch.ingredientId,
                    quantity = batch.quantity,
                    purchaseDay = batch.purchaseDay,
                    shelfLife = batch.shelfLife
                });
            }

            return data;
        }

        /// <summary>
        /// Restores the inventory state from a previously saved snapshot.
        /// If batch data is available it takes precedence over the flat lists.
        /// </summary>
        /// <param name="data">Saved inventory data.</param>
        public void LoadSaveData(InventorySaveData data)
        {
            _ownedFurniture.Clear();
            _ingredientBatches.Clear();

            if (data == null)
            {
                RebuildIngredientStock();
                return;
            }

            // Furniture.
            for (int i = 0; i < data.furnitureIds.Count && i < data.furnitureCounts.Count; i++)
            {
                _ownedFurniture[data.furnitureIds[i]] = data.furnitureCounts[i];
            }

            // Ingredients: prefer batch data when available.
            if (data.ingredientBatches != null && data.ingredientBatches.Count > 0)
            {
                _ingredientBatches.AddRange(data.ingredientBatches);
            }
            else
            {
                // Fall back to flat lists (legacy saves).
                int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
                for (int i = 0; i < data.ingredientIds.Count && i < data.ingredientCounts.Count; i++)
                {
                    _ingredientBatches.Add(new IngredientBatch
                    {
                        ingredientId = data.ingredientIds[i],
                        quantity = data.ingredientCounts[i],
                        purchaseDay = currentDay,
                        shelfLife = 0 // unknown shelf life from legacy save; treat as non-perishable
                    });
                }
            }

            RebuildIngredientStock();
        }

        /// <summary>
        /// Returns a snapshot of all ingredient batches, suitable for external
        /// save systems that manage their own serialisation.
        /// </summary>
        public List<IngredientBatch> GetIngredientBatchesSnapshot()
        {
            List<IngredientBatch> snapshot = new List<IngredientBatch>(_ingredientBatches.Count);
            foreach (IngredientBatch batch in _ingredientBatches)
            {
                snapshot.Add(new IngredientBatch
                {
                    ingredientId = batch.ingredientId,
                    quantity = batch.quantity,
                    purchaseDay = batch.purchaseDay,
                    shelfLife = batch.shelfLife
                });
            }
            return snapshot;
        }

        /// <summary>
        /// Restores ingredient inventory from a previously saved list of batches.
        /// </summary>
        /// <param name="batches">Saved batches to restore.</param>
        public void LoadIngredientBatches(List<IngredientBatch> batches)
        {
            _ingredientBatches.Clear();
            if (batches != null)
            {
                _ingredientBatches.AddRange(batches);
            }
            RebuildIngredientStock();
        }
    }
}
