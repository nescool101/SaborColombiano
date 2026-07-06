using UnityEngine;

namespace SaborColombiano.Menu
{
    /// <summary>
    /// Categorizes ingredients by their food group, used for filtering,
    /// storage rules, and shop organisation.
    /// </summary>
    public enum IngredientCategory
    {
        /// <summary>Beef, pork, chicken, chorizo, chicharron, etc.</summary>
        Carne,
        /// <summary>Potato, onion, tomato, yuca, plantain leaf, etc.</summary>
        Verdura,
        /// <summary>Lulo, maracuya, guanabana, mango, coconut, etc.</summary>
        Fruta,
        /// <summary>Rice, beans (frijoles), lentils, masarepa, corn, etc.</summary>
        Grano,
        /// <summary>Cheese (queso), cream, milk, butter, etc.</summary>
        Lacteo,
        /// <summary>Cumin, hogao, aji, guascas, cilantro, achiote, etc.</summary>
        Especia,
        /// <summary>Water, milk for beverages, coconut milk, etc.</summary>
        Bebida,
        /// <summary>Panela, eggs, oil, salt, sugar, etc.</summary>
        Otro
    }

    /// <summary>
    /// ScriptableObject that defines a single ingredient used in Colombian recipes.
    /// Each ingredient asset lives in a Resources or Addressables folder and is
    /// referenced by <see cref="Recipe"/> assets via <see cref="IngredientAmount"/>.
    /// <para>
    /// <b>Colombian ingredient examples:</b>
    /// <list type="bullet">
    ///   <item><description>Platano (green and ripe plantain)</description></item>
    ///   <item><description>Yuca (cassava)</description></item>
    ///   <item><description>Masarepa (pre-cooked corn flour for arepas)</description></item>
    ///   <item><description>Hogao (tomato-onion sofrito base)</description></item>
    ///   <item><description>Guascas (herb essential for ajiaco)</description></item>
    ///   <item><description>Aji (Colombian hot sauce / chili)</description></item>
    ///   <item><description>Panela (unrefined cane sugar block)</description></item>
    ///   <item><description>Frijoles (red beans, typically cargamanto variety)</description></item>
    ///   <item><description>Chicharron (fried pork belly / pork rinds)</description></item>
    ///   <item><description>Aguacate (avocado, served alongside bandeja paisa)</description></item>
    ///   <item><description>Lulo (naranjilla, tart citrus-like fruit)</description></item>
    ///   <item><description>Maracuya (passion fruit)</description></item>
    ///   <item><description>Guanabana (soursop)</description></item>
    /// </list>
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Ingredient",
        menuName = "Sabor Colombiano/Menu/Ingredient",
        order = 0)]
    public class Ingredient : ScriptableObject
    {
        // ------------------------------------------------------------------ //
        //  Identity
        // ------------------------------------------------------------------ //

        /// <summary>Display name shown in the UI (e.g. "Platano Verde").</summary>
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Localised display name of the ingredient.")]
        private string ingredientName = string.Empty;

        /// <summary>Short flavour text or tooltip description.</summary>
        [SerializeField]
        [TextArea(2, 4)]
        [Tooltip("Brief description shown in the ingredient info panel.")]
        private string description = string.Empty;

        /// <summary>Inventory / shop icon.</summary>
        [SerializeField]
        [Tooltip("Sprite displayed in the shop, inventory, and recipe panels.")]
        private Sprite icon;

        /// <summary>Food group this ingredient belongs to.</summary>
        [SerializeField]
        [Tooltip("Category used for shop tabs, storage rules, and sorting.")]
        private IngredientCategory category = IngredientCategory.Otro;

        // ------------------------------------------------------------------ //
        //  Economy
        // ------------------------------------------------------------------ //

        /// <summary>How much this ingredient costs to purchase from the supplier (in pesos).</summary>
        [Header("Economy")]
        [SerializeField]
        [Min(0)]
        [Tooltip("Purchase price in Colombian pesos from the supplier shop.")]
        private int purchasePrice = 500;

        // ------------------------------------------------------------------ //
        //  Freshness
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Number of in-game days before this ingredient spoils.
        /// A value of <c>0</c> means the ingredient never spoils (e.g. salt, panela, rice).
        /// </summary>
        [Header("Freshness")]
        [SerializeField]
        [Min(0)]
        [Tooltip("In-game days before spoiling. 0 = never spoils.")]
        private int shelfLife;

        // ------------------------------------------------------------------ //
        //  Unlock / Availability
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Minimum restaurant level required before this ingredient appears in the shop.
        /// </summary>
        [Header("Unlock")]
        [SerializeField]
        [Min(1)]
        [Tooltip("Restaurant level required to unlock this ingredient in the shop.")]
        private int unlockLevel = 1;

        /// <summary>
        /// When <c>true</c> the ingredient is always stocked in the supplier shop
        /// regardless of random rotation. Basic staples like rice, oil, and salt
        /// should have this enabled.
        /// </summary>
        [SerializeField]
        [Tooltip("If true, this ingredient is always available in the shop (staple items).")]
        private bool isBasic = true;

        // ------------------------------------------------------------------ //
        //  Public Properties
        // ------------------------------------------------------------------ //

        /// <summary>Localised display name of the ingredient.</summary>
        public string IngredientName => ingredientName;

        /// <summary>Brief description shown in info panels and tooltips.</summary>
        public string Description => description;

        /// <summary>Icon sprite used in UI elements.</summary>
        public Sprite Icon => icon;

        /// <summary>Food group category.</summary>
        public IngredientCategory Category => category;

        /// <summary>Purchase cost in pesos.</summary>
        public int PurchasePrice => purchasePrice;

        /// <summary>Days until the ingredient spoils. 0 means it never spoils.</summary>
        public int ShelfLife => shelfLife;

        /// <summary>Whether this ingredient has a finite shelf life.</summary>
        public bool CanSpoil => shelfLife > 0;

        /// <summary>Restaurant level at which this ingredient becomes available.</summary>
        public int UnlockLevel => unlockLevel;

        /// <summary>Whether this ingredient is always available in the shop.</summary>
        public bool IsBasic => isBasic;

        // ------------------------------------------------------------------ //
        //  Validation
        // ------------------------------------------------------------------ //

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(ingredientName))
            {
                ingredientName = name;
            }
        }
#endif
    }
}
