using System;
using UnityEngine;

namespace SaborColombiano.Grid
{
    /// <summary>
    /// Possible states of the placement workflow.
    /// </summary>
    public enum PlacementState
    {
        /// <summary>No placement in progress.</summary>
        Idle,
        /// <summary>The player has chosen an object from the build menu and is picking a cell.</summary>
        Selecting,
        /// <summary>The ghost preview is following the cursor / finger and snapping to the grid.</summary>
        Placing,
        /// <summary>The player is cycling through rotation steps before confirming.</summary>
        Rotating
    }

    /// <summary>
    /// Handles the full drag-and-drop placement flow for <see cref="GridObject"/>
    /// instances onto the isometric grid managed by <see cref="GridManager"/>.
    /// Supports mouse (editor / desktop) and single-touch (mobile) input,
    /// ghost previews with colour feedback, and 90-degree rotation steps.
    /// </summary>
    public class PlacementSystem : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Inspector fields
        // ---------------------------------------------------------------

        /// <summary>Reference to the grid manager that owns the cell data.</summary>
        [Header("References")]
        [SerializeField] private GridManager gridManager;

        /// <summary>
        /// Camera used to convert screen / touch positions into world space.
        /// Falls back to <see cref="Camera.main"/> when left unassigned.
        /// </summary>
        [SerializeField] private Camera placementCamera;

        /// <summary>Colour applied to the ghost when the position is valid.</summary>
        [Header("Feedback Colours")]
        [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);

        /// <summary>Colour applied to the ghost when the position is blocked.</summary>
        [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

        /// <summary>Sorting order used for the ghost preview so it renders on top.</summary>
        [Header("Visuals")]
        [SerializeField] private int ghostSortingOrder = 100;

        // ---------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------

        /// <summary>Fired when a placement session begins (object selected from build menu).</summary>
        public event Action<GridObject> OnPlacementStarted;

        /// <summary>Fired when the player confirms the placement.</summary>
        public event Action<GridObject, Vector2Int> OnPlacementConfirmed;

        /// <summary>Fired when the player cancels the current placement.</summary>
        public event Action OnPlacementCancelled;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        /// <summary>Current phase of the placement workflow.</summary>
        private PlacementState currentState = PlacementState.Idle;

        /// <summary>The prefab that was passed to <see cref="StartPlacing"/>.</summary>
        private GridObject sourcePrefab;

        /// <summary>Live ghost instance shown under the cursor.</summary>
        private GridObject ghostInstance;

        /// <summary>Grid coordinate the ghost is currently snapped to.</summary>
        private Vector2Int currentGridPos;

        /// <summary>Accumulated rotation in degrees (0, 90, 180, 270).</summary>
        private int currentRotation;

        /// <summary>Cached flag indicating whether the current position is valid.</summary>
        private bool isCurrentPositionValid;

        // ---------------------------------------------------------------
        //  Public properties
        // ---------------------------------------------------------------

        /// <summary>Current placement state.</summary>
        public PlacementState CurrentState => currentState;

        /// <summary>Whether a placement session is active (not Idle).</summary>
        public bool IsPlacing => currentState != PlacementState.Idle;

        // ---------------------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (placementCamera == null)
            {
                placementCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (currentState == PlacementState.Idle)
            {
                return;
            }

            UpdatePointerPosition();
            HandleInput();
        }

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Begins a placement session. A semi-transparent ghost of the supplied
        /// prefab is instantiated and will follow the pointer until confirmed or
        /// cancelled.
        /// </summary>
        /// <param name="prefab">The <see cref="GridObject"/> prefab to place.</param>
        public void StartPlacing(GridObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[PlacementSystem] StartPlacing called with a null prefab.");
                return;
            }

            // Clean up any previous session.
            if (ghostInstance != null)
            {
                Destroy(ghostInstance.gameObject);
            }

            sourcePrefab = prefab;
            currentRotation = 0;

            // Instantiate ghost.
            ghostInstance = Instantiate(prefab);
            ghostInstance.name = $"Ghost_{prefab.ObjectName}";
            ghostInstance.Rotation = currentRotation;

            // Disable any colliders / rigidbodies so the ghost cannot interfere
            // with physics or raycasts.
            DisablePhysics(ghostInstance.gameObject);

            // Push the ghost sprite to a high sorting order so it draws on top.
            SetGhostSortingOrder(ghostInstance.gameObject, ghostSortingOrder);

            currentState = PlacementState.Placing;
            OnPlacementStarted?.Invoke(ghostInstance);
        }

        /// <summary>
        /// Attempts to confirm placement at the current grid position. If the
        /// position is invalid the call is ignored and the ghost remains active.
        /// On success a new instance is placed via <see cref="GridManager.PlaceObject"/>
        /// and the ghost is destroyed.
        /// </summary>
        public void ConfirmPlacement()
        {
            if (currentState == PlacementState.Idle || ghostInstance == null)
            {
                return;
            }

            if (!isCurrentPositionValid)
            {
                Debug.Log("[PlacementSystem] Cannot confirm -- position is blocked.");
                return;
            }

            // Spawn the real object from the original prefab.
            GridObject placed = Instantiate(sourcePrefab);
            placed.name = sourcePrefab.ObjectName;
            placed.Rotation = currentRotation;
            placed.ApplyRotationVisual();

            bool success = gridManager.PlaceObject(placed, currentGridPos);

            if (success)
            {
                OnPlacementConfirmed?.Invoke(placed, currentGridPos);
                CleanUpGhost();
                currentState = PlacementState.Idle;
            }
            else
            {
                // Shouldn't happen because we pre-validated, but clean up just in case.
                Destroy(placed.gameObject);
                Debug.LogWarning("[PlacementSystem] GridManager rejected placement despite local validation.");
            }
        }

        /// <summary>
        /// Cancels the active placement session and destroys the ghost preview.
        /// </summary>
        public void CancelPlacement()
        {
            if (currentState == PlacementState.Idle)
            {
                return;
            }

            CleanUpGhost();
            currentState = PlacementState.Idle;
            OnPlacementCancelled?.Invoke();
        }

        /// <summary>
        /// Rotates the ghost preview by 90 degrees clockwise. The rotation wraps
        /// around after 270 back to 0.
        /// </summary>
        public void RotateObject()
        {
            if (currentState == PlacementState.Idle || ghostInstance == null)
            {
                return;
            }

            currentState = PlacementState.Rotating;

            currentRotation = (currentRotation + 90) % 360;
            ghostInstance.Rotation = currentRotation;
            ghostInstance.ApplyRotationVisual();

            // Re-validate after rotation since footprint may have changed.
            RefreshValidation();

            currentState = PlacementState.Placing;
        }

        // ---------------------------------------------------------------
        //  Input handling
        // ---------------------------------------------------------------

        /// <summary>
        /// Reads mouse or touch input each frame to detect confirm, cancel, and
        /// rotate gestures.
        /// </summary>
        private void HandleInput()
        {
            // --- Desktop (mouse) ---
            if (Input.mousePresent)
            {
                // Left-click = confirm.
                if (Input.GetMouseButtonDown(0))
                {
                    ConfirmPlacement();
                    return;
                }

                // Right-click = cancel.
                if (Input.GetMouseButtonDown(1))
                {
                    CancelPlacement();
                    return;
                }

                // R key = rotate.
                if (Input.GetKeyDown(KeyCode.R))
                {
                    RotateObject();
                    return;
                }

                // Escape = cancel.
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelPlacement();
                    return;
                }
            }

            // --- Mobile (touch) ---
            if (Input.touchCount > 0)
            {
                Touch primaryTouch = Input.GetTouch(0);

                switch (primaryTouch.phase)
                {
                    case TouchPhase.Ended:
                        ConfirmPlacement();
                        break;

                    case TouchPhase.Canceled:
                        CancelPlacement();
                        break;
                }

                // Two-finger tap = rotate.
                if (Input.touchCount >= 2)
                {
                    Touch secondTouch = Input.GetTouch(1);
                    if (secondTouch.phase == TouchPhase.Began)
                    {
                        RotateObject();
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        //  Pointer tracking & snapping
        // ---------------------------------------------------------------

        /// <summary>
        /// Reads the current pointer (mouse or primary touch) position, converts
        /// it to a grid coordinate, and snaps the ghost preview accordingly.
        /// </summary>
        private void UpdatePointerPosition()
        {
            if (ghostInstance == null || gridManager == null || placementCamera == null)
            {
                return;
            }

            Vector3 screenPos = GetPointerScreenPosition();
            Vector3 worldPos = placementCamera.ScreenToWorldPoint(screenPos);
            worldPos.z = 0f;

            Vector2Int gridPos = gridManager.WorldToGrid(worldPos);

            // Clamp to grid bounds so the ghost cannot drift far outside.
            gridPos.x = Mathf.Clamp(gridPos.x, 0, gridManager.Width - 1);
            gridPos.y = Mathf.Clamp(gridPos.y, 0, gridManager.Height - 1);

            if (gridPos != currentGridPos)
            {
                currentGridPos = gridPos;
                SnapGhostToGrid();
                RefreshValidation();
            }
        }

        /// <summary>
        /// Returns the current pointer position in screen pixels. Prefers touch
        /// input on devices that support it, otherwise falls back to mouse.
        /// </summary>
        private Vector3 GetPointerScreenPosition()
        {
            if (Input.touchCount > 0)
            {
                return Input.GetTouch(0).position;
            }

            return Input.mousePosition;
        }

        /// <summary>
        /// Moves the ghost instance so that its anchor aligns with
        /// <see cref="currentGridPos"/> on the isometric grid.
        /// </summary>
        private void SnapGhostToGrid()
        {
            Vector2Int effectiveSize = ghostInstance.GetRotatedSize();

            // Compute the visual centre of the multi-cell footprint.
            float centreX = currentGridPos.x + effectiveSize.x * 0.5f;
            float centreY = currentGridPos.y + effectiveSize.y * 0.5f;

            ghostInstance.transform.position = GridToWorldFractional(centreX, centreY);
        }

        /// <summary>
        /// Helper to compute world position from fractional grid coordinates
        /// using the same isometric formula as <see cref="GridManager"/>.
        /// </summary>
        private Vector3 GridToWorldFractional(float gx, float gy)
        {
            float halfCell = gridManager.CellSize * 0.5f;
            float quarterCell = gridManager.CellSize * 0.25f;

            float isoX = (gx - gy) * halfCell;
            float isoY = (gx + gy) * quarterCell;

            return new Vector3(
                isoX + gridManager.GridOrigin.x,
                isoY + gridManager.GridOrigin.y,
                0f
            );
        }

        // ---------------------------------------------------------------
        //  Validation & feedback
        // ---------------------------------------------------------------

        /// <summary>
        /// Re-evaluates whether the ghost's current grid position is valid and
        /// updates the ghost colour accordingly.
        /// </summary>
        private void RefreshValidation()
        {
            if (ghostInstance == null || gridManager == null)
            {
                return;
            }

            Vector2Int effectiveSize = ghostInstance.GetRotatedSize();
            isCurrentPositionValid = gridManager.IsValidPosition(currentGridPos, effectiveSize);

            ghostInstance.SetColor(isCurrentPositionValid ? validColor : invalidColor);
        }

        // ---------------------------------------------------------------
        //  Ghost lifecycle helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Destroys the ghost instance and resets cached references.
        /// </summary>
        private void CleanUpGhost()
        {
            if (ghostInstance != null)
            {
                Destroy(ghostInstance.gameObject);
                ghostInstance = null;
            }

            sourcePrefab = null;
        }

        /// <summary>
        /// Disables all <see cref="Collider2D"/> and <see cref="Rigidbody2D"/>
        /// components on the ghost so it cannot interact with game physics.
        /// Also disables 3D colliders for hybrid setups.
        /// </summary>
        private static void DisablePhysics(GameObject root)
        {
            foreach (Collider2D col in root.GetComponentsInChildren<Collider2D>(true))
            {
                col.enabled = false;
            }

            foreach (Rigidbody2D rb in root.GetComponentsInChildren<Rigidbody2D>(true))
            {
                rb.simulated = false;
            }

            foreach (Collider col3D in root.GetComponentsInChildren<Collider>(true))
            {
                col3D.enabled = false;
            }

            foreach (Rigidbody rb3D in root.GetComponentsInChildren<Rigidbody>(true))
            {
                rb3D.isKinematic = true;
            }
        }

        /// <summary>
        /// Sets the sorting order on all <see cref="SpriteRenderer"/> components
        /// in the ghost hierarchy so it always draws above placed objects.
        /// </summary>
        private static void SetGhostSortingOrder(GameObject root, int order)
        {
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingOrder = order;
            }
        }
    }
}
