using UnityEngine;

namespace SaborColombiano.Data
{
    /// <summary>
    /// Broad classification for furniture and equipment items in the restaurant.
    /// Used for shop filtering, inventory tabs, and placement rules.
    /// </summary>
    public enum FurnitureCategory
    {
        /// <summary>Tables of various sizes and styles (Mesa de Madera, Mesa Elegante).</summary>
        Mesa,
        /// <summary>Seating for customers (Silla Rustica, Banqueta de Bar).</summary>
        Silla,
        /// <summary>Kitchen equipment (Estufa de Lena, Horno de Barro, Barra de Jugos).</summary>
        Cocina,
        /// <summary>Decorative items (Hamaca, Sombrero Vueltiao, Mochila Wayuu).</summary>
        Decoracion,
        /// <summary>Wall elements (paintings, shelving, signage).</summary>
        Pared,
        /// <summary>Floor tiles and rugs.</summary>
        Piso
    }

    /// <summary>
    /// Describes how characters interact with a placed furniture/equipment item.
    /// Drives gameplay behaviour such as customer seating, chef cooking, and
    /// storage capacity.
    /// </summary>
    public enum InteractionType
    {
        /// <summary>Customers can sit here (chairs, benches).</summary>
        Seating,
        /// <summary>Chefs use this to prepare food (stoves, ovens, grills).</summary>
        Cooking,
        /// <summary>Stores ingredients or finished dishes (fridges, shelves).</summary>
        Storage,
        /// <summary>Purely visual; no gameplay interaction (wall art, plants).</summary>
        Decorative,
        /// <summary>Functional but non-cooking (cash registers, wash stations).</summary>
        Functional
    }

    /// <summary>
    /// ScriptableObject asset defining a single furniture or equipment item that
    /// can be purchased, placed on the grid, and interacted with by staff and
    /// customers.
    /// <para>
    /// Create new items via <b>Assets > Create > Sabor Colombiano > Data > Furniture</b>.
    /// </para>
    /// <para>
    /// <b>Colombian-themed item ideas:</b>
    /// <list type="bullet">
    ///   <item><description>Mesa de Madera -- rustic wooden table, seats 4.</description></item>
    ///   <item><description>Silla Rustica -- handcrafted wooden chair with woven seat.</description></item>
    ///   <item><description>Estufa de Lena -- traditional wood-burning stove, slow but high quality.</description></item>
    ///   <item><description>Horno de Barro -- clay oven for lechona, pandebono, and empanadas.</description></item>
    ///   <item><description>Barra de Jugos -- juice bar counter with blender station.</description></item>
    ///   <item><description>Hamaca Decorativa -- decorative Caribbean hammock, boosts ambiance.</description></item>
    ///   <item><description>Sombrero Vueltiao Decoration -- iconic Colombian hat mounted on the wall.</description></item>
    ///   <item><description>Mochila Wayuu Wall Art -- colourful hand-woven Wayuu bag displayed as art.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Furniture",
        menuName = "Sabor Colombiano/Data/Furniture",
        order = 0)]
    public class FurnitureData : ScriptableObject
    {
        // ------------------------------------------------------------------ //
        //  Identity
        // ------------------------------------------------------------------ //

        [Header("Identity")]

        [SerializeField]
        [Tooltip("Unique string identifier for this item. Must be globally unique " +
                 "across all furniture assets (e.g. \"mesa_madera_01\").")]
        private string _itemId = string.Empty;

        [SerializeField]
        [Tooltip("Localised display name shown in the shop and inventory UI.")]
        private string _itemName = string.Empty;

        [SerializeField]
        [TextArea(2, 4)]
        [Tooltip("Short flavour-text description shown in info panels.")]
        private string _description = string.Empty;

        [SerializeField]
        [Tooltip("Icon sprite displayed in the shop, inventory, and grid placement UI.")]
        private Sprite _icon;

        [SerializeField]
        [Tooltip("Broad category used for shop tabs and placement rules.")]
        private FurnitureCategory _category = FurnitureCategory.Mesa;

        // ------------------------------------------------------------------ //
        //  Grid
        // ------------------------------------------------------------------ //

        [Header("Grid")]

        [SerializeField]
        [Tooltip("Footprint on the grid in cells (x = width, y = height). " +
                 "A standard chair is (1,1); a long counter might be (3,1).")]
        private Vector2Int _gridSize = Vector2Int.one;

        [SerializeField]
        [Tooltip("Prefab instantiated when this item is placed on the grid. " +
                 "Should contain a GridObject component.")]
        private GameObject _prefab;

        // ------------------------------------------------------------------ //
        //  Economy
        // ------------------------------------------------------------------ //

        [Header("Economy")]

        [SerializeField]
        [Min(0)]
        [Tooltip("Cost in Pesos to buy this item from the shop.")]
        private int _purchasePrice = 500;

        // ------------------------------------------------------------------ //
        //  Unlock
        // ------------------------------------------------------------------ //

        [Header("Unlock")]

        [SerializeField]
        [Min(1)]
        [Tooltip("Minimum restaurant level required to purchase this item.")]
        private int _unlockLevel = 1;

        // ------------------------------------------------------------------ //
        //  Gameplay
        // ------------------------------------------------------------------ //

        [Header("Interaction")]

        [SerializeField]
        [Tooltip("How characters interact with this item once placed.")]
        private InteractionType _interactionType = InteractionType.Decorative;

        [SerializeField]
        [Min(0)]
        [Tooltip("Capacity of this item. For tables: number of seats around it. " +
                 "For cooking equipment: number of dishes that can be prepared at once.")]
        private int _capacity = 1;

        [Header("Modifiers")]

        [SerializeField]
        [Range(0f, 50f)]
        [Tooltip("Comfort bonus applied when a customer uses this item. " +
                 "Higher values increase customer satisfaction (primarily for seating).")]
        private float _comfortBonus;

        [SerializeField]
        [Range(-0.1f, 0.1f)]
        [Tooltip("Modifier to restaurant cleanliness when this item is placed. " +
                 "Positive values make the restaurant look cleaner; negative values " +
                 "make it messier (e.g. a grill might be -0.02, a plant +0.01).")]
        private float _cleanlinessModifier;

        // ------------------------------------------------------------------ //
        //  Public properties
        // ------------------------------------------------------------------ //

        /// <summary>Globally unique string identifier for this furniture item.</summary>
        public string ItemId => _itemId;

        /// <summary>Localised display name.</summary>
        public string ItemName => _itemName;

        /// <summary>Short description / flavour text.</summary>
        public string Description => _description;

        /// <summary>Shop and inventory icon sprite.</summary>
        public Sprite Icon => _icon;

        /// <summary>Broad furniture category.</summary>
        public FurnitureCategory Category => _category;

        /// <summary>Grid footprint in cells (width, height).</summary>
        public Vector2Int GridSize => _gridSize;

        /// <summary>Cost in Pesos to purchase this item.</summary>
        public int PurchasePrice => _purchasePrice;

        /// <summary>
        /// Sell price in Pesos (always half the purchase price, rounded down).
        /// </summary>
        public int SellPrice => _purchasePrice / 2;

        /// <summary>Minimum restaurant level to unlock this item in the shop.</summary>
        public int UnlockLevel => _unlockLevel;

        /// <summary>Prefab instantiated on the grid when placing this item.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>How characters interact with the placed item.</summary>
        public InteractionType InteractionType => _interactionType;

        /// <summary>
        /// Capacity: seats for tables, simultaneous dishes for cooking equipment.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>Comfort bonus applied to customer satisfaction (primarily seating).</summary>
        public float ComfortBonus => _comfortBonus;

        /// <summary>
        /// Cleanliness modifier applied to the restaurant score when this item is placed.
        /// Positive = cleaner appearance, negative = messier.
        /// </summary>
        public float CleanlinessModifier => _cleanlinessModifier;

        // ------------------------------------------------------------------ //
        //  Validation
        // ------------------------------------------------------------------ //

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-fill the item name from the asset name if left blank.
            if (string.IsNullOrWhiteSpace(_itemName))
            {
                _itemName = name;
            }

            // Auto-fill the item ID from the asset name if left blank.
            if (string.IsNullOrWhiteSpace(_itemId))
            {
                _itemId = name.ToLowerInvariant().Replace(" ", "_");
            }

            // Ensure grid size is at least 1x1.
            _gridSize.x = Mathf.Max(1, _gridSize.x);
            _gridSize.y = Mathf.Max(1, _gridSize.y);
        }
#endif
    }
}
