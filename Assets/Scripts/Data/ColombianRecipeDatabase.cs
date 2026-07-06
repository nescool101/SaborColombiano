using System.Collections.Generic;
using UnityEngine;

namespace SaborColombiano.Data
{
    /// <summary>
    /// Static database of default Colombian recipes and ingredients.
    /// Used to seed the game with starter content and define all unlockable items.
    /// These can be converted to ScriptableObjects in the Unity Editor, or used
    /// directly as data at runtime.
    /// </summary>
    public static class ColombianRecipeDatabase
    {
        #region Ingredient Definitions

        public static readonly IngredientInfo[] AllIngredients = new IngredientInfo[]
        {
            // Carnes
            new IngredientInfo("carne_res", "Carne de Res", "Corte de res fresco colombiano", IngredientCategory.Carne, 800, 5, 1, true),
            new IngredientInfo("pollo", "Pollo", "Pollo entero o en presas", IngredientCategory.Carne, 600, 4, 1, true),
            new IngredientInfo("cerdo", "Cerdo", "Carne de cerdo para asados y frituras", IngredientCategory.Carne, 700, 4, 1, true),
            new IngredientInfo("chicharron", "Chicharrón", "Piel de cerdo crujiente", IngredientCategory.Carne, 500, 3, 3, false),
            new IngredientInfo("chorizo", "Chorizo", "Chorizo colombiano artesanal", IngredientCategory.Carne, 400, 3, 3, false),
            new IngredientInfo("morcilla", "Morcilla", "Morcilla colombiana con arroz", IngredientCategory.Carne, 350, 2, 5, false),
            new IngredientInfo("pescado", "Pescado", "Pescado fresco del río o mar", IngredientCategory.Carne, 900, 3, 4, false),
            new IngredientInfo("camaron", "Camarón", "Camarones frescos del Pacífico", IngredientCategory.Carne, 1200, 2, 8, false),

            // Verduras
            new IngredientInfo("papa", "Papa", "Papa criolla o papa pastusa", IngredientCategory.Verdura, 200, 7, 1, true),
            new IngredientInfo("yuca", "Yuca", "Yuca fresca para cocinar o freír", IngredientCategory.Verdura, 250, 5, 1, true),
            new IngredientInfo("platano", "Plátano", "Plátano verde o maduro", IngredientCategory.Verdura, 150, 5, 1, true),
            new IngredientInfo("cebolla", "Cebolla", "Cebolla cabezona o larga", IngredientCategory.Verdura, 100, 7, 1, true),
            new IngredientInfo("tomate", "Tomate", "Tomate rojo maduro", IngredientCategory.Verdura, 120, 5, 1, true),
            new IngredientInfo("mazorca", "Mazorca", "Mazorca tierna de maíz", IngredientCategory.Verdura, 180, 4, 2, false),
            new IngredientInfo("aguacate", "Aguacate", "Aguacate criollo colombiano", IngredientCategory.Verdura, 300, 3, 2, false),
            new IngredientInfo("lechuga", "Lechuga", "Lechuga fresca para ensalada", IngredientCategory.Verdura, 80, 3, 1, true),
            new IngredientInfo("aji", "Ají", "Ají picante colombiano", IngredientCategory.Verdura, 50, 7, 2, false),

            // Granos
            new IngredientInfo("arroz", "Arroz", "Arroz blanco de grano largo", IngredientCategory.Grano, 150, 0, 1, true),
            new IngredientInfo("frijol", "Fríjoles", "Fríjoles rojos cargamanto", IngredientCategory.Grano, 200, 0, 1, true),
            new IngredientInfo("lenteja", "Lentejas", "Lentejas secas", IngredientCategory.Grano, 180, 0, 3, false),
            new IngredientInfo("masarepa", "Masarepa", "Harina de maíz precocida para arepas", IngredientCategory.Grano, 160, 0, 1, true),
            new IngredientInfo("harina_trigo", "Harina de Trigo", "Harina para empanadas y buñuelos", IngredientCategory.Grano, 140, 0, 2, false),

            // Lácteos
            new IngredientInfo("queso", "Queso", "Queso campesino o costeño", IngredientCategory.Lacteo, 350, 5, 1, true),
            new IngredientInfo("crema", "Crema de Leche", "Crema para salsas y postres", IngredientCategory.Lacteo, 250, 4, 3, false),
            new IngredientInfo("leche", "Leche", "Leche entera fresca", IngredientCategory.Lacteo, 180, 3, 1, true),

            // Frutas
            new IngredientInfo("lulo", "Lulo", "Lulo naranjilla para jugos", IngredientCategory.Fruta, 300, 3, 4, false),
            new IngredientInfo("maracuya", "Maracuyá", "Maracuyá fresco del Valle", IngredientCategory.Fruta, 280, 3, 4, false),
            new IngredientInfo("guanabana", "Guanábana", "Guanábana para jugos y postres", IngredientCategory.Fruta, 350, 3, 5, false),
            new IngredientInfo("mango", "Mango", "Mango Tommy o de azúcar", IngredientCategory.Fruta, 200, 4, 3, false),
            new IngredientInfo("coco", "Coco", "Coco fresco para limonada y arroz", IngredientCategory.Fruta, 250, 5, 6, false),
            new IngredientInfo("limon", "Limón", "Limón Tahití para jugos", IngredientCategory.Fruta, 100, 7, 1, true),
            new IngredientInfo("mora", "Mora", "Mora de Castilla", IngredientCategory.Fruta, 280, 3, 5, false),

            // Especias y otros
            new IngredientInfo("panela", "Panela", "Panela de caña para aguapanela y dulces", IngredientCategory.Otro, 120, 0, 1, true),
            new IngredientInfo("hogao", "Hogao", "Sofrito colombiano de tomate y cebolla", IngredientCategory.Especia, 150, 4, 2, false),
            new IngredientInfo("guascas", "Guascas", "Hierba aromática esencial para el ajiaco", IngredientCategory.Especia, 100, 5, 4, false),
            new IngredientInfo("comino", "Comino", "Comino molido", IngredientCategory.Especia, 80, 0, 1, true),
            new IngredientInfo("cilantro", "Cilantro", "Cilantro fresco picado", IngredientCategory.Especia, 60, 4, 1, true),
            new IngredientInfo("aceite", "Aceite", "Aceite vegetal para freír", IngredientCategory.Otro, 200, 0, 1, true),
            new IngredientInfo("arequipe", "Arequipe", "Dulce de leche colombiano", IngredientCategory.Otro, 300, 0, 6, false),
            new IngredientInfo("bocadillo", "Bocadillo", "Dulce de guayaba veleño", IngredientCategory.Otro, 200, 0, 5, false),
        };

        #endregion

        #region Recipe Definitions

        public static readonly RecipeInfo[] AllRecipes = new RecipeInfo[]
        {
            // === LEVEL 1 — Starter recipes ===
            new RecipeInfo(
                "arepa_queso", "Arepa con Queso", "Arepa de maíz asada con queso derretido. El desayuno colombiano por excelencia.",
                RecipeCategory.Acompanamiento, EquipmentType.Grill, 1, 1,
                5f, 1500, 8,
                new IngredientRequirement("masarepa", 1), new IngredientRequirement("queso", 1)
            ),
            new RecipeInfo(
                "arroz_blanco", "Arroz Blanco", "Arroz blanco suelto, la base de toda comida colombiana.",
                RecipeCategory.Acompanamiento, EquipmentType.Stove, 1, 1,
                4f, 800, 3,
                new IngredientRequirement("arroz", 1), new IngredientRequirement("aceite", 1)
            ),
            new RecipeInfo(
                "aguapanela", "Aguapanela", "Bebida tradicional de panela con limón. Reconforta el alma.",
                RecipeCategory.Bebida, EquipmentType.Stove, 1, 1,
                3f, 600, 5,
                new IngredientRequirement("panela", 1), new IngredientRequirement("limon", 1)
            ),
            new RecipeInfo(
                "huevos_pericos", "Huevos Pericos", "Huevos revueltos con tomate y cebolla. Desayuno clásico.",
                RecipeCategory.PlatoFuerte, EquipmentType.Stove, 1, 1,
                4f, 1200, 5,
                new IngredientRequirement("tomate", 1), new IngredientRequirement("cebolla", 1)
            ),

            // === LEVEL 2 ===
            new RecipeInfo(
                "empanadas", "Empanadas", "Empanadas de carne o pollo con ají. El pasabocas colombiano favorito.",
                RecipeCategory.Entrada, EquipmentType.Fryer, 2, 2,
                8f, 2000, 10,
                new IngredientRequirement("masarepa", 1), new IngredientRequirement("carne_res", 1),
                new IngredientRequirement("papa", 1), new IngredientRequirement("comino", 1)
            ),
            new RecipeInfo(
                "patacones", "Patacones", "Plátano verde frito y aplastado. Crujiente y delicioso.",
                RecipeCategory.Acompanamiento, EquipmentType.Fryer, 1, 2,
                5f, 1000, 7,
                new IngredientRequirement("platano", 2), new IngredientRequirement("aceite", 1)
            ),
            new RecipeInfo(
                "limonada_coco", "Limonada de Coco", "Limonada cremosa con leche de coco. Refrescante del Caribe.",
                RecipeCategory.Bebida, EquipmentType.Blender, 1, 2,
                4f, 1800, 8,
                new IngredientRequirement("limon", 2), new IngredientRequirement("coco", 1), new IngredientRequirement("leche", 1)
            ),

            // === LEVEL 3 ===
            new RecipeInfo(
                "bandeja_paisa", "Bandeja Paisa", "El plato más emblemático de Colombia. Fríjoles, arroz, carne, chicharrón, huevo, plátano, arepa, aguacate y hogao.",
                RecipeCategory.PlatoFuerte, EquipmentType.Stove, 5, 3,
                20f, 8000, 20,
                new IngredientRequirement("frijol", 2), new IngredientRequirement("arroz", 1),
                new IngredientRequirement("carne_res", 1), new IngredientRequirement("chicharron", 1),
                new IngredientRequirement("platano", 1), new IngredientRequirement("aguacate", 1),
                new IngredientRequirement("hogao", 1)
            ),
            new RecipeInfo(
                "sancocho", "Sancocho", "Sopa tradicional con pollo, yuca, plátano, mazorca y papa. El alma de la cocina colombiana.",
                RecipeCategory.Sopa, EquipmentType.Stove, 4, 3,
                18f, 6000, 18,
                new IngredientRequirement("pollo", 1), new IngredientRequirement("yuca", 1),
                new IngredientRequirement("platano", 1), new IngredientRequirement("mazorca", 1),
                new IngredientRequirement("papa", 1), new IngredientRequirement("cilantro", 1)
            ),

            // === LEVEL 4 ===
            new RecipeInfo(
                "ajiaco", "Ajiaco Bogotano", "La sopa insignia de Bogotá. Tres tipos de papa, pollo, guascas, mazorca y alcaparras. Se sirve con crema y aguacate.",
                RecipeCategory.Sopa, EquipmentType.Stove, 5, 4,
                22f, 7500, 20,
                new IngredientRequirement("pollo", 1), new IngredientRequirement("papa", 3),
                new IngredientRequirement("mazorca", 1), new IngredientRequirement("guascas", 1),
                new IngredientRequirement("crema", 1), new IngredientRequirement("aguacate", 1)
            ),
            new RecipeInfo(
                "lomo_cerdo", "Lomo de Cerdo en Salsa", "Lomo de cerdo jugoso en salsa de hogao con arroz y plátano maduro.",
                RecipeCategory.PlatoFuerte, EquipmentType.Oven, 3, 4,
                15f, 6500, 15,
                new IngredientRequirement("cerdo", 2), new IngredientRequirement("hogao", 1),
                new IngredientRequirement("platano", 1)
            ),

            // === LEVEL 5 ===
            new RecipeInfo(
                "lechona", "Lechona Tolimense", "Cerdo relleno de arroz y arvejas, asado lentamente. Fiesta en cada bocado.",
                RecipeCategory.PlatoFuerte, EquipmentType.Oven, 5, 5,
                30f, 10000, 25,
                new IngredientRequirement("cerdo", 3), new IngredientRequirement("arroz", 2),
                new IngredientRequirement("comino", 1), new IngredientRequirement("cebolla", 1)
            ),
            new RecipeInfo(
                "tamales", "Tamales Tolimenses", "Masa de maíz rellena de cerdo, pollo, verduras, envuelta en hoja de plátano.",
                RecipeCategory.PlatoFuerte, EquipmentType.Stove, 5, 5,
                25f, 5000, 18,
                new IngredientRequirement("masarepa", 2), new IngredientRequirement("cerdo", 1),
                new IngredientRequirement("pollo", 1), new IngredientRequirement("comino", 1)
            ),
            new RecipeInfo(
                "jugo_lulo", "Jugo de Lulo", "Jugo natural de lulo. El sabor tropical más refrescante de Colombia.",
                RecipeCategory.Bebida, EquipmentType.Blender, 1, 5,
                3f, 2000, 10,
                new IngredientRequirement("lulo", 2), new IngredientRequirement("panela", 1)
            ),

            // === LEVEL 6 ===
            new RecipeInfo(
                "cazuela_mariscos", "Cazuela de Mariscos", "Cazuela cremosa de camarones y pescado con coco. Del Pacífico colombiano.",
                RecipeCategory.PlatoFuerte, EquipmentType.Stove, 5, 6,
                20f, 12000, 22,
                new IngredientRequirement("camaron", 2), new IngredientRequirement("pescado", 1),
                new IngredientRequirement("coco", 1), new IngredientRequirement("hogao", 1)
            ),
            new RecipeInfo(
                "arroz_coco", "Arroz con Coco", "Arroz dulce cocinado con leche de coco. Sabor del Caribe colombiano.",
                RecipeCategory.Acompanamiento, EquipmentType.Stove, 3, 6,
                12f, 3000, 12,
                new IngredientRequirement("arroz", 1), new IngredientRequirement("coco", 1),
                new IngredientRequirement("panela", 1)
            ),

            // === LEVEL 7 ===
            new RecipeInfo(
                "cholado", "Cholado Caleño", "Hielo raspado con frutas frescas, jugo, leche condensada y jarabe. El postre callejero de Cali.",
                RecipeCategory.Postre, EquipmentType.Blender, 3, 7,
                8f, 3500, 15,
                new IngredientRequirement("mango", 1), new IngredientRequirement("mora", 1),
                new IngredientRequirement("limon", 1), new IngredientRequirement("leche", 1)
            ),
            new RecipeInfo(
                "obleas", "Obleas con Arequipe", "Obleas crujientes con arequipe, coco rallado y mora. El postre colombiano clásico.",
                RecipeCategory.Postre, EquipmentType.None, 2, 7,
                4f, 2500, 12,
                new IngredientRequirement("arequipe", 1), new IngredientRequirement("coco", 1),
                new IngredientRequirement("mora", 1)
            ),

            // === LEVEL 8 ===
            new RecipeInfo(
                "pescado_frito", "Pescado Frito", "Mojarra frita crujiente con patacones, arroz de coco y ensalada. Costa Caribe en tu mesa.",
                RecipeCategory.PlatoFuerte, EquipmentType.Fryer, 4, 8,
                15f, 9000, 18,
                new IngredientRequirement("pescado", 1), new IngredientRequirement("platano", 1),
                new IngredientRequirement("arroz", 1), new IngredientRequirement("coco", 1),
                new IngredientRequirement("lechuga", 1)
            ),
            new RecipeInfo(
                "jugo_maracuya", "Jugo de Maracuyá", "Jugo de maracuyá natural. Ácido y dulce, perfecto para acompañar.",
                RecipeCategory.Bebida, EquipmentType.Blender, 1, 8,
                3f, 2200, 10,
                new IngredientRequirement("maracuya", 2), new IngredientRequirement("panela", 1)
            ),

            // === LEVEL 10 (Endgame) ===
            new RecipeInfo(
                "ajiaco_premium", "Ajiaco Santafereño Premium", "La versión suprema del ajiaco con todos los ingredientes de la mejor calidad.",
                RecipeCategory.Sopa, EquipmentType.Stove, 5, 10,
                25f, 15000, 25,
                new IngredientRequirement("pollo", 2), new IngredientRequirement("papa", 4),
                new IngredientRequirement("mazorca", 2), new IngredientRequirement("guascas", 2),
                new IngredientRequirement("crema", 1), new IngredientRequirement("aguacate", 1),
                new IngredientRequirement("cilantro", 1)
            ),
            new RecipeInfo(
                "bandeja_premium", "Gran Bandeja Colombiana", "La bandeja definitiva con todos los acompañamientos. Para paladares exigentes.",
                RecipeCategory.PlatoFuerte, EquipmentType.Stove, 5, 10,
                30f, 18000, 30,
                new IngredientRequirement("frijol", 2), new IngredientRequirement("arroz", 2),
                new IngredientRequirement("carne_res", 2), new IngredientRequirement("chicharron", 1),
                new IngredientRequirement("chorizo", 1), new IngredientRequirement("platano", 1),
                new IngredientRequirement("aguacate", 1), new IngredientRequirement("hogao", 1)
            ),
        };

        #endregion

        #region Furniture Definitions

        public static readonly FurnitureInfo[] AllFurniture = new FurnitureInfo[]
        {
            // Tables
            new FurnitureInfo("mesa_madera", "Mesa de Madera", "Mesa rústica de madera. Básica pero confiable.", FurnitureCategory.Mesa, 1, 1, 500, 1, 2, 5),
            new FurnitureInfo("mesa_plastico", "Mesa Plástica", "Mesa plástica económica para empezar.", FurnitureCategory.Mesa, 1, 1, 200, 1, 2, 0),
            new FurnitureInfo("mesa_elegante", "Mesa Elegante", "Mesa con mantel blanco y centro de mesa.", FurnitureCategory.Mesa, 1, 1, 2000, 4, 4, 15),
            new FurnitureInfo("mesa_terraza", "Mesa de Terraza", "Mesa para área al aire libre con sombrilla.", FurnitureCategory.Mesa, 2, 2, 3000, 6, 6, 20),

            // Chairs
            new FurnitureInfo("silla_madera", "Silla de Madera", "Silla rústica que combina con la mesa de madera.", FurnitureCategory.Silla, 1, 1, 200, 1, 1, 3),
            new FurnitureInfo("silla_plastico", "Silla Plástica", "Silla plástica básica.", FurnitureCategory.Silla, 1, 1, 80, 1, 1, 0),
            new FurnitureInfo("silla_elegante", "Silla Elegante", "Silla acolchada y cómoda.", FurnitureCategory.Silla, 1, 1, 800, 4, 1, 10),
            new FurnitureInfo("banqueta", "Banqueta de Bar", "Banqueta alta para la barra.", FurnitureCategory.Silla, 1, 1, 500, 3, 1, 7),

            // Kitchen Equipment
            new FurnitureInfo("estufa_basica", "Estufa Básica", "Estufa de dos hornillas para empezar.", FurnitureCategory.Cocina, 1, 1, 1000, 1, 1, 0),
            new FurnitureInfo("estufa_industrial", "Estufa Industrial", "Estufa de cuatro hornillas. Cocina más rápido.", FurnitureCategory.Cocina, 2, 1, 5000, 4, 2, 0),
            new FurnitureInfo("horno", "Horno", "Horno para lechona, pan y más.", FurnitureCategory.Cocina, 1, 1, 3000, 3, 1, 0),
            new FurnitureInfo("horno_barro", "Horno de Barro", "Horno artesanal de barro. Da sabor auténtico.", FurnitureCategory.Cocina, 2, 1, 8000, 6, 2, 10),
            new FurnitureInfo("freidora", "Freidora", "Freidora para empanadas, patacones y pescado.", FurnitureCategory.Cocina, 1, 1, 2000, 2, 1, 0),
            new FurnitureInfo("licuadora", "Licuadora", "Licuadora industrial para jugos.", FurnitureCategory.Cocina, 1, 1, 1500, 2, 1, 0),
            new FurnitureInfo("parrilla", "Parrilla", "Parrilla para arepas, carnes y chorizos.", FurnitureCategory.Cocina, 2, 1, 4000, 3, 2, 0),
            new FurnitureInfo("nevera", "Nevera", "Refrigerador para mantener ingredientes frescos.", FurnitureCategory.Cocina, 1, 2, 3500, 2, 0, 5),

            // Decorations
            new FurnitureInfo("sombrero_vueltiao", "Sombrero Vueltiao", "Decoración de pared. El sombrero icónico de Colombia.", FurnitureCategory.Decoracion, 1, 1, 1500, 3, 0, 15),
            new FurnitureInfo("mochila_wayuu", "Mochila Wayúu", "Mochila colorida tejida a mano. Decoración de pared.", FurnitureCategory.Decoracion, 1, 1, 2000, 4, 0, 18),
            new FurnitureInfo("maceta_orquidea", "Maceta con Orquídea", "La flor nacional de Colombia. Elegante.", FurnitureCategory.Decoracion, 1, 1, 800, 2, 0, 10),
            new FurnitureInfo("cuadro_botero", "Cuadro estilo Botero", "Pintura al estilo de Fernando Botero. Arte colombiano.", FurnitureCategory.Decoracion, 1, 1, 5000, 5, 0, 25),
            new FurnitureInfo("hamaca", "Hamaca", "Hamaca decorativa del Caribe colombiano.", FurnitureCategory.Decoracion, 2, 1, 1200, 3, 0, 12),
            new FurnitureInfo("letrero_fonda", "Letrero de Fonda", "Letrero rústico que dice 'Bienvenidos'. Ambiente de fonda.", FurnitureCategory.Decoracion, 2, 1, 600, 1, 0, 8),
            new FurnitureInfo("plantas", "Plantas Tropicales", "Plantas tropicales en maceta. Ambiente fresco.", FurnitureCategory.Decoracion, 1, 1, 400, 1, 0, 6),
            new FurnitureInfo("bandera", "Bandera de Colombia", "Tricolor colombiana. ¡Orgullo patrio!", FurnitureCategory.Decoracion, 1, 1, 300, 1, 0, 5),
        };

        #endregion

        #region Data Structures

        public enum IngredientCategory { Carne, Verdura, Fruta, Grano, Lacteo, Especia, Bebida, Otro }
        public enum RecipeCategory { Entrada, PlatoFuerte, Sopa, Bebida, Postre, Acompanamiento }
        public enum EquipmentType { Stove, Oven, Grill, Fryer, Blender, None }
        public enum FurnitureCategory { Mesa, Silla, Cocina, Decoracion, Pared, Piso }

        [System.Serializable]
        public struct IngredientInfo
        {
            public string id;
            public string name;
            public string description;
            public IngredientCategory category;
            public int price;
            public int shelfLifeDays; // 0 = never spoils
            public int unlockLevel;
            public bool isBasic;

            public IngredientInfo(string id, string name, string description, IngredientCategory category,
                int price, int shelfLifeDays, int unlockLevel, bool isBasic)
            {
                this.id = id;
                this.name = name;
                this.description = description;
                this.category = category;
                this.price = price;
                this.shelfLifeDays = shelfLifeDays;
                this.unlockLevel = unlockLevel;
                this.isBasic = isBasic;
            }
        }

        [System.Serializable]
        public struct IngredientRequirement
        {
            public string ingredientId;
            public int quantity;

            public IngredientRequirement(string ingredientId, int quantity)
            {
                this.ingredientId = ingredientId;
                this.quantity = quantity;
            }
        }

        [System.Serializable]
        public struct RecipeInfo
        {
            public string id;
            public string name;
            public string description;
            public RecipeCategory category;
            public EquipmentType requiredEquipment;
            public int difficulty; // 1-5
            public int unlockLevel;
            public float cookingTime;
            public int sellingPrice;
            public int satisfactionBonus;
            public IngredientRequirement[] ingredients;

            public RecipeInfo(string id, string name, string description, RecipeCategory category,
                EquipmentType equipment, int difficulty, int unlockLevel, float cookingTime,
                int sellingPrice, int satisfactionBonus, params IngredientRequirement[] ingredients)
            {
                this.id = id;
                this.name = name;
                this.description = description;
                this.category = category;
                this.requiredEquipment = equipment;
                this.difficulty = difficulty;
                this.unlockLevel = unlockLevel;
                this.cookingTime = cookingTime;
                this.sellingPrice = sellingPrice;
                this.satisfactionBonus = satisfactionBonus;
                this.ingredients = ingredients;
            }
        }

        [System.Serializable]
        public struct FurnitureInfo
        {
            public string id;
            public string name;
            public string description;
            public FurnitureCategory category;
            public int gridWidth;
            public int gridHeight;
            public int price;
            public int unlockLevel;
            public int capacity;
            public int comfortBonus;

            public FurnitureInfo(string id, string name, string description, FurnitureCategory category,
                int gridWidth, int gridHeight, int price, int unlockLevel, int capacity, int comfortBonus)
            {
                this.id = id;
                this.name = name;
                this.description = description;
                this.category = category;
                this.gridWidth = gridWidth;
                this.gridHeight = gridHeight;
                this.price = price;
                this.unlockLevel = unlockLevel;
                this.capacity = capacity;
                this.comfortBonus = comfortBonus;
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>Find an ingredient by its ID.</summary>
        public static IngredientInfo? GetIngredient(string id)
        {
            foreach (var ing in AllIngredients)
            {
                if (ing.id == id) return ing;
            }
            return null;
        }

        /// <summary>Find a recipe by its ID.</summary>
        public static RecipeInfo? GetRecipe(string id)
        {
            foreach (var recipe in AllRecipes)
            {
                if (recipe.id == id) return recipe;
            }
            return null;
        }

        /// <summary>Find furniture by its ID.</summary>
        public static FurnitureInfo? GetFurniture(string id)
        {
            foreach (var furniture in AllFurniture)
            {
                if (furniture.id == id) return furniture;
            }
            return null;
        }

        /// <summary>Get all recipes available at a given restaurant level.</summary>
        public static List<RecipeInfo> GetRecipesForLevel(int level)
        {
            var result = new List<RecipeInfo>();
            foreach (var recipe in AllRecipes)
            {
                if (recipe.unlockLevel <= level) result.Add(recipe);
            }
            return result;
        }

        /// <summary>Get all ingredients available at a given restaurant level.</summary>
        public static List<IngredientInfo> GetIngredientsForLevel(int level)
        {
            var result = new List<IngredientInfo>();
            foreach (var ing in AllIngredients)
            {
                if (ing.unlockLevel <= level) result.Add(ing);
            }
            return result;
        }

        /// <summary>Get all furniture available at a given restaurant level.</summary>
        public static List<FurnitureInfo> GetFurnitureForLevel(int level)
        {
            var result = new List<FurnitureInfo>();
            foreach (var f in AllFurniture)
            {
                if (f.unlockLevel <= level) result.Add(f);
            }
            return result;
        }

        #endregion
    }
}
