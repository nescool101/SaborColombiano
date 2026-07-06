using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SaborColombiano.Core;

namespace SaborColombiano.UI
{
    // ====================================================================== //
    //  Supporting types
    // ====================================================================== //

    /// <summary>
    /// Categories for Colombian recipes, used for filtering in the menu panel
    /// and for chef specialization matching.
    /// </summary>
    public enum RecipeCategory
    {
        /// <summary>Starters: empanadas, patacones, arepas con queso.</summary>
        Entrada,
        /// <summary>Main courses: bandeja paisa, ajiaco, lechona, sancocho.</summary>
        PlatoFuerte,
        /// <summary>Soups: sancocho, ajiaco, sopa de mondongo, caldo de costilla.</summary>
        Sopa,
        /// <summary>Drinks: limonada de coco, jugo de lulo, agua de panela, aguardiente.</summary>
        Bebida,
        /// <summary>Desserts: tres leches, obleas, cocadas, arroz con leche.</summary>
        Postre,
        /// <summary>Side dishes: arroz, ensalada, platano maduro, frijoles.</summary>
        Acompanamiento
    }

    /// <summary>
    /// Represents a single ingredient requirement within a recipe, specifying
    /// both the ingredient reference name and the quantity needed per serving.
    /// </summary>
    [Serializable]
    public struct RecipeIngredient
    {
        /// <summary>Display name of the required ingredient (e.g. "Platano Verde").</summary>
        [Tooltip("Name of the required ingredient.")]
        public string ingredientName;

        /// <summary>Quantity needed per serving.</summary>
        [Tooltip("Amount required per serving.")]
        public int quantity;

        /// <summary>
        /// Whether the player currently has enough of this ingredient in
        /// stock. Set at runtime by <see cref="MenuPanel"/>.
        /// </summary>
        [HideInInspector]
        public bool isAvailable;
    }

    /// <summary>
    /// Complete data definition for a Colombian recipe. Configured as a
    /// serializable class so it can be embedded in ScriptableObjects or
    /// serialized to JSON for save data.
    /// </summary>
    [Serializable]
    public class Recipe
    {
        /// <summary>Unique identifier for save/load and lookup.</summary>
        [Tooltip("Unique recipe identifier.")]
        public string recipeId;

        /// <summary>Display name in Spanish (e.g. "Bandeja Paisa").</summary>
        [Tooltip("Display name shown in the recipe card.")]
        public string recipeName;

        /// <summary>
        /// Flavour text describing the dish and its regional origins
        /// (e.g. "Plato insignia de Antioquia...").
        /// </summary>
        [Tooltip("Description mentioning regional Colombian origins.")]
        [TextArea(2, 5)]
        public string description;

        /// <summary>Icon shown on the recipe card.</summary>
        [Tooltip("Recipe card icon sprite.")]
        public Sprite icon;

        /// <summary>Culinary category of this recipe.</summary>
        [Tooltip("Recipe category for filtering and chef specialization.")]
        public RecipeCategory category;

        /// <summary>Time in game-seconds to prepare one serving.</summary>
        [Tooltip("Cooking time in game-seconds per serving.")]
        public float cookingTime;

        /// <summary>Selling price per serving in pesos.</summary>
        [Tooltip("Selling price in pesos per serving.")]
        public int sellingPrice;

        /// <summary>
        /// Popularity rating from 1 to 5. Affects how often customers
        /// order this dish.
        /// </summary>
        [Tooltip("Popularity rating 1-5 (affects order frequency).")]
        [Range(1, 5)]
        public int popularity;

        /// <summary>List of ingredients required per serving.</summary>
        [Tooltip("Ingredients required to prepare one serving.")]
        public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

        /// <summary>Minimum restaurant level to unlock this recipe.</summary>
        [Tooltip("Restaurant level required to unlock.")]
        public int unlockLevel;

        /// <summary>Whether this recipe has been unlocked by the player.</summary>
        [HideInInspector]
        public bool isUnlocked;
    }

    // ====================================================================== //
    //  MenuPanel MonoBehaviour
    // ====================================================================== //

    /// <summary>
    /// Controls the recipe book and active-menu management panel. The panel
    /// has two sections:
    /// <list type="bullet">
    ///   <item><b>Recetas</b> -- all unlocked recipes, browsable with
    ///   category filters.</item>
    ///   <item><b>Mi Menu</b> -- the player's active menu with a limited
    ///   number of slots determined by restaurant level. Recipes on the
    ///   active menu are the ones customers can order.</item>
    /// </list>
    /// <para>
    /// The panel also features a daily special selector that grants a
    /// popularity/revenue bonus to a single chosen dish.
    /// </para>
    /// </summary>
    public class MenuPanel : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>Raised when a recipe is added to the active menu.</summary>
        public event Action<Recipe> OnRecipeAddedToMenu;

        /// <summary>Raised when a recipe is removed from the active menu.</summary>
        public event Action<Recipe> OnRecipeRemovedFromMenu;

        /// <summary>Raised when the daily special is changed.</summary>
        public event Action<Recipe> OnDailySpecialChanged;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Section toggles
        // ------------------------------------------------------------------ //

        [Header("Section Tabs")]

        [SerializeField]
        [Tooltip("Button to switch to the Recetas (all recipes) section.")]
        private Button _recetasTabButton;

        [SerializeField]
        [Tooltip("Button to switch to the Mi Menu (active menu) section.")]
        private Button _miMenuTabButton;

        [SerializeField]
        [Tooltip("Root container for the Recetas section.")]
        private GameObject _recetasSection;

        [SerializeField]
        [Tooltip("Root container for the Mi Menu section.")]
        private GameObject _miMenuSection;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Recipes grid
        // ------------------------------------------------------------------ //

        [Header("Recipes Grid")]

        [SerializeField]
        [Tooltip("ScrollRect for the recipe list.")]
        private ScrollRect _recipesScrollRect;

        [SerializeField]
        [Tooltip("Content container for recipe cards.")]
        private RectTransform _recipesContent;

        [SerializeField]
        [Tooltip("Prefab for a single recipe card in the grid.")]
        private GameObject _recipeCardPrefab;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Category filters
        // ------------------------------------------------------------------ //

        [Header("Category Filters")]

        [SerializeField]
        [Tooltip("Button to show all recipe categories.")]
        private Button _filterAllButton;

        [SerializeField]
        [Tooltip("Button to filter by Entrada.")]
        private Button _filterEntradaButton;

        [SerializeField]
        [Tooltip("Button to filter by PlatoFuerte.")]
        private Button _filterPlatoFuerteButton;

        [SerializeField]
        [Tooltip("Button to filter by Sopa.")]
        private Button _filterSopaButton;

        [SerializeField]
        [Tooltip("Button to filter by Bebida.")]
        private Button _filterBebidaButton;

        [SerializeField]
        [Tooltip("Button to filter by Postre.")]
        private Button _filterPostreButton;

        [SerializeField]
        [Tooltip("Button to filter by Acompanamiento.")]
        private Button _filterAcompanamientoButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Active menu
        // ------------------------------------------------------------------ //

        [Header("Active Menu (Mi Menu)")]

        [SerializeField]
        [Tooltip("Content container for active menu slots.")]
        private RectTransform _activeMenuContent;

        [SerializeField]
        [Tooltip("Prefab for an active-menu slot (shows assigned recipe or empty state).")]
        private GameObject _menuSlotPrefab;

        [SerializeField]
        [Tooltip("Text displaying current slot usage, e.g. '4 / 6'.")]
        private Text _slotCountText;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Daily special
        // ------------------------------------------------------------------ //

        [Header("Daily Special")]

        [SerializeField]
        [Tooltip("Root container for the daily special selector.")]
        private GameObject _dailySpecialContainer;

        [SerializeField]
        [Tooltip("Image showing the daily special's icon.")]
        private Image _dailySpecialIcon;

        [SerializeField]
        [Tooltip("Text showing the daily special's name.")]
        private Text _dailySpecialName;

        [SerializeField]
        [Tooltip("Button to change the daily special.")]
        private Button _changeDailySpecialButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Recipe detail
        // ------------------------------------------------------------------ //

        [Header("Recipe Detail")]

        [SerializeField]
        [Tooltip("Root GameObject for the recipe detail popup.")]
        private GameObject _recipeDetailPopup;

        [SerializeField]
        [Tooltip("Image for the recipe icon in the detail view.")]
        private Image _detailIcon;

        [SerializeField]
        [Tooltip("Text for the recipe name in the detail view.")]
        private Text _detailName;

        [SerializeField]
        [Tooltip("Text for the recipe description in the detail view.")]
        private Text _detailDescription;

        [SerializeField]
        [Tooltip("Text showing cooking time and selling price.")]
        private Text _detailStats;

        [SerializeField]
        [Tooltip("Text listing required ingredients.")]
        private Text _detailIngredients;

        [SerializeField]
        [Tooltip("Button to add the viewed recipe to the active menu.")]
        private Button _detailAddButton;

        [SerializeField]
        [Tooltip("Button to remove the viewed recipe from the active menu.")]
        private Button _detailRemoveButton;

        [SerializeField]
        [Tooltip("Button to set the viewed recipe as the daily special.")]
        private Button _detailSetSpecialButton;

        [SerializeField]
        [Tooltip("Button to close the recipe detail popup.")]
        private Button _detailCloseButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Slot configuration
        // ------------------------------------------------------------------ //

        [Header("Slot Configuration")]

        [SerializeField]
        [Tooltip("Base number of active-menu slots available at level 1.")]
        private int _baseMenuSlots = 3;

        [SerializeField]
        [Tooltip("Additional menu slots gained per restaurant level.")]
        private int _slotsPerLevel = 1;

        [SerializeField]
        [Tooltip("Maximum number of active-menu slots regardless of level.")]
        private int _maxMenuSlots = 12;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Data
        // ------------------------------------------------------------------ //

        [Header("Recipe Data")]

        [SerializeField]
        [Tooltip("All recipes available in the game.")]
        private List<Recipe> _allRecipes = new List<Recipe>();

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        /// <summary>Recipes currently on the player's active menu.</summary>
        private readonly List<Recipe> _activeMenu = new List<Recipe>();

        /// <summary>The recipe chosen as today's daily special, or null.</summary>
        private Recipe _dailySpecial;

        /// <summary>Currently applied category filter, or null for "All".</summary>
        private RecipeCategory? _activeCategoryFilter;

        /// <summary>Currently selected recipe in the detail popup.</summary>
        private Recipe _selectedRecipe;

        /// <summary>Pool of instantiated recipe card GameObjects.</summary>
        private readonly List<GameObject> _recipeCardPool = new List<GameObject>();

        /// <summary>Pool of instantiated active-menu slot GameObjects.</summary>
        private readonly List<GameObject> _menuSlotPool = new List<GameObject>();

        /// <summary>Whether the Recetas section is active (vs. Mi Menu).</summary>
        private bool _showingRecetas = true;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            WireButtons();
            HideRecipeDetail();
        }

        private void OnEnable()
        {
            ShowRecetasSection();
            PopulateRecipes();
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Population
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Clears and rebuilds the recipe grid based on the current category
        /// filter and unlocked status.
        /// </summary>
        public void PopulateRecipes()
        {
            // Return pooled cards.
            foreach (GameObject card in _recipeCardPool)
            {
                if (card != null) card.SetActive(false);
            }

            List<Recipe> visible = GetFilteredRecipes();
            for (int i = 0; i < visible.Count; i++)
            {
                GameObject card = GetOrCreateRecipeCard(i);
                card.SetActive(true);
                PopulateRecipeCard(card, visible[i]);
            }

            if (_recipesScrollRect != null)
                _recipesScrollRect.verticalNormalizedPosition = 1f;
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Recipe selection
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Selects a recipe and opens its detail popup.
        /// </summary>
        /// <param name="recipe">The recipe to inspect.</param>
        public void SelectRecipe(Recipe recipe)
        {
            if (recipe == null)
                return;

            _selectedRecipe = recipe;
            ShowRecipeDetail(recipe);
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Active menu management
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Adds a recipe to the player's active menu if there is an open slot
        /// and the recipe is not already on the menu.
        /// </summary>
        /// <param name="recipe">The recipe to add.</param>
        /// <returns><c>true</c> if the recipe was successfully added.</returns>
        public bool AddToActiveMenu(Recipe recipe)
        {
            if (recipe == null)
                return false;

            if (_activeMenu.Contains(recipe))
            {
                Debug.LogWarning($"[MenuPanel] '{recipe.recipeName}' is already on the active menu.");
                return false;
            }

            int maxSlots = GetMaxMenuSlots();
            if (_activeMenu.Count >= maxSlots)
            {
                Debug.LogWarning($"[MenuPanel] Active menu is full ({maxSlots} slots).");
                return false;
            }

            _activeMenu.Add(recipe);
            OnRecipeAddedToMenu?.Invoke(recipe);
            Debug.Log($"[MenuPanel] Added '{recipe.recipeName}' to active menu.");

            RefreshActiveMenuDisplay();
            RefreshDetailButtons();
            return true;
        }

        /// <summary>
        /// Removes a recipe from the player's active menu.
        /// </summary>
        /// <param name="recipe">The recipe to remove.</param>
        /// <returns><c>true</c> if the recipe was found and removed.</returns>
        public bool RemoveFromActiveMenu(Recipe recipe)
        {
            if (recipe == null)
                return false;

            if (!_activeMenu.Remove(recipe))
            {
                Debug.LogWarning($"[MenuPanel] '{recipe.recipeName}' is not on the active menu.");
                return false;
            }

            // Clear daily special if it was this recipe.
            if (_dailySpecial == recipe)
            {
                _dailySpecial = null;
                RefreshDailySpecialDisplay();
            }

            OnRecipeRemovedFromMenu?.Invoke(recipe);
            Debug.Log($"[MenuPanel] Removed '{recipe.recipeName}' from active menu.");

            RefreshActiveMenuDisplay();
            RefreshDetailButtons();
            return true;
        }

        /// <summary>
        /// Designates a recipe as the daily special. The recipe must already be
        /// on the active menu.
        /// </summary>
        /// <param name="recipe">The recipe to set as daily special.</param>
        /// <returns><c>true</c> if the daily special was changed.</returns>
        public bool SetDailySpecial(Recipe recipe)
        {
            if (recipe == null)
                return false;

            if (!_activeMenu.Contains(recipe))
            {
                Debug.LogWarning($"[MenuPanel] '{recipe.recipeName}' must be on the active menu to set as daily special.");
                return false;
            }

            _dailySpecial = recipe;
            OnDailySpecialChanged?.Invoke(recipe);
            Debug.Log($"[MenuPanel] Daily special set to '{recipe.recipeName}'.");

            RefreshDailySpecialDisplay();
            RefreshDetailButtons();
            return true;
        }

        /// <summary>
        /// Returns a read-only view of the recipes currently on the active menu.
        /// </summary>
        public IReadOnlyList<Recipe> GetActiveMenu()
        {
            return _activeMenu.AsReadOnly();
        }

        /// <summary>
        /// Returns the currently set daily special, or <c>null</c> if none.
        /// </summary>
        public Recipe GetDailySpecial()
        {
            return _dailySpecial;
        }

        // ------------------------------------------------------------------ //
        //  Button wiring
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Binds all button click listeners.
        /// </summary>
        private void WireButtons()
        {
            // Section tabs.
            if (_recetasTabButton != null)
                _recetasTabButton.onClick.AddListener(ShowRecetasSection);
            if (_miMenuTabButton != null)
                _miMenuTabButton.onClick.AddListener(ShowMiMenuSection);

            // Category filters.
            if (_filterAllButton != null)
                _filterAllButton.onClick.AddListener(() => ApplyCategoryFilter(null));
            if (_filterEntradaButton != null)
                _filterEntradaButton.onClick.AddListener(() => ApplyCategoryFilter(RecipeCategory.Entrada));
            if (_filterPlatoFuerteButton != null)
                _filterPlatoFuerteButton.onClick.AddListener(() => ApplyCategoryFilter(RecipeCategory.PlatoFuerte));
            if (_filterSopaButton != null)
                _filterSopaButton.onClick.AddListener(() => ApplyCategoryFilter(RecipeCategory.Sopa));
            if (_filterBebidaButton != null)
                _filterBebidaButton.onClick.AddListener(() => ApplyCategoryFilter(RecipeCategory.Bebida));
            if (_filterPostreButton != null)
                _filterPostreButton.onClick.AddListener(() => ApplyCategoryFilter(RecipeCategory.Postre));
            if (_filterAcompanamientoButton != null)
                _filterAcompanamientoButton.onClick.AddListener(() => ApplyCategoryFilter(RecipeCategory.Acompanamiento));

            // Detail popup.
            if (_detailAddButton != null)
                _detailAddButton.onClick.AddListener(OnDetailAddClicked);
            if (_detailRemoveButton != null)
                _detailRemoveButton.onClick.AddListener(OnDetailRemoveClicked);
            if (_detailSetSpecialButton != null)
                _detailSetSpecialButton.onClick.AddListener(OnDetailSetSpecialClicked);
            if (_detailCloseButton != null)
                _detailCloseButton.onClick.AddListener(HideRecipeDetail);

            // Daily special.
            if (_changeDailySpecialButton != null)
                _changeDailySpecialButton.onClick.AddListener(OnChangeDailySpecialClicked);
        }

        // ------------------------------------------------------------------ //
        //  Section switching
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Activates the Recetas section and hides Mi Menu.
        /// </summary>
        private void ShowRecetasSection()
        {
            _showingRecetas = true;
            if (_recetasSection != null) _recetasSection.SetActive(true);
            if (_miMenuSection != null) _miMenuSection.SetActive(false);
            PopulateRecipes();
        }

        /// <summary>
        /// Activates the Mi Menu section and hides Recetas.
        /// </summary>
        private void ShowMiMenuSection()
        {
            _showingRecetas = false;
            if (_recetasSection != null) _recetasSection.SetActive(false);
            if (_miMenuSection != null) _miMenuSection.SetActive(true);
            RefreshActiveMenuDisplay();
        }

        // ------------------------------------------------------------------ //
        //  Category filtering
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Applies a category filter and rebuilds the recipe grid.
        /// Pass <c>null</c> to show all categories.
        /// </summary>
        private void ApplyCategoryFilter(RecipeCategory? category)
        {
            _activeCategoryFilter = category;
            PopulateRecipes();
        }

        /// <summary>
        /// Returns the list of unlocked recipes matching the current filter.
        /// </summary>
        private List<Recipe> GetFilteredRecipes()
        {
            List<Recipe> results = new List<Recipe>();

            foreach (Recipe recipe in _allRecipes)
            {
                if (!recipe.isUnlocked)
                    continue;

                if (_activeCategoryFilter.HasValue && recipe.category != _activeCategoryFilter.Value)
                    continue;

                results.Add(recipe);
            }

            return results;
        }

        // ------------------------------------------------------------------ //
        //  Recipe card population
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns or creates a recipe card at the specified pool index.
        /// </summary>
        private GameObject GetOrCreateRecipeCard(int index)
        {
            if (index < _recipeCardPool.Count)
                return _recipeCardPool[index];

            if (_recipeCardPrefab == null || _recipesContent == null)
                return new GameObject("EmptyCard");

            GameObject card = Instantiate(_recipeCardPrefab, _recipesContent);
            _recipeCardPool.Add(card);
            return card;
        }

        /// <summary>
        /// Fills a recipe card's UI elements with data from a
        /// <see cref="Recipe"/> instance.
        /// </summary>
        private void PopulateRecipeCard(GameObject card, Recipe recipe)
        {
            // Icon.
            Image icon = card.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
                icon.sprite = recipe.icon;

            // Name.
            Text nameText = card.transform.Find("NameText")?.GetComponent<Text>();
            if (nameText != null)
                nameText.text = recipe.recipeName;

            // Category badge.
            Text categoryText = card.transform.Find("CategoryBadge")?.GetComponent<Text>();
            if (categoryText != null)
                categoryText.text = GetCategoryDisplayName(recipe.category);

            // Cooking time.
            Text timeText = card.transform.Find("TimeText")?.GetComponent<Text>();
            if (timeText != null)
                timeText.text = $"{recipe.cookingTime:F0}s";

            // Price.
            Text priceText = card.transform.Find("PriceText")?.GetComponent<Text>();
            if (priceText != null)
                priceText.text = $"${recipe.sellingPrice}";

            // Popularity stars.
            Transform starsParent = card.transform.Find("Stars");
            if (starsParent != null)
            {
                for (int i = 0; i < starsParent.childCount; i++)
                {
                    starsParent.GetChild(i).gameObject.SetActive(i < recipe.popularity);
                }
            }

            // Ingredient availability indicator.
            bool allAvailable = AreIngredientsAvailable(recipe);
            Image cardBg = card.GetComponent<Image>();
            if (cardBg != null)
            {
                cardBg.color = allAvailable
                    ? Color.white
                    : new Color(0.7f, 0.7f, 0.7f, 1f);
            }

            // On active menu indicator.
            GameObject onMenuIndicator = card.transform.Find("OnMenuIndicator")?.gameObject;
            if (onMenuIndicator != null)
                onMenuIndicator.SetActive(_activeMenu.Contains(recipe));

            // Click handler.
            Button cardButton = card.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.onClick.RemoveAllListeners();
                Recipe captured = recipe;
                cardButton.onClick.AddListener(() => SelectRecipe(captured));
            }
        }

        // ------------------------------------------------------------------ //
        //  Active menu display
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Rebuilds the active-menu slot display.
        /// </summary>
        private void RefreshActiveMenuDisplay()
        {
            // Return pooled slots.
            foreach (GameObject slot in _menuSlotPool)
            {
                if (slot != null) slot.SetActive(false);
            }

            int maxSlots = GetMaxMenuSlots();

            for (int i = 0; i < maxSlots; i++)
            {
                GameObject slot = GetOrCreateMenuSlot(i);
                slot.SetActive(true);

                bool hasRecipe = i < _activeMenu.Count;
                PopulateMenuSlot(slot, hasRecipe ? _activeMenu[i] : null, i);
            }

            // Slot count text.
            if (_slotCountText != null)
                _slotCountText.text = $"{_activeMenu.Count} / {maxSlots}";
        }

        /// <summary>
        /// Returns or creates a menu slot at the specified pool index.
        /// </summary>
        private GameObject GetOrCreateMenuSlot(int index)
        {
            if (index < _menuSlotPool.Count)
                return _menuSlotPool[index];

            if (_menuSlotPrefab == null || _activeMenuContent == null)
                return new GameObject("EmptySlot");

            GameObject slot = Instantiate(_menuSlotPrefab, _activeMenuContent);
            _menuSlotPool.Add(slot);
            return slot;
        }

        /// <summary>
        /// Fills a menu slot with recipe data or shows an empty state.
        /// </summary>
        private void PopulateMenuSlot(GameObject slot, Recipe recipe, int slotIndex)
        {
            Image icon = slot.transform.Find("Icon")?.GetComponent<Image>();
            Text nameText = slot.transform.Find("NameText")?.GetComponent<Text>();
            GameObject emptyState = slot.transform.Find("EmptyState")?.gameObject;
            GameObject filledState = slot.transform.Find("FilledState")?.gameObject;
            GameObject specialBadge = slot.transform.Find("SpecialBadge")?.gameObject;

            bool filled = recipe != null;

            if (emptyState != null) emptyState.SetActive(!filled);
            if (filledState != null) filledState.SetActive(filled);

            if (filled)
            {
                if (icon != null) icon.sprite = recipe.icon;
                if (nameText != null) nameText.text = recipe.recipeName;
                if (specialBadge != null) specialBadge.SetActive(recipe == _dailySpecial);
            }

            // Tap to select recipe.
            Button slotButton = slot.GetComponent<Button>();
            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                if (filled)
                {
                    Recipe captured = recipe;
                    slotButton.onClick.AddListener(() => SelectRecipe(captured));
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Daily special display
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Updates the daily special indicator in the UI.
        /// </summary>
        private void RefreshDailySpecialDisplay()
        {
            if (_dailySpecialContainer != null)
                _dailySpecialContainer.SetActive(_dailySpecial != null);

            if (_dailySpecial != null)
            {
                if (_dailySpecialIcon != null) _dailySpecialIcon.sprite = _dailySpecial.icon;
                if (_dailySpecialName != null) _dailySpecialName.text = _dailySpecial.recipeName;
            }
        }

        // ------------------------------------------------------------------ //
        //  Recipe detail popup
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens the recipe detail popup with full information.
        /// </summary>
        private void ShowRecipeDetail(Recipe recipe)
        {
            if (_recipeDetailPopup == null)
                return;

            _recipeDetailPopup.SetActive(true);

            if (_detailIcon != null) _detailIcon.sprite = recipe.icon;
            if (_detailName != null) _detailName.text = recipe.recipeName;
            if (_detailDescription != null) _detailDescription.text = recipe.description;

            if (_detailStats != null)
            {
                string categoryName = GetCategoryDisplayName(recipe.category);
                _detailStats.text =
                    $"Categoria: {categoryName}\n" +
                    $"Tiempo: {recipe.cookingTime:F0}s\n" +
                    $"Precio de venta: ${recipe.sellingPrice}\n" +
                    $"Popularidad: {new string('*', recipe.popularity)}";
            }

            if (_detailIngredients != null)
            {
                _detailIngredients.text = BuildIngredientListText(recipe);
            }

            RefreshDetailButtons();
        }

        /// <summary>
        /// Hides the recipe detail popup.
        /// </summary>
        private void HideRecipeDetail()
        {
            if (_recipeDetailPopup != null)
                _recipeDetailPopup.SetActive(false);
        }

        /// <summary>
        /// Updates the Add / Remove / Set Special buttons based on the
        /// currently selected recipe's state.
        /// </summary>
        private void RefreshDetailButtons()
        {
            if (_selectedRecipe == null)
                return;

            bool isOnMenu = _activeMenu.Contains(_selectedRecipe);
            bool menuFull = _activeMenu.Count >= GetMaxMenuSlots();
            bool isSpecial = _dailySpecial == _selectedRecipe;

            if (_detailAddButton != null)
            {
                _detailAddButton.gameObject.SetActive(!isOnMenu);
                _detailAddButton.interactable = !menuFull;
            }

            if (_detailRemoveButton != null)
                _detailRemoveButton.gameObject.SetActive(isOnMenu);

            if (_detailSetSpecialButton != null)
            {
                _detailSetSpecialButton.gameObject.SetActive(isOnMenu);
                _detailSetSpecialButton.interactable = !isSpecial;
            }
        }

        // ------------------------------------------------------------------ //
        //  Button handlers
        // ------------------------------------------------------------------ //

        private void OnDetailAddClicked()
        {
            if (_selectedRecipe != null)
                AddToActiveMenu(_selectedRecipe);
        }

        private void OnDetailRemoveClicked()
        {
            if (_selectedRecipe != null)
                RemoveFromActiveMenu(_selectedRecipe);
        }

        private void OnDetailSetSpecialClicked()
        {
            if (_selectedRecipe != null)
                SetDailySpecial(_selectedRecipe);
        }

        private void OnChangeDailySpecialClicked()
        {
            // Switch to Mi Menu section so the player can pick a new special.
            ShowMiMenuSection();
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the maximum number of active-menu slots based on the
        /// current restaurant level.
        /// </summary>
        private int GetMaxMenuSlots()
        {
            int level = 1;
            if (GameManager.Instance != null && GameManager.Instance.Restaurant != null)
                level = GameManager.Instance.Restaurant.Level;

            int slots = _baseMenuSlots + (_slotsPerLevel * (level - 1));
            return Mathf.Clamp(slots, _baseMenuSlots, _maxMenuSlots);
        }

        /// <summary>
        /// Checks whether the player has all required ingredients in stock
        /// for a given recipe. Returns <c>true</c> if all are available.
        /// </summary>
        /// <remarks>
        /// Currently returns <c>true</c> by default; will be connected to the
        /// inventory system once implemented.
        /// </remarks>
        private bool AreIngredientsAvailable(Recipe recipe)
        {
            if (recipe.ingredients == null || recipe.ingredients.Count == 0)
                return true;

            // TODO: Query inventory system for each ingredient.
            // For now, assume all ingredients are available.
            foreach (RecipeIngredient ri in recipe.ingredients)
            {
                if (!ri.isAvailable)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Builds a formatted string listing a recipe's ingredients and
        /// their availability.
        /// </summary>
        private string BuildIngredientListText(Recipe recipe)
        {
            if (recipe.ingredients == null || recipe.ingredients.Count == 0)
                return "Sin ingredientes especiales.";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Ingredientes:");

            foreach (RecipeIngredient ri in recipe.ingredients)
            {
                string status = ri.isAvailable ? "[OK]" : "[X]";
                sb.AppendLine($"  {status} {ri.ingredientName} x{ri.quantity}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns a user-friendly display name for a recipe category in
        /// Spanish.
        /// </summary>
        private static string GetCategoryDisplayName(RecipeCategory category)
        {
            switch (category)
            {
                case RecipeCategory.Entrada:         return "Entrada";
                case RecipeCategory.PlatoFuerte:     return "Plato Fuerte";
                case RecipeCategory.Sopa:            return "Sopa";
                case RecipeCategory.Bebida:          return "Bebida";
                case RecipeCategory.Postre:          return "Postre";
                case RecipeCategory.Acompanamiento:  return "Acompanamiento";
                default:                             return category.ToString();
            }
        }
    }
}
