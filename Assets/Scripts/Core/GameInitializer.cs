using System.Collections.Generic;
using UnityEngine;
using SaborColombiano.Data;
using SaborColombiano.Menu;
using SaborColombiano.Economy;

namespace SaborColombiano.Core
{
    /// <summary>
    /// Initializes a new game with starter Colombian content.
    /// Creates default recipes, ingredients, and starting inventory.
    /// Called on first launch or when starting a new game.
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [Header("Starter Configuration")]
        [SerializeField] private int startingPesos = 5000;
        [SerializeField] private int startingEstrellas = 5;
        [SerializeField] private int starterIngredientQuantity = 10;

        /// <summary>
        /// Initialize a brand new game with Colombian starter content.
        /// </summary>
        public void InitializeNewGame()
        {
            Debug.Log("[GameInitializer] Setting up new Colombian restaurant...");

            InitializeEconomy();
            InitializeStarterIngredients();
            InitializeStarterMenu();
            InitializeStarterFurniture();

            Debug.Log("[GameInitializer] ¡Bienvenido a Sabor Colombiano! Tu restaurante está listo.");
        }

        private void InitializeEconomy()
        {
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddPesos(startingPesos);
                EconomyManager.Instance.AddEstrellas(startingEstrellas);
            }
        }

        private void InitializeStarterIngredients()
        {
            var shopSystem = FindAnyObjectByType<ShopSystem>();
            if (shopSystem == null) return;

            // Give starter quantities of all basic (level 1) ingredients
            foreach (var ingredient in ColombianRecipeDatabase.AllIngredients)
            {
                if (ingredient.isBasic && ingredient.unlockLevel <= 1)
                {
                    shopSystem.AddIngredientToInventory(ingredient.id, starterIngredientQuantity);
                    Debug.Log($"  + {starterIngredientQuantity}x {ingredient.name}");
                }
            }
        }

        private void InitializeStarterMenu()
        {
            var menuSystem = FindAnyObjectByType<MenuSystem>();
            if (menuSystem == null) return;

            // Unlock all level 1 recipes
            var starterRecipes = ColombianRecipeDatabase.GetRecipesForLevel(1);
            foreach (var recipe in starterRecipes)
            {
                menuSystem.UnlockRecipeById(recipe.id);
                Debug.Log($"  + Unlocked recipe: {recipe.name}");
            }

            // Auto-add first 2 recipes to active menu
            int added = 0;
            foreach (var recipe in starterRecipes)
            {
                if (added >= 2) break;
                menuSystem.AddToMenuById(recipe.id);
                added++;
            }
        }

        private void InitializeStarterFurniture()
        {
            // The player starts with a small restaurant:
            // - 2 wooden tables
            // - 4 wooden chairs
            // - 1 basic stove
            // - 1 basic grill (for arepas)
            // These are tracked in inventory; player places them via PlacementSystem.

            var shopSystem = FindAnyObjectByType<ShopSystem>();
            if (shopSystem == null) return;

            var starterFurniture = new Dictionary<string, int>
            {
                { "mesa_madera", 2 },
                { "silla_madera", 4 },
                { "estufa_basica", 1 },
                { "parrilla", 1 },
                { "letrero_fonda", 1 },
            };

            foreach (var kvp in starterFurniture)
            {
                shopSystem.AddFurnitureToInventory(kvp.Key, kvp.Value);
                var info = ColombianRecipeDatabase.GetFurniture(kvp.Key);
                if (info.HasValue)
                {
                    Debug.Log($"  + {kvp.Value}x {info.Value.name}");
                }
            }
        }

        /// <summary>
        /// Check for level-up unlocks and notify the player of new content.
        /// Call this whenever the restaurant levels up.
        /// </summary>
        public List<string> GetUnlocksForLevel(int level)
        {
            var unlocks = new List<string>();

            // Check new recipes
            foreach (var recipe in ColombianRecipeDatabase.AllRecipes)
            {
                if (recipe.unlockLevel == level)
                {
                    unlocks.Add($"Nueva receta: {recipe.name}");
                }
            }

            // Check new ingredients
            foreach (var ingredient in ColombianRecipeDatabase.AllIngredients)
            {
                if (ingredient.unlockLevel == level)
                {
                    unlocks.Add($"Nuevo ingrediente: {ingredient.name}");
                }
            }

            // Check new furniture
            foreach (var furniture in ColombianRecipeDatabase.AllFurniture)
            {
                if (furniture.unlockLevel == level)
                {
                    unlocks.Add($"Nuevo mueble: {furniture.name}");
                }
            }

            return unlocks;
        }
    }
}
