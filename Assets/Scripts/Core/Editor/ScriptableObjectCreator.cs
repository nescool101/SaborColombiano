#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SaborColombiano.Data;
using SaborColombiano.Menu;

namespace SaborColombiano.Core.Editor
{
    /// <summary>
    /// Editor utility to auto-generate ScriptableObject assets from the ColombianRecipeDatabase.
    /// Use via the Unity menu: Sabor Colombiano > Generate All ScriptableObjects
    /// </summary>
    public static class ScriptableObjectCreator
    {
        private const string RecipePath = "Assets/ScriptableObjects/Recipes/";
        private const string IngredientPath = "Assets/ScriptableObjects/Ingredients/";
        private const string FurniturePath = "Assets/ScriptableObjects/Furniture/";

        [MenuItem("Sabor Colombiano/Generate All ScriptableObjects")]
        public static void GenerateAll()
        {
            GenerateIngredients();
            GenerateRecipes();
            GenerateFurniture();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ScriptableObjectCreator] All ScriptableObjects generated successfully!");
        }

        [MenuItem("Sabor Colombiano/Generate Ingredients")]
        public static void GenerateIngredients()
        {
            EnsureDirectoryExists(IngredientPath);

            foreach (var data in ColombianRecipeDatabase.AllIngredients)
            {
                string assetPath = $"{IngredientPath}{data.id}.asset";

                var existing = AssetDatabase.LoadAssetAtPath<Ingredient>(assetPath);
                if (existing != null)
                {
                    // Update existing
                    existing.ingredientName = data.name;
                    existing.description = data.description;
                    existing.purchasePrice = data.price;
                    existing.shelfLife = data.shelfLifeDays;
                    existing.unlockLevel = data.unlockLevel;
                    existing.isBasic = data.isBasic;
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    var ingredient = ScriptableObject.CreateInstance<Ingredient>();
                    ingredient.ingredientName = data.name;
                    ingredient.description = data.description;
                    ingredient.purchasePrice = data.price;
                    ingredient.shelfLife = data.shelfLifeDays;
                    ingredient.unlockLevel = data.unlockLevel;
                    ingredient.isBasic = data.isBasic;
                    AssetDatabase.CreateAsset(ingredient, assetPath);
                }
            }

            Debug.Log($"[ScriptableObjectCreator] Generated {ColombianRecipeDatabase.AllIngredients.Length} ingredient assets.");
        }

        [MenuItem("Sabor Colombiano/Generate Recipes")]
        public static void GenerateRecipes()
        {
            EnsureDirectoryExists(RecipePath);

            foreach (var data in ColombianRecipeDatabase.AllRecipes)
            {
                string assetPath = $"{RecipePath}{data.id}.asset";

                var existing = AssetDatabase.LoadAssetAtPath<Recipe>(assetPath);
                if (existing != null)
                {
                    existing.recipeName = data.name;
                    existing.description = data.description;
                    existing.cookingTime = data.cookingTime;
                    existing.sellingPrice = data.sellingPrice;
                    existing.difficulty = data.difficulty;
                    existing.unlockLevel = data.unlockLevel;
                    existing.satisfactionBonus = data.satisfactionBonus;
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    var recipe = ScriptableObject.CreateInstance<Recipe>();
                    recipe.recipeName = data.name;
                    recipe.description = data.description;
                    recipe.cookingTime = data.cookingTime;
                    recipe.sellingPrice = data.sellingPrice;
                    recipe.difficulty = data.difficulty;
                    recipe.unlockLevel = data.unlockLevel;
                    recipe.satisfactionBonus = data.satisfactionBonus;
                    AssetDatabase.CreateAsset(recipe, assetPath);
                }
            }

            Debug.Log($"[ScriptableObjectCreator] Generated {ColombianRecipeDatabase.AllRecipes.Length} recipe assets.");
        }

        [MenuItem("Sabor Colombiano/Generate Furniture")]
        public static void GenerateFurniture()
        {
            EnsureDirectoryExists(FurniturePath);

            foreach (var data in ColombianRecipeDatabase.AllFurniture)
            {
                string assetPath = $"{FurniturePath}{data.id}.asset";

                var existing = AssetDatabase.LoadAssetAtPath<FurnitureData>(assetPath);
                if (existing != null)
                {
                    existing.itemName = data.name;
                    existing.description = data.description;
                    existing.purchasePrice = data.price;
                    existing.gridSize = new Vector2Int(data.gridWidth, data.gridHeight);
                    existing.unlockLevel = data.unlockLevel;
                    existing.capacity = data.capacity;
                    existing.comfortBonus = data.comfortBonus;
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    var furniture = ScriptableObject.CreateInstance<FurnitureData>();
                    furniture.itemName = data.name;
                    furniture.description = data.description;
                    furniture.purchasePrice = data.price;
                    furniture.gridSize = new Vector2Int(data.gridWidth, data.gridHeight);
                    furniture.unlockLevel = data.unlockLevel;
                    furniture.capacity = data.capacity;
                    furniture.comfortBonus = data.comfortBonus;
                    AssetDatabase.CreateAsset(furniture, assetPath);
                }
            }

            Debug.Log($"[ScriptableObjectCreator] Generated {ColombianRecipeDatabase.AllFurniture.Length} furniture assets.");
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path.TrimEnd('/')))
            {
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    if (string.IsNullOrEmpty(parts[i])) continue;
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
    }
}
#endif
