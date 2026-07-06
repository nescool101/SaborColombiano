using UnityEngine;
using SaborColombiano.Grid;
using SaborColombiano.Menu;
using SaborColombiano.Economy;
using SaborColombiano.UI;
using SaborColombiano.Input;

namespace SaborColombiano.Core
{
    /// <summary>
    /// Bootstraps the main game scene by creating all required manager GameObjects
    /// and wiring them together. Attach this to an empty GameObject in the scene
    /// and it will set up all systems on Awake.
    /// </summary>
    public class SceneBootstrap : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool autoCreateManagers = true;

        [Header("References (optional — auto-created if null)")]
        [SerializeField] private GameManager gameManagerPrefab;
        [SerializeField] private Camera mainCamera;

        private void Awake()
        {
            if (!autoCreateManagers) return;

            Debug.Log("[SaborColombiano] Bootstrapping game scene...");

            // Create Managers root
            var managersRoot = new GameObject("--- MANAGERS ---");

            // Game Manager (singleton)
            if (GameManager.Instance == null)
            {
                var gmObj = new GameObject("GameManager");
                gmObj.transform.SetParent(managersRoot.transform);
                gmObj.AddComponent<GameManager>();
            }

            // Restaurant Manager
            if (FindAnyObjectByType<RestaurantManager>() == null)
            {
                var rmObj = new GameObject("RestaurantManager");
                rmObj.transform.SetParent(managersRoot.transform);
                rmObj.AddComponent<RestaurantManager>();
            }

            // Economy Manager
            if (FindAnyObjectByType<EconomyManager>() == null)
            {
                var emObj = new GameObject("EconomyManager");
                emObj.transform.SetParent(managersRoot.transform);
                emObj.AddComponent<EconomyManager>();
            }

            // Grid Manager
            if (FindAnyObjectByType<GridManager>() == null)
            {
                var gridObj = new GameObject("GridManager");
                gridObj.transform.SetParent(managersRoot.transform);
                gridObj.AddComponent<GridManager>();
            }

            // Placement System
            if (FindAnyObjectByType<PlacementSystem>() == null)
            {
                var placementObj = new GameObject("PlacementSystem");
                placementObj.transform.SetParent(managersRoot.transform);
                placementObj.AddComponent<PlacementSystem>();
            }

            // Menu System
            if (FindAnyObjectByType<MenuSystem>() == null)
            {
                var menuObj = new GameObject("MenuSystem");
                menuObj.transform.SetParent(managersRoot.transform);
                menuObj.AddComponent<MenuSystem>();
            }

            // Shop System
            if (FindAnyObjectByType<ShopSystem>() == null)
            {
                var shopObj = new GameObject("ShopSystem");
                shopObj.transform.SetParent(managersRoot.transform);
                shopObj.AddComponent<ShopSystem>();
            }

            // Save System auto-save (static class, but we need a MonoBehaviour for coroutines)
            var saveObj = new GameObject("AutoSave");
            saveObj.transform.SetParent(managersRoot.transform);
            saveObj.AddComponent<AutoSaveRunner>();

            // Create Input root
            var inputRoot = new GameObject("--- INPUT ---");

            // Touch Input Manager
            if (FindAnyObjectByType<TouchInputManager>() == null)
            {
                var touchObj = new GameObject("TouchInputManager");
                touchObj.transform.SetParent(inputRoot.transform);
                touchObj.AddComponent<TouchInputManager>();
            }

            // Camera Controller
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null && mainCamera.GetComponent<CameraController>() == null)
            {
                mainCamera.gameObject.AddComponent<CameraController>();
            }

            // Create UI root
            var uiRoot = new GameObject("--- UI ---");

            // UI Manager
            if (FindAnyObjectByType<UIManager>() == null)
            {
                var uiObj = new GameObject("UIManager");
                uiObj.transform.SetParent(uiRoot.transform);
                uiObj.AddComponent<UIManager>();
            }

            Debug.Log("[SaborColombiano] Bootstrap complete. All managers initialized.");
        }
    }

    /// <summary>
    /// Simple MonoBehaviour that triggers auto-save at intervals.
    /// </summary>
    public class AutoSaveRunner : MonoBehaviour
    {
        [SerializeField] private float autoSaveIntervalSeconds = 60f;
        private float timer;

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= autoSaveIntervalSeconds)
            {
                timer = 0f;
                // Auto-save will be triggered by GameManager when it's ready
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RequestSave();
                }
            }
        }
    }
}
