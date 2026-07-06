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
                    var so = new SerializedObject(existing);
                    so.FindProperty("ingredientName").stringValue = data.name;
                    so.FindProperty("description").stringValue = data.description;
                    so.FindProperty("purchasePrice").intValue = data.price;
                    so.FindProperty("shelfLife").intValue = data.shelfLifeDays;
                    so.FindProperty("unlockLevel").intValue = data.unlockLevel;
                    so.FindProperty("isBasic").boolValue = data.isBasic;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    var ingredient = ScriptableObject.CreateInstance<Ingredient>();
                    var so = new SerializedObject(ingredient);
                    so.FindProperty("ingredientName").stringValue = data.name;
                    so.FindProperty("description").stringValue = data.description;
                    so.FindProperty("purchasePrice").intValue = data.price;
                    so.FindProperty("shelfLife").intValue = data.shelfLifeDays;
                    so.FindProperty("unlockLevel").intValue = data.unlockLevel;
                    so.FindProperty("isBasic").boolValue = data.isBasic;
                    so.ApplyModifiedPropertiesWithoutUndo();
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
                    var so = new SerializedObject(existing);
                    so.FindProperty("recipeName").stringValue = data.name;
                    so.FindProperty("description").stringValue = data.description;
                    so.FindProperty("cookingTime").floatValue = data.cookingTime;
                    so.FindProperty("sellingPrice").intValue = data.sellingPrice;
                    so.FindProperty("difficulty").intValue = data.difficulty;
                    so.FindProperty("unlockLevel").intValue = data.unlockLevel;
                    so.FindProperty("satisfactionBonus").floatValue = data.satisfactionBonus;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    var recipe = ScriptableObject.CreateInstance<Recipe>();
                    var so = new SerializedObject(recipe);
                    so.FindProperty("recipeName").stringValue = data.name;
                    so.FindProperty("description").stringValue = data.description;
                    so.FindProperty("cookingTime").floatValue = data.cookingTime;
                    so.FindProperty("sellingPrice").intValue = data.sellingPrice;
                    so.FindProperty("difficulty").intValue = data.difficulty;
                    so.FindProperty("unlockLevel").intValue = data.unlockLevel;
                    so.FindProperty("satisfactionBonus").floatValue = data.satisfactionBonus;
                    so.ApplyModifiedPropertiesWithoutUndo();
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
                    var so = new SerializedObject(existing);
                    so.FindProperty("_itemName").stringValue = data.name;
                    so.FindProperty("_description").stringValue = data.description;
                    so.FindProperty("_purchasePrice").intValue = data.price;
                    so.FindProperty("_gridSize").vector2IntValue = new Vector2Int(data.gridWidth, data.gridHeight);
                    so.FindProperty("_unlockLevel").intValue = data.unlockLevel;
                    so.FindProperty("_capacity").intValue = data.capacity;
                    so.FindProperty("_comfortBonus").floatValue = data.comfortBonus;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    var furniture = ScriptableObject.CreateInstance<FurnitureData>();
                    var so = new SerializedObject(furniture);
                    so.FindProperty("_itemName").stringValue = data.name;
                    so.FindProperty("_description").stringValue = data.description;
                    so.FindProperty("_purchasePrice").intValue = data.price;
                    so.FindProperty("_gridSize").vector2IntValue = new Vector2Int(data.gridWidth, data.gridHeight);
                    so.FindProperty("_unlockLevel").intValue = data.unlockLevel;
                    so.FindProperty("_capacity").intValue = data.capacity;
                    so.FindProperty("_comfortBonus").floatValue = data.comfortBonus;
                    so.ApplyModifiedPropertiesWithoutUndo();
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
