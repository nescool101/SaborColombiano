# Sabor Colombiano

A Colombian restaurant management simulator built with Unity. Inspired by classic Facebook-era restaurant games like Restaurant City, reimagined with authentic Colombian cuisine and culture.

## About

Run your own Colombian restaurant — cook bandeja paisa, ajiaco, empanadas, and 20+ authentic dishes. Hire staff, decorate your fonda, serve customers, and grow from a small corner shop to the most famous restaurant in Colombia.

## Features

- **22+ Colombian Recipes** — From arepas to lechona, each with authentic ingredients and regional descriptions
- **38 Colombian Ingredients** — Plátano, yuca, masarepa, guascas, panela, lulo, and more
- **24 Furniture & Decorations** — Sombrero vueltiao, mochila wayúu, horno de barro, hamacas
- **Isometric Grid System** — Place and rotate furniture, equipment, and decorations
- **Customer AI** — Customers enter, order, eat, and tip based on satisfaction
- **Staff Management** — Hire waiters and chefs, upgrade their skills
- **Economy System** — Earn pesos, buy ingredients, upgrade equipment
- **Save/Load** — Local save system, fully offline

## Requirements

- **Unity 2022 LTS** or **Unity 6 LTS**
- **Android Build Support** module (for mobile builds)
- Unity Personal (free) is sufficient

## Getting Started

1. Install [Unity Hub](https://unity.com/download)
2. Install Unity 2022 LTS or Unity 6 with Android Build Support
3. Open this project folder in Unity Hub
4. Unity will import assets and compile scripts
5. Open the main scene and press Play

### First-Time Setup in Unity Editor

After opening the project:

1. Create a new Scene (File > New Scene)
2. Add an empty GameObject and attach the `SceneBootstrap` script — this auto-creates all managers
3. Use the menu **Sabor Colombiano > Generate All ScriptableObjects** to create recipe/ingredient/furniture assets
4. Press Play to test

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/           # GameManager, RestaurantManager, EconomyManager, SaveSystem
│   ├── Grid/           # GridManager, PlacementSystem, GridObject
│   ├── Menu/           # Recipe, Ingredient, MenuSystem, CookingStation
│   ├── AI/             # CustomerAI, WaiterAI, ChefAI, Pathfinding, CustomerSpawner
│   ├── Economy/        # ShopSystem
│   ├── UI/             # UIManager, HUD, ShopPanel, MenuPanel, StaffPanel
│   ├── Input/          # TouchInputManager, CameraController
│   └── Data/           # GameData, FurnitureData, GameConfig, ColombianRecipeDatabase
├── ScriptableObjects/  # Generated recipe, ingredient, furniture assets
├── Prefabs/            # Character, furniture, equipment prefabs (add sprites here)
├── Sprites/            # Art assets (add your own or use free assets)
└── Scenes/             # Game scenes
```

## Free Art Assets

For prototyping, use these free asset sources:
- [Kenney.nl](https://kenney.nl/) — Free CC0 game assets
- [OpenGameArt.org](https://opengameart.org/) — Free isometric sprites
- Unity Asset Store — Search for free 2D isometric tilesets

## Colombian Dishes Included

| Level | Dish | Category |
|-------|------|----------|
| 1 | Arepa con Queso | Acompañamiento |
| 1 | Arroz Blanco | Acompañamiento |
| 1 | Aguapanela | Bebida |
| 1 | Huevos Pericos | Plato Fuerte |
| 2 | Empanadas | Entrada |
| 2 | Patacones | Acompañamiento |
| 2 | Limonada de Coco | Bebida |
| 3 | Bandeja Paisa | Plato Fuerte |
| 3 | Sancocho | Sopa |
| 4 | Ajiaco Bogotano | Sopa |
| 4 | Lomo de Cerdo en Salsa | Plato Fuerte |
| 5 | Lechona Tolimense | Plato Fuerte |
| 5 | Tamales Tolimenses | Plato Fuerte |
| 5 | Jugo de Lulo | Bebida |
| 6 | Cazuela de Mariscos | Plato Fuerte |
| 6 | Arroz con Coco | Acompañamiento |
| 7 | Cholado Caleño | Postre |
| 7 | Obleas con Arequipe | Postre |
| 8 | Pescado Frito | Plato Fuerte |
| 8 | Jugo de Maracuyá | Bebida |
| 10 | Ajiaco Santafereño Premium | Sopa |
| 10 | Gran Bandeja Colombiana | Plato Fuerte |

## Building for Android

1. File > Build Settings > Switch Platform to Android
2. Player Settings > configure package name, icons
3. Build & Run (requires USB-connected Android device or emulator)

## License

See [LICENSE](LICENSE) file.
