using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaborColombiano.Menu
{
    // ====================================================================== //
    //  Supporting Types
    // ====================================================================== //

    /// <summary>
    /// Categorises dishes on the menu for UI tabs, kitchen prioritisation,
    /// and customer ordering logic.
    /// </summary>
    public enum DishCategory
    {
        /// <summary>Starters / appetisers (e.g. Empanadas, Patacones).</summary>
        Entrada,
        /// <summary>Main courses (e.g. Bandeja Paisa, Lechona).</summary>
        PlatoFuerte,
        /// <summary>Soups (e.g. Ajiaco, Sancocho).</summary>
        Sopa,
        /// <summary>Drinks (e.g. Aguapanela, Limonada de Coco).</summary>
        Bebida,
        /// <summary>Desserts (e.g. Obleas, Tres Leches, Natilla).</summary>
        Postre,
        /// <summary>Side dishes (e.g. Arepas, Patacones, Arroz).</summary>
        Acompanamiento
    }

    /// <summary>
    /// Kitchen equipment type required to prepare a dish.
    /// Maps one-to-one with <see cref="CookingStation.StationType"/>.
    /// </summary>
    public enum EquipmentType
    {
        /// <summary>No equipment needed (cold assembly, e.g. Obleas).</summary>
        None,
        /// <summary>Stovetop burner (e.g. Ajiaco, Sancocho, Hogao).</summary>
        Stove,
        /// <summary>Oven (e.g. Lechona, Pandebono).</summary>
        Oven,
        /// <summary>Charcoal or gas grill (e.g. grilled meats, arepas).</summary>
        Grill,
        /// <summary>Deep fryer (e.g. Empanadas, Patacones).</summary>
        Fryer,
        /// <summary>Blender (e.g. Limonada de Coco, Cholado, jugos).</summary>
        Blender
    }

    /// <summary>
    /// Pairs an <see cref="Ingredient"/> reference with a required quantity.
    /// Used inside <see cref="Recipe.RequiredIngredients"/> to define what
    /// the kitchen must have in stock before cooking can begin.
    /// </summary>
    [Serializable]
    public struct IngredientAmount
    {
        /// <summary>Reference to the ingredient ScriptableObject.</summary>
        [Tooltip("The ingredient asset required for this recipe.")]
        public Ingredient ingredient;

        /// <summary>
        /// Number of units consumed when the dish is prepared.
        /// Fractional amounts are supported for spices, liquids, etc.
        /// </summary>
        [Min(0.01f)]
        [Tooltip("Quantity of this ingredient consumed per dish.")]
        public float quantity;
    }

    // ====================================================================== //
    //  Recipe ScriptableObject
    // ====================================================================== //

    /// <summary>
    /// ScriptableObject defining a single Colombian dish that can be prepared
    /// and served in the restaurant. Acts as the blueprint referenced by the
    /// <see cref="MenuSystem"/> and <see cref="CookingStation"/>.
    /// <para>
    /// <b>Iconic Colombian dishes (example assets):</b>
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Bandeja Paisa</b> -- The national platter: red beans, rice,
    ///     ground beef, chicharron, fried egg, plantain, arepa, chorizo,
    ///     avocado, and hogao. Category: PlatoFuerte, Equipment: Stove.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Ajiaco</b> -- Bogota's signature chicken-and-potato soup
    ///     with three potato varieties, corn, guascas herb, and cream.
    ///     Category: Sopa, Equipment: Stove.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Sancocho</b> -- Hearty stew with chicken or beef, yuca,
    ///     plantain, corn, and cilantro. Category: Sopa, Equipment: Stove.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Empanadas</b> -- Deep-fried corn turnovers filled with
    ///     seasoned meat and potato, served with aji.
    ///     Category: Entrada, Equipment: Fryer.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Arepas</b> -- Grilled or fried corn cakes made from masarepa,
    ///     often filled with cheese or egg. Category: Acompanamiento, Equipment: Grill.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Patacones</b> -- Twice-fried green plantain discs served as
    ///     a side or appetiser with hogao. Category: Acompanamiento, Equipment: Fryer.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Lechona</b> -- Slow-roasted whole pig stuffed with rice, peas,
    ///     and spices. A Tolima specialty. Category: PlatoFuerte, Equipment: Oven.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Tamales</b> -- Corn-dough parcels steamed in plantain leaves
    ///     with chicken, pork, vegetables, and spices.
    ///     Category: PlatoFuerte, Equipment: Stove.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Obleas</b> -- Thin wafer discs sandwiched with arequipe
    ///     (dulce de leche), jam, and grated cheese.
    ///     Category: Postre, Equipment: None.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Aguapanela</b> -- Traditional hot or cold drink made by
    ///     dissolving panela in water, often with lime.
    ///     Category: Bebida, Equipment: Stove.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Limonada de Coco</b> -- Creamy coconut limeade blended with
    ///     condensed milk and lime juice. Category: Bebida, Equipment: Blender.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Cholado</b> -- Shaved-ice dessert from Cali topped with
    ///     tropical fruits, condensed milk, and syrup.
    ///     Category: Postre, Equipment: Blender.
    ///   </description></item>
    /// </list>
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Recipe",
        menuName = "Sabor Colombiano/Menu/Recipe",
        order = 1)]
    public class Recipe : ScriptableObject
    {
        // ------------------------------------------------------------------ //
        //  Identity
        // ------------------------------------------------------------------ //

        /// <summary>Display name of the dish (e.g. "Bandeja Paisa").</summary>
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Display name shown on the menu board and order tickets.")]
        private string recipeName = string.Empty;

        /// <summary>Flavour-text description shown in info panels.</summary>
        [SerializeField]
        [TextArea(2, 5)]
        [Tooltip("Short description of the dish for the player.")]
        private string description = string.Empty;

        /// <summary>Menu board / order ticket icon.</summary>
        [SerializeField]
        [Tooltip("Sprite displayed on the menu board and order UI.")]
        private Sprite icon;

        /// <summary>Menu section this dish belongs to.</summary>
        [SerializeField]
        [Tooltip("Menu category used for UI tabs and ordering logic.")]
        private DishCategory category = DishCategory.PlatoFuerte;

        // ------------------------------------------------------------------ //
        //  Ingredients
        // ------------------------------------------------------------------ //

        /// <summary>
        /// All ingredients (and their quantities) needed to prepare one serving.
        /// The kitchen inventory is checked against this list before cooking starts.
        /// </summary>
        [Header("Ingredients")]
        [SerializeField]
        [Tooltip("Ingredients and quantities consumed per serving.")]
        private List<IngredientAmount> requiredIngredients = new List<IngredientAmount>();

        // ------------------------------------------------------------------ //
        //  Cooking
        // ------------------------------------------------------------------ //

        /// <summary>Time in seconds to prepare one serving at base speed.</summary>
        [Header("Cooking")]
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Base cooking time in seconds (before speed multipliers).")]
        private float cookingTime = 30f;

        /// <summary>
        /// Difficulty rating from 1 (simple) to 5 (expert).
        /// Affects the chance of cooking failure when the chef skill is low.
        /// </summary>
        [SerializeField]
        [Range(1, 5)]
        [Tooltip("Difficulty rating 1-5. Higher = requires better chef skill.")]
        private int difficulty = 1;

        /// <summary>
        /// Type of kitchen equipment required to cook the dish.
        /// <see cref="EquipmentType.None"/> for cold-assembly dishes.
        /// </summary>
        [SerializeField]
        [Tooltip("Equipment needed. Must have a matching CookingStation.")]
        private EquipmentType requiredEquipment = EquipmentType.None;

        // ------------------------------------------------------------------ //
        //  Economy
        // ------------------------------------------------------------------ //

        /// <summary>Base selling price in Colombian pesos.</summary>
        [Header("Economy")]
        [SerializeField]
        [Min(0)]
        [Tooltip("Revenue per serving in pesos (before modifiers).")]
        private int sellingPrice = 5000;

        // ------------------------------------------------------------------ //
        //  Progression
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Minimum restaurant level required before this recipe can be unlocked.
        /// </summary>
        [Header("Progression")]
        [SerializeField]
        [Min(1)]
        [Tooltip("Restaurant level needed to unlock this recipe.")]
        private int unlockLevel = 1;

        /// <summary>
        /// Flat bonus added to customer satisfaction when this dish is served.
        /// Signature dishes or daily specials can have higher values.
        /// </summary>
        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra satisfaction points awarded when this dish is served.")]
        private float satisfactionBonus;

        // ------------------------------------------------------------------ //
        //  Public Properties
        // ------------------------------------------------------------------ //

        /// <summary>Display name of the dish.</summary>
        public string RecipeName => recipeName;

        /// <summary>Flavour-text description.</summary>
        public string Description => description;

        /// <summary>Menu icon sprite.</summary>
        public Sprite Icon => icon;

        /// <summary>Menu category.</summary>
        public DishCategory Category => category;

        /// <summary>Read-only view of the required ingredients list.</summary>
        public IReadOnlyList<IngredientAmount> RequiredIngredients => requiredIngredients;

        /// <summary>Base cooking duration in seconds.</summary>
        public float CookingTime => cookingTime;

        /// <summary>Difficulty rating (1-5).</summary>
        public int Difficulty => difficulty;

        /// <summary>Kitchen equipment type required.</summary>
        public EquipmentType RequiredEquipment => requiredEquipment;

        /// <summary>Revenue per serving in pesos.</summary>
        public int SellingPrice => sellingPrice;

        /// <summary>Restaurant level needed to unlock.</summary>
        public int UnlockLevel => unlockLevel;

        /// <summary>Bonus satisfaction points from serving this dish.</summary>
        public float SatisfactionBonus => satisfactionBonus;

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the total ingredient cost to produce one serving,
        /// calculated from the purchase prices of all required ingredients.
        /// </summary>
        /// <returns>Total cost in pesos.</returns>
        public int CalculateIngredientCost()
        {
            float total = 0f;
            for (int i = 0; i < requiredIngredients.Count; i++)
            {
                IngredientAmount ia = requiredIngredients[i];
                if (ia.ingredient != null)
                {
                    total += ia.ingredient.PurchasePrice * ia.quantity;
                }
            }
            return Mathf.CeilToInt(total);
        }

        /// <summary>
        /// Gross profit per serving (selling price minus ingredient cost).
        /// </summary>
        public int ProfitMargin => sellingPrice - CalculateIngredientCost();

        // ------------------------------------------------------------------ //
        //  Validation
        // ------------------------------------------------------------------ //

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                recipeName = name;
            }

            // Clamp difficulty just in case (Range attribute handles the slider,
            // but direct serialisation could bypass it).
            difficulty = Mathf.Clamp(difficulty, 1, 5);
        }
#endif
    }
}
