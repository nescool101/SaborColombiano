using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SaborColombiano.Core;

namespace SaborColombiano.UI
{
    // ====================================================================== //
    //  Data types
    // ====================================================================== //

    /// <summary>
    /// Top-level shop categories that map to the tab bar in the shop UI.
    /// </summary>
    public enum ShopCategory
    {
        /// <summary>Tables, chairs, counters, cash registers.</summary>
        Muebles,
        /// <summary>Stoves, ovens, fridges, wash stations.</summary>
        Equipos,
        /// <summary>Paintings, plants, rugs, wall art.</summary>
        Decoracion,
        /// <summary>Cooking ingredients purchased from the supplier.</summary>
        Ingredientes
    }

    /// <summary>
    /// Specifies which currency an item costs.
    /// </summary>
    public enum CurrencyType
    {
        /// <summary>Standard in-game currency.</summary>
        Pesos,
        /// <summary>Premium currency.</summary>
        Estrellas
    }

    /// <summary>
    /// Filter modes for the shop item grid.
    /// </summary>
    public enum ShopFilter
    {
        /// <summary>Show all items in the category.</summary>
        All,
        /// <summary>Show only items the player can currently afford.</summary>
        Affordable,
        /// <summary>Show only newly unlocked items.</summary>
        New
    }

    /// <summary>
    /// Immutable data container describing a single purchasable item in the
    /// shop. Instances are typically loaded from ScriptableObject assets or
    /// built at runtime from configuration data.
    /// </summary>
    [Serializable]
    public struct ShopItemData
    {
        /// <summary>Display name shown in the shop grid and detail popup.</summary>
        [Tooltip("Display name of the item.")]
        public string name;

        /// <summary>Flavour or mechanical description shown in the detail popup.</summary>
        [Tooltip("Description shown in the item detail popup.")]
        [TextArea(2, 4)]
        public string description;

        /// <summary>Icon sprite rendered in the grid cell.</summary>
        [Tooltip("Icon rendered in the shop grid cell.")]
        public Sprite icon;

        /// <summary>Cost of the item in the designated currency.</summary>
        [Tooltip("Price in the designated currency.")]
        public int price;

        /// <summary>Which currency this item costs.</summary>
        [Tooltip("Currency type (Pesos or Estrellas).")]
        public CurrencyType currencyType;

        /// <summary>Which shop tab this item belongs to.</summary>
        [Tooltip("Shop category tab.")]
        public ShopCategory category;

        /// <summary>
        /// Prefab instantiated on the grid when the player places the item.
        /// May be <c>null</c> for consumable items like ingredients.
        /// </summary>
        [Tooltip("Prefab spawned on the grid after purchase. Null for consumables.")]
        public GameObject prefab;

        /// <summary>Minimum restaurant level required to see and buy this item.</summary>
        [Tooltip("Restaurant level required to unlock this item.")]
        public int unlockLevel;

        /// <summary>
        /// Unique identifier used to track ownership count across sessions.
        /// </summary>
        [Tooltip("Unique identifier for save/load tracking.")]
        public string itemId;
    }

    // ====================================================================== //
    //  ShopPanel MonoBehaviour
    // ====================================================================== //

    /// <summary>
    /// Controls the shop / store overlay panel. Players browse items arranged
    /// in a scrollable grid, organised by category tabs, and purchase them.
    /// Purchased furniture and equipment enters a grid-placement mode via the
    /// GridManager; ingredients are added directly to the player's inventory.
    /// <para>
    /// The panel supports three filter modes (<see cref="ShopFilter"/>), an
    /// item detail popup with full description and stats, and a purchase
    /// confirmation dialog for expensive items.
    /// </para>
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Raised after a successful purchase. Subscribers receive the
        /// purchased <see cref="ShopItemData"/>.
        /// </summary>
        public event Action<ShopItemData> OnItemPurchased;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Category tabs
        // ------------------------------------------------------------------ //

        [Header("Category Tabs")]

        [SerializeField]
        [Tooltip("Toggle or button for the Muebles (Furniture) tab.")]
        private Button _tabMuebles;

        [SerializeField]
        [Tooltip("Toggle or button for the Equipos (Equipment) tab.")]
        private Button _tabEquipos;

        [SerializeField]
        [Tooltip("Toggle or button for the Decoracion tab.")]
        private Button _tabDecoracion;

        [SerializeField]
        [Tooltip("Toggle or button for the Ingredientes tab.")]
        private Button _tabIngredientes;

        [SerializeField]
        [Tooltip("Array of Image components for tab underline highlights, " +
                 "indexed to match ShopCategory ordinals.")]
        private Image[] _tabHighlights;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Item grid
        // ------------------------------------------------------------------ //

        [Header("Item Grid")]

        [SerializeField]
        [Tooltip("ScrollRect containing the item grid.")]
        private ScrollRect _scrollRect;

        [SerializeField]
        [Tooltip("RectTransform content container inside the ScrollRect where " +
                 "item cells are instantiated.")]
        private RectTransform _gridContent;

        [SerializeField]
        [Tooltip("Prefab for a single shop-item cell in the grid. Must contain " +
                 "child references for icon, name, price, and level lock.")]
        private GameObject _itemCellPrefab;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Filters
        // ------------------------------------------------------------------ //

        [Header("Filters")]

        [SerializeField]
        [Tooltip("Button to apply the 'All' filter.")]
        private Button _filterAllButton;

        [SerializeField]
        [Tooltip("Button to apply the 'Affordable' filter.")]
        private Button _filterAffordableButton;

        [SerializeField]
        [Tooltip("Button to apply the 'New' filter.")]
        private Button _filterNewButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Detail popup
        // ------------------------------------------------------------------ //

        [Header("Detail Popup")]

        [SerializeField]
        [Tooltip("Root GameObject of the item-detail popup overlay.")]
        private GameObject _detailPopup;

        [SerializeField]
        [Tooltip("Image showing the selected item's icon in the detail popup.")]
        private Image _detailIcon;

        [SerializeField]
        [Tooltip("Text displaying the item name in the detail popup.")]
        private Text _detailName;

        [SerializeField]
        [Tooltip("Text displaying the item description.")]
        private Text _detailDescription;

        [SerializeField]
        [Tooltip("Text displaying the item price in the detail popup.")]
        private Text _detailPrice;

        [SerializeField]
        [Tooltip("Text displaying additional stats (level requirement, owned count).")]
        private Text _detailStats;

        [SerializeField]
        [Tooltip("Buy button inside the detail popup.")]
        private Button _detailBuyButton;

        [SerializeField]
        [Tooltip("Close button for the detail popup.")]
        private Button _detailCloseButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Confirmation dialog
        // ------------------------------------------------------------------ //

        [Header("Confirmation Dialog")]

        [SerializeField]
        [Tooltip("Root GameObject for the purchase confirmation dialog.")]
        private GameObject _confirmDialog;

        [SerializeField]
        [Tooltip("Text summarising the purchase in the confirmation dialog.")]
        private Text _confirmText;

        [SerializeField]
        [Tooltip("Confirm button in the dialog.")]
        private Button _confirmYesButton;

        [SerializeField]
        [Tooltip("Cancel button in the dialog.")]
        private Button _confirmNoButton;

        [SerializeField]
        [Tooltip("Price threshold above which a confirmation dialog is shown.")]
        private int _confirmationThreshold = 5000;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Data
        // ------------------------------------------------------------------ //

        [Header("Item Data")]

        [SerializeField]
        [Tooltip("All shop items available in the game. Populate from ScriptableObjects " +
                 "or load dynamically.")]
        private List<ShopItemData> _allItems = new List<ShopItemData>();

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        /// <summary>Currently selected category tab.</summary>
        private ShopCategory _currentCategory = ShopCategory.Muebles;

        /// <summary>Currently active filter.</summary>
        private ShopFilter _currentFilter = ShopFilter.All;

        /// <summary>The item currently shown in the detail popup (if any).</summary>
        private ShopItemData _selectedItem;

        /// <summary>Pool of instantiated cell GameObjects for recycling.</summary>
        private readonly List<GameObject> _cellPool = new List<GameObject>();

        /// <summary>
        /// Tracks how many of each item the player owns, keyed by
        /// <see cref="ShopItemData.itemId"/>.
        /// </summary>
        private readonly Dictionary<string, int> _ownedCounts = new Dictionary<string, int>();

        /// <summary>
        /// Set of item IDs that were unlocked since the last time the player
        /// opened the shop. Used by the "New" filter.
        /// </summary>
        private readonly HashSet<string> _newlyUnlockedIds = new HashSet<string>();

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            WireButtons();
            HideDetailPopup();
            HideConfirmDialog();
        }

        private void OnEnable()
        {
            PopulateCategory(_currentCategory);
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Clears the current grid and repopulates it with items from the
        /// specified <paramref name="category"/>, applying the active filter.
        /// </summary>
        /// <param name="category">The shop category to display.</param>
        public void PopulateCategory(ShopCategory category)
        {
            _currentCategory = category;
            UpdateTabHighlights();
            RebuildGrid();
        }

        /// <summary>
        /// Selects an item and opens the detail popup showing its full
        /// description and stats.
        /// </summary>
        /// <param name="item">The item to inspect.</param>
        public void SelectItem(ShopItemData item)
        {
            _selectedItem = item;
            ShowDetailPopup(item);
        }

        /// <summary>
        /// Attempts to purchase the given item. Checks affordability and level
        /// requirements, optionally shows a confirmation dialog for expensive
        /// items, then deducts currency and notifies listeners.
        /// </summary>
        /// <param name="item">The item to purchase.</param>
        public void PurchaseItem(ShopItemData item)
        {
            // Level check.
            int playerLevel = GetPlayerLevel();
            if (playerLevel < item.unlockLevel)
            {
                Debug.LogWarning($"[ShopPanel] Cannot buy '{item.name}': requires level {item.unlockLevel}.");
                return;
            }

            // Affordability check.
            if (!CanAfford(item))
            {
                Debug.LogWarning($"[ShopPanel] Cannot buy '{item.name}': insufficient funds.");
                return;
            }

            // Confirmation for expensive items.
            if (item.price >= _confirmationThreshold)
            {
                ShowConfirmDialog(item);
                return;
            }

            ExecutePurchase(item);
        }

        /// <summary>
        /// Marks an item ID as newly unlocked so it appears under the
        /// "New" filter with a badge.
        /// </summary>
        /// <param name="itemId">Unique ID of the newly unlocked item.</param>
        public void MarkAsNew(string itemId)
        {
            _newlyUnlockedIds.Add(itemId);
        }

        /// <summary>
        /// Sets the owned count for a specific item. Called by external systems
        /// (e.g. save/load) to keep the shop display in sync.
        /// </summary>
        /// <param name="itemId">Unique item identifier.</param>
        /// <param name="count">Number owned.</param>
        public void SetOwnedCount(string itemId, int count)
        {
            _ownedCounts[itemId] = Mathf.Max(0, count);
        }

        // ------------------------------------------------------------------ //
        //  Button wiring
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Wires all button click listeners.
        /// </summary>
        private void WireButtons()
        {
            // Category tabs.
            if (_tabMuebles != null)
                _tabMuebles.onClick.AddListener(() => PopulateCategory(ShopCategory.Muebles));
            if (_tabEquipos != null)
                _tabEquipos.onClick.AddListener(() => PopulateCategory(ShopCategory.Equipos));
            if (_tabDecoracion != null)
                _tabDecoracion.onClick.AddListener(() => PopulateCategory(ShopCategory.Decoracion));
            if (_tabIngredientes != null)
                _tabIngredientes.onClick.AddListener(() => PopulateCategory(ShopCategory.Ingredientes));

            // Filters.
            if (_filterAllButton != null)
                _filterAllButton.onClick.AddListener(() => ApplyFilter(ShopFilter.All));
            if (_filterAffordableButton != null)
                _filterAffordableButton.onClick.AddListener(() => ApplyFilter(ShopFilter.Affordable));
            if (_filterNewButton != null)
                _filterNewButton.onClick.AddListener(() => ApplyFilter(ShopFilter.New));

            // Detail popup.
            if (_detailBuyButton != null)
                _detailBuyButton.onClick.AddListener(OnDetailBuyClicked);
            if (_detailCloseButton != null)
                _detailCloseButton.onClick.AddListener(HideDetailPopup);

            // Confirmation dialog.
            if (_confirmYesButton != null)
                _confirmYesButton.onClick.AddListener(OnConfirmYes);
            if (_confirmNoButton != null)
                _confirmNoButton.onClick.AddListener(HideConfirmDialog);
        }

        // ------------------------------------------------------------------ //
        //  Grid building
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Rebuilds the item grid for the current category and filter.
        /// </summary>
        private void RebuildGrid()
        {
            // Return all active cells to the pool.
            foreach (GameObject cell in _cellPool)
            {
                if (cell != null)
                    cell.SetActive(false);
            }

            List<ShopItemData> filtered = GetFilteredItems();
            int playerLevel = GetPlayerLevel();

            for (int i = 0; i < filtered.Count; i++)
            {
                GameObject cell = GetOrCreateCell(i);
                cell.SetActive(true);
                PopulateCell(cell, filtered[i], playerLevel);
            }

            // Scroll to top.
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// Returns or creates a cell GameObject at the given pool index.
        /// </summary>
        private GameObject GetOrCreateCell(int index)
        {
            if (index < _cellPool.Count)
                return _cellPool[index];

            if (_itemCellPrefab == null || _gridContent == null)
                return new GameObject("EmptyCell");

            GameObject cell = Instantiate(_itemCellPrefab, _gridContent);
            _cellPool.Add(cell);
            return cell;
        }

        /// <summary>
        /// Fills a cell's child UI elements with data from a
        /// <see cref="ShopItemData"/> entry.
        /// </summary>
        private void PopulateCell(GameObject cell, ShopItemData item, int playerLevel)
        {
            // Icon.
            Image icon = cell.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
                icon.sprite = item.icon;

            // Name.
            Text nameText = cell.transform.Find("NameText")?.GetComponent<Text>();
            if (nameText != null)
                nameText.text = item.name;

            // Price.
            Text priceText = cell.transform.Find("PriceText")?.GetComponent<Text>();
            if (priceText != null)
            {
                string currencySymbol = item.currencyType == CurrencyType.Pesos ? "$" : "E";
                priceText.text = $"{currencySymbol}{item.price:N0}";
                priceText.color = CanAfford(item) ? Color.white : new Color(1f, 0.4f, 0.4f);
            }

            // Level lock indicator.
            GameObject lockOverlay = cell.transform.Find("LockOverlay")?.gameObject;
            Text levelText = cell.transform.Find("LevelText")?.GetComponent<Text>();
            bool locked = playerLevel < item.unlockLevel;
            if (lockOverlay != null)
                lockOverlay.SetActive(locked);
            if (levelText != null)
                levelText.text = locked ? $"Nv. {item.unlockLevel}" : string.Empty;

            // Owned count.
            Text ownedText = cell.transform.Find("OwnedText")?.GetComponent<Text>();
            if (ownedText != null)
            {
                int owned = GetOwnedCount(item.itemId);
                ownedText.text = owned > 0 ? $"x{owned}" : string.Empty;
            }

            // New badge.
            GameObject newBadge = cell.transform.Find("NewBadge")?.gameObject;
            if (newBadge != null)
                newBadge.SetActive(_newlyUnlockedIds.Contains(item.itemId));

            // Click handler.
            Button cellButton = cell.GetComponent<Button>();
            if (cellButton != null)
            {
                cellButton.onClick.RemoveAllListeners();
                ShopItemData captured = item;
                cellButton.onClick.AddListener(() => SelectItem(captured));
            }
        }

        // ------------------------------------------------------------------ //
        //  Filtering
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Applies a new filter mode and rebuilds the grid.
        /// </summary>
        private void ApplyFilter(ShopFilter filter)
        {
            _currentFilter = filter;
            RebuildGrid();
        }

        /// <summary>
        /// Returns the list of items matching the current category and filter.
        /// </summary>
        private List<ShopItemData> GetFilteredItems()
        {
            List<ShopItemData> results = new List<ShopItemData>();
            int playerLevel = GetPlayerLevel();

            foreach (ShopItemData item in _allItems)
            {
                if (item.category != _currentCategory)
                    continue;

                // Items above the player's level are still shown (locked) unless
                // using the Affordable filter.
                switch (_currentFilter)
                {
                    case ShopFilter.All:
                        results.Add(item);
                        break;

                    case ShopFilter.Affordable:
                        if (playerLevel >= item.unlockLevel && CanAfford(item))
                            results.Add(item);
                        break;

                    case ShopFilter.New:
                        if (_newlyUnlockedIds.Contains(item.itemId))
                            results.Add(item);
                        break;
                }
            }

            return results;
        }

        // ------------------------------------------------------------------ //
        //  Detail popup
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens the detail popup with information about the selected item.
        /// </summary>
        private void ShowDetailPopup(ShopItemData item)
        {
            if (_detailPopup == null)
                return;

            _detailPopup.SetActive(true);

            if (_detailIcon != null) _detailIcon.sprite = item.icon;
            if (_detailName != null) _detailName.text = item.name;
            if (_detailDescription != null) _detailDescription.text = item.description;

            if (_detailPrice != null)
            {
                string currencyLabel = item.currencyType == CurrencyType.Pesos ? "pesos" : "estrellas";
                _detailPrice.text = $"{item.price:N0} {currencyLabel}";
            }

            if (_detailStats != null)
            {
                int owned = GetOwnedCount(item.itemId);
                _detailStats.text = $"Nivel requerido: {item.unlockLevel}\nAdquiridos: {owned}";
            }

            // Disable buy button if locked or unaffordable.
            if (_detailBuyButton != null)
            {
                bool canBuy = GetPlayerLevel() >= item.unlockLevel && CanAfford(item);
                _detailBuyButton.interactable = canBuy;
            }
        }

        /// <summary>
        /// Hides the item detail popup.
        /// </summary>
        private void HideDetailPopup()
        {
            if (_detailPopup != null)
                _detailPopup.SetActive(false);
        }

        /// <summary>
        /// Handler for the Buy button inside the detail popup.
        /// </summary>
        private void OnDetailBuyClicked()
        {
            PurchaseItem(_selectedItem);
        }

        // ------------------------------------------------------------------ //
        //  Confirmation dialog
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Shows the confirmation dialog for expensive purchases.
        /// </summary>
        private void ShowConfirmDialog(ShopItemData item)
        {
            if (_confirmDialog == null)
            {
                // No dialog configured; purchase immediately.
                ExecutePurchase(item);
                return;
            }

            _confirmDialog.SetActive(true);

            if (_confirmText != null)
            {
                string currencyLabel = item.currencyType == CurrencyType.Pesos ? "pesos" : "estrellas";
                _confirmText.text = $"Comprar {item.name} por {item.price:N0} {currencyLabel}?";
            }
        }

        /// <summary>
        /// Hides the confirmation dialog without purchasing.
        /// </summary>
        private void HideConfirmDialog()
        {
            if (_confirmDialog != null)
                _confirmDialog.SetActive(false);
        }

        /// <summary>
        /// Confirmation "yes" handler.
        /// </summary>
        private void OnConfirmYes()
        {
            HideConfirmDialog();
            ExecutePurchase(_selectedItem);
        }

        // ------------------------------------------------------------------ //
        //  Purchase execution
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Performs the actual purchase: deducts currency, increments owned
        /// count, fires events, and triggers grid placement if applicable.
        /// </summary>
        private void ExecutePurchase(ShopItemData item)
        {
            // Deduct currency via EconomyManager.
            if (GameManager.Instance != null && GameManager.Instance.Economy != null)
            {
                EconomyManager economy = GameManager.Instance.Economy;
                bool success = item.currencyType == CurrencyType.Pesos
                    ? economy.SpendPesos(item.price)
                    : economy.SpendEstrellas(item.price);

                if (!success)
                {
                    Debug.LogWarning($"[ShopPanel] Purchase failed for '{item.name}': economy rejected the transaction.");
                    return;
                }
            }

            // Update owned count.
            if (!string.IsNullOrEmpty(item.itemId))
            {
                if (!_ownedCounts.ContainsKey(item.itemId))
                    _ownedCounts[item.itemId] = 0;
                _ownedCounts[item.itemId]++;
            }

            // Remove from new set.
            if (!string.IsNullOrEmpty(item.itemId))
                _newlyUnlockedIds.Remove(item.itemId);

            // Notify HUD.
            HUDController hud = FindObjectOfType<HUDController>();
            if (hud != null)
                hud.SpawnExpenseText(item.price);

            OnItemPurchased?.Invoke(item);
            Debug.Log($"[ShopPanel] Purchased: {item.name} for {item.price} {item.currencyType}.");

            // Refresh the grid to update affordability and counts.
            RebuildGrid();
            HideDetailPopup();
        }

        // ------------------------------------------------------------------ //
        //  Tab highlights
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Updates the visual highlight on the category tabs to reflect the
        /// currently selected category.
        /// </summary>
        private void UpdateTabHighlights()
        {
            if (_tabHighlights == null)
                return;

            int activeIndex = (int)_currentCategory;
            for (int i = 0; i < _tabHighlights.Length; i++)
            {
                if (_tabHighlights[i] != null)
                    _tabHighlights[i].enabled = (i == activeIndex);
            }
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the current player restaurant level, falling back to 1
        /// if the GameManager is not available.
        /// </summary>
        private int GetPlayerLevel()
        {
            if (GameManager.Instance != null && GameManager.Instance.Restaurant != null)
                return GameManager.Instance.Restaurant.Level;
            return 1;
        }

        /// <summary>
        /// Checks whether the player can afford a given item based on their
        /// current balance in the appropriate currency.
        /// </summary>
        private bool CanAfford(ShopItemData item)
        {
            if (GameManager.Instance == null || GameManager.Instance.Economy == null)
                return false;

            EconomyManager economy = GameManager.Instance.Economy;
            return item.currencyType == CurrencyType.Pesos
                ? economy.Pesos >= item.price
                : economy.Estrellas >= item.price;
        }

        /// <summary>
        /// Returns the number of a specific item the player owns.
        /// </summary>
        private int GetOwnedCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return 0;
            return _ownedCounts.TryGetValue(itemId, out int count) ? count : 0;
        }
    }
}
