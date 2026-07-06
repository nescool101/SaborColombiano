using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SaborColombiano.AI
{
    // ------------------------------------------------------------------ //
    //  Enums & helper structs
    // ------------------------------------------------------------------ //

    /// <summary>
    /// All possible states a chef can be in during a shift.
    /// </summary>
    public enum ChefState
    {
        /// <summary>Waiting at the assigned station or idle area for a new order.</summary>
        Idle,
        /// <summary>Walking toward the appropriate cooking station.</summary>
        WalkingToStation,
        /// <summary>Prepping ingredients (chopping, measuring, etc.).</summary>
        Preparing,
        /// <summary>Actively cooking the dish (frying, boiling, grilling).</summary>
        Cooking,
        /// <summary>Plating the finished dish for presentation.</summary>
        Plating,
        /// <summary>Dish is ready; waiting for a waiter to pick it up.</summary>
        WaitingForPickup
    }

    /// <summary>
    /// Recipe category used by the chef specialization system.
    /// Maps to the categories defined in the Menu namespace.
    /// </summary>
    public enum RecipeCategory
    {
        /// <summary>No specialization.</summary>
        None,
        /// <summary>Soups and broths (e.g., Ajiaco, Sancocho).</summary>
        Sopas,
        /// <summary>Main courses (e.g., Bandeja Paisa, Lechona).</summary>
        PlatoFuerte,
        /// <summary>Appetizers and snacks (e.g., Empanadas, Arepas).</summary>
        Entradas,
        /// <summary>Desserts (e.g., Tres Leches, Obleas).</summary>
        Postres,
        /// <summary>Beverages (e.g., Aguapanela, Lulada).</summary>
        Bebidas
    }

    /// <summary>
    /// The result of a cooking attempt. Returned by <see cref="ChefAI"/>
    /// events so listeners can react to success or failure.
    /// </summary>
    [Serializable]
    public struct CookingResult
    {
        /// <summary>The recipe that was being cooked.</summary>
        public ScriptableObject Recipe;

        /// <summary>Whether the dish was burned.</summary>
        public bool Burned;

        /// <summary>
        /// Quality bonus from the chef's skill (0-1). 0 = baseline quality,
        /// 1 = maximum skill bonus. Negative means burned-dish penalty.
        /// </summary>
        public float QualityBonus;

        /// <summary>The customer who ordered this dish (may be null).</summary>
        public CustomerAI OrderingCustomer;
    }

    // ------------------------------------------------------------------ //
    //  Main component
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Drives a single chef's behaviour using a coroutine-based workflow.
    /// The chef picks orders from a shared queue, walks to the correct
    /// cooking station, prepares and cooks the dish, then plates it and
    /// waits for a waiter to pick it up. Skill level affects cook time,
    /// burn chance, and quality bonus. Chefs may specialise in a
    /// <see cref="RecipeCategory"/> for a speed bonus.
    /// </summary>
    public class ChefAI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>Raised when the chef starts cooking a dish.</summary>
        public event Action<ChefAI, ScriptableObject> OnCookingStarted;

        /// <summary>Raised when a dish is successfully completed.</summary>
        public event Action<ChefAI, CookingResult> OnDishReady;

        /// <summary>Raised when a dish is burned.</summary>
        public event Action<ChefAI, CookingResult> OnDishBurned;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("Movement")]

        [SerializeField]
        [Tooltip("Base movement speed in world units per second.")]
        private float _baseSpeed = 3.5f;

        [Header("Cooking")]

        [SerializeField]
        [Tooltip("Base preparation time in seconds (modified by skill).")]
        private float _basePrepTime = 4f;

        [SerializeField]
        [Tooltip("Base cooking time in seconds (modified by skill and specialization).")]
        private float _baseCookTime = 8f;

        [SerializeField]
        [Tooltip("Base plating time in seconds (modified by skill).")]
        private float _basePlatingTime = 2f;

        [Header("Skill")]

        [SerializeField]
        [Tooltip("Cooking skill level (0 = novice, 10 = master). Affects cook time, quality, and burn chance.")]
        [Range(0, 10)]
        private int _cookingSkillLevel = 1;

        [SerializeField]
        [Tooltip("Base burn chance at skill level 0 (0-1). Decreases with skill.")]
        [Range(0f, 0.5f)]
        private float _baseBurnChance = 0.15f;

        [Header("Specialization")]

        [SerializeField]
        [Tooltip("The recipe category this chef specialises in. Grants a speed bonus for matching dishes.")]
        private RecipeCategory _specialization = RecipeCategory.None;

        [SerializeField]
        [Tooltip("Speed multiplier applied when cooking a dish that matches the chef's specialization. Lower = faster.")]
        [Range(0.5f, 1f)]
        private float _specializationSpeedMultiplier = 0.75f;

        [Header("Upgrades")]

        [SerializeField]
        [Tooltip("Speed upgrade level (0 = base). Each level adds 10% movement speed.")]
        [Range(0, 10)]
        private int _speedLevel = 0;

        [Header("Station")]

        [SerializeField]
        [Tooltip("The cooking station transform this chef is assigned to. Set at runtime or in the inspector.")]
        private Transform _assignedStation;

        [SerializeField]
        [Tooltip("Grid position of the assigned cooking station.")]
        private Vector2Int _stationGridPosition;

        [SerializeField]
        [Tooltip("Grid position of the order pickup window where tickets appear.")]
        private Vector2Int _orderWindowPosition;

        [SerializeField]
        [Tooltip("Grid position of the dish ready window where finished dishes wait for waiters.")]
        private Vector2Int _dishReadyPosition;

        [Header("Animator")]

        [SerializeField]
        [Tooltip("Optional Animator for cooking, walking, and idle animations.")]
        private Animator _animator;

        // ------------------------------------------------------------------ //
        //  Public properties
        // ------------------------------------------------------------------ //

        /// <summary>Current behavioural state.</summary>
        public ChefState CurrentState { get; private set; } = ChefState.Idle;

        /// <summary>Effective movement speed factoring in upgrades.</summary>
        public float Speed => _baseSpeed * (1f + _speedLevel * 0.1f);

        /// <summary>
        /// Cooking skill level (0-10). Higher skill means faster cooking,
        /// lower burn chance, and better quality bonus.
        /// </summary>
        public int CookingSkill => _cookingSkillLevel;

        /// <summary>This chef's recipe specialization.</summary>
        public RecipeCategory Specialization => _specialization;

        /// <summary>The station transform this chef is assigned to.</summary>
        public Transform AssignedStation => _assignedStation;

        /// <summary>
        /// The recipe the chef is currently working on, or <c>null</c> if idle.
        /// Typed as <c>ScriptableObject</c> until the Recipe SO is available.
        /// </summary>
        public ScriptableObject CurrentOrder { get; private set; }

        /// <summary>The customer who placed the current order (may be null).</summary>
        public CustomerAI CurrentOrderCustomer { get; private set; }

        /// <summary>Whether the chef is currently busy (not idle).</summary>
        public bool IsBusy => CurrentState != ChefState.Idle;

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        private Coroutine _workLoopCoroutine;
        private bool[,] _walkableGrid;
        private float _cellSize = 1f;
        private Vector3 _gridOrigin = Vector3.zero;

        /// <summary>
        /// Shared order queue. Orders are added externally and consumed by
        /// whichever chef is idle first. Protected by a simple dequeue pattern
        /// (not thread-safe, but Unity is single-threaded).
        /// </summary>
        private static readonly List<(ScriptableObject recipe, CustomerAI customer, RecipeCategory category)> _sharedOrderQueue =
            new List<(ScriptableObject, CustomerAI, RecipeCategory)>();

        /// <summary>Flag set when a waiter picks up the plated dish.</summary>
        private bool _dishPickedUp;

        // ------------------------------------------------------------------ //
        //  Initialization
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Configures the chef with grid data and station positions. Should
        /// be called by the restaurant manager during setup.
        /// </summary>
        /// <param name="walkableGrid">Grid occupancy snapshot.</param>
        /// <param name="cellSize">World-space size of one grid cell.</param>
        /// <param name="gridOrigin">World-space origin of the grid.</param>
        /// <param name="stationGridPos">Grid position of the chef's station.</param>
        /// <param name="orderWindowPos">Grid position of the order ticket window.</param>
        /// <param name="dishReadyPos">Grid position of the finished-dish window.</param>
        public void Initialize(
            bool[,] walkableGrid,
            float cellSize,
            Vector3 gridOrigin,
            Vector2Int stationGridPos,
            Vector2Int orderWindowPos,
            Vector2Int dishReadyPos)
        {
            _walkableGrid = walkableGrid;
            _cellSize = cellSize;
            _gridOrigin = gridOrigin;
            _stationGridPosition = stationGridPos;
            _orderWindowPosition = orderWindowPos;
            _dishReadyPosition = dishReadyPos;

            if (_workLoopCoroutine == null)
            {
                _workLoopCoroutine = StartCoroutine(WorkLoop());
            }
        }

        // ------------------------------------------------------------------ //
        //  Static order queue API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Adds a new order to the shared order queue. Called by the
        /// restaurant/kitchen system when a waiter submits an order ticket.
        /// </summary>
        /// <param name="recipe">The recipe ScriptableObject to cook.</param>
        /// <param name="customer">The customer who ordered (for tracking).</param>
        /// <param name="category">The recipe category (for specialization matching).</param>
        public static void EnqueueOrder(ScriptableObject recipe, CustomerAI customer, RecipeCategory category = RecipeCategory.None)
        {
            _sharedOrderQueue.Add((recipe, customer, category));
            Debug.Log($"[ChefAI] Order enqueued: {(recipe != null ? recipe.name : "null")} (queue size: {_sharedOrderQueue.Count})");
        }

        /// <summary>
        /// Clears all orders from the shared queue. Useful for day-end cleanup.
        /// </summary>
        public static void ClearOrderQueue()
        {
            _sharedOrderQueue.Clear();
        }

        /// <summary>Returns the number of orders currently waiting in the queue.</summary>
        public static int PendingOrderCount => _sharedOrderQueue.Count;

        // ------------------------------------------------------------------ //
        //  Instance order API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Directly assigns an order to this specific chef, bypassing the
        /// shared queue. The chef must be idle.
        /// </summary>
        /// <param name="recipe">The recipe to cook.</param>
        /// <param name="customer">The customer who ordered (optional).</param>
        /// <param name="category">The recipe category (for specialization matching).</param>
        /// <returns><c>true</c> if the order was accepted.</returns>
        public bool AssignOrder(ScriptableObject recipe, CustomerAI customer = null, RecipeCategory category = RecipeCategory.None)
        {
            if (IsBusy)
            {
                Debug.LogWarning($"[ChefAI] {gameObject.name}: Cannot assign order -- chef is busy.");
                return false;
            }

            CurrentOrder = recipe;
            CurrentOrderCustomer = customer;
            _directOrderCategory = category;
            _hasDirectOrder = true;
            return true;
        }

        /// <summary>
        /// Returns the recipe the chef is currently working on, or <c>null</c>.
        /// </summary>
        public ScriptableObject GetCurrentOrder() => CurrentOrder;

        /// <summary>
        /// Manually marks the current order as complete. Primarily used
        /// if the order needs to be cancelled externally.
        /// </summary>
        public void CompleteOrder()
        {
            CurrentOrder = null;
            CurrentOrderCustomer = null;
            _hasDirectOrder = false;
            SetState(ChefState.Idle);
        }

        /// <summary>
        /// Called by the waiter when picking up the plated dish from the
        /// ready window.
        /// </summary>
        public void DishPickedUp()
        {
            _dishPickedUp = true;
        }

        // Direct-order fields (bypass shared queue).
        private bool _hasDirectOrder;
        private RecipeCategory _directOrderCategory;

        // ------------------------------------------------------------------ //
        //  Upgrade API
        // ------------------------------------------------------------------ //

        /// <summary>Increases the movement speed upgrade level by one, up to 10.</summary>
        public void UpgradeSpeed()
        {
            _speedLevel = Mathf.Min(_speedLevel + 1, 10);
            Debug.Log($"[ChefAI] {gameObject.name}: Speed upgraded to level {_speedLevel} (speed: {Speed:F1})");
        }

        /// <summary>Increases the cooking skill level by one, up to 10.</summary>
        public void UpgradeSkill()
        {
            _cookingSkillLevel = Mathf.Min(_cookingSkillLevel + 1, 10);
            Debug.Log($"[ChefAI] {gameObject.name}: Cooking skill upgraded to level {_cookingSkillLevel}");
        }

        /// <summary>
        /// Sets the chef's specialization. A chef can only specialise in one
        /// category at a time.
        /// </summary>
        /// <param name="category">The new specialization.</param>
        public void SetSpecialization(RecipeCategory category)
        {
            _specialization = category;
            Debug.Log($"[ChefAI] {gameObject.name}: Specialization set to {_specialization}");
        }

        /// <summary>
        /// Assigns a new cooking station to this chef.
        /// </summary>
        /// <param name="station">The station transform.</param>
        /// <param name="stationGridPos">Grid position of the station.</param>
        public void AssignStation(Transform station, Vector2Int stationGridPos)
        {
            _assignedStation = station;
            _stationGridPosition = stationGridPos;
        }

        /// <summary>
        /// Updates the walkable grid reference (e.g., after kitchen renovation).
        /// </summary>
        public void RefreshWalkableGrid(bool[,] walkableGrid)
        {
            _walkableGrid = walkableGrid;
        }

        // ------------------------------------------------------------------ //
        //  Work loop
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Infinite coroutine that repeatedly checks for orders (direct or
        /// queued) and processes them through the full cooking pipeline.
        /// </summary>
        private IEnumerator WorkLoop()
        {
            while (true)
            {
                // Check for a direct order first.
                if (_hasDirectOrder && CurrentOrder != null)
                {
                    _hasDirectOrder = false;
                    yield return StartCoroutine(ProcessOrder(CurrentOrder, CurrentOrderCustomer, _directOrderCategory));
                    continue;
                }

                // Check the shared queue.
                if (_sharedOrderQueue.Count > 0)
                {
                    // Prefer orders matching the chef's specialization.
                    int bestIndex = FindBestOrderIndex();

                    if (bestIndex >= 0)
                    {
                        var order = _sharedOrderQueue[bestIndex];
                        _sharedOrderQueue.RemoveAt(bestIndex);

                        CurrentOrder = order.recipe;
                        CurrentOrderCustomer = order.customer;

                        yield return StartCoroutine(ProcessOrder(order.recipe, order.customer, order.category));
                        continue;
                    }
                }

                // Nothing to do -- remain idle.
                if (CurrentState != ChefState.Idle)
                {
                    SetState(ChefState.Idle);
                    SetAnimatorBool("IsCooking", false);
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        /// <summary>
        /// Finds the index of the best order in the shared queue.
        /// Prefers orders that match the chef's specialization.
        /// Returns -1 if the queue is empty.
        /// </summary>
        private int FindBestOrderIndex()
        {
            if (_sharedOrderQueue.Count == 0)
                return -1;

            // First pass: look for a specialization match.
            if (_specialization != RecipeCategory.None)
            {
                for (int i = 0; i < _sharedOrderQueue.Count; i++)
                {
                    if (_sharedOrderQueue[i].category == _specialization)
                        return i;
                }
            }

            // No match found; take the first available order.
            return 0;
        }

        // ------------------------------------------------------------------ //
        //  Cooking pipeline
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Full cooking pipeline: walk to station, prepare, cook, plate,
        /// then wait for pickup.
        /// </summary>
        private IEnumerator ProcessOrder(ScriptableObject recipe, CustomerAI customer, RecipeCategory category)
        {
            OnCookingStarted?.Invoke(this, recipe);
            Debug.Log($"[ChefAI] {gameObject.name}: Starting to cook {(recipe != null ? recipe.name : "unknown")}");

            // -- Walk to station --
            SetState(ChefState.WalkingToStation);
            yield return StartCoroutine(NavigateToGrid(_stationGridPosition));

            // -- Prepare --
            SetState(ChefState.Preparing);
            SetAnimatorTrigger("Prepare");
            float prepTime = _basePrepTime * GetSkillTimeMultiplier();
            yield return new WaitForSeconds(prepTime);

            // -- Cook --
            SetState(ChefState.Cooking);
            SetAnimatorBool("IsCooking", true);

            float cookTime = _baseCookTime * GetSkillTimeMultiplier();
            if (_specialization != RecipeCategory.None && category == _specialization)
            {
                cookTime *= _specializationSpeedMultiplier;
            }
            yield return new WaitForSeconds(cookTime);

            SetAnimatorBool("IsCooking", false);

            // Determine if the dish burned.
            bool burned = DidDishBurn();

            if (burned)
            {
                CookingResult burnResult = new CookingResult
                {
                    Recipe = recipe,
                    Burned = true,
                    QualityBonus = -0.5f,
                    OrderingCustomer = customer
                };

                Debug.LogWarning($"[ChefAI] {gameObject.name}: Burned the dish! ({(recipe != null ? recipe.name : "unknown")})");
                OnDishBurned?.Invoke(this, burnResult);

                // Burned dishes still get plated (lower quality), but the chef
                // could also retry. For simplicity we proceed with the burned dish.
            }

            // -- Plate --
            SetState(ChefState.Plating);
            SetAnimatorTrigger("Plate");
            float plateTime = _basePlatingTime * GetSkillTimeMultiplier();
            yield return new WaitForSeconds(plateTime);

            // -- Place dish at ready window --
            yield return StartCoroutine(NavigateToGrid(_dishReadyPosition));

            // -- Wait for pickup --
            SetState(ChefState.WaitingForPickup);
            _dishPickedUp = false;

            CookingResult result = new CookingResult
            {
                Recipe = recipe,
                Burned = burned,
                QualityBonus = burned ? -0.5f : GetQualityBonus(),
                OrderingCustomer = customer
            };

            OnDishReady?.Invoke(this, result);

            // Wait until a waiter picks up the dish (or a timeout).
            float pickupWait = 0f;
            const float maxPickupWait = 30f;

            while (!_dishPickedUp && pickupWait < maxPickupWait)
            {
                pickupWait += Time.deltaTime;
                yield return null;
            }

            if (!_dishPickedUp)
            {
                Debug.LogWarning($"[ChefAI] {gameObject.name}: Dish waited too long for pickup. Discarding.");
            }

            // Reset for next order.
            CurrentOrder = null;
            CurrentOrderCustomer = null;
            SetState(ChefState.Idle);
        }

        // ------------------------------------------------------------------ //
        //  Skill calculations
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns a multiplier (0.5 - 1.0) that reduces cooking/prep/plating
        /// times based on the chef's skill level. Skill 0 = 1.0 (full time),
        /// skill 10 = 0.5 (half time).
        /// </summary>
        private float GetSkillTimeMultiplier()
        {
            return Mathf.Lerp(1.0f, 0.5f, _cookingSkillLevel / 10f);
        }

        /// <summary>
        /// Determines whether the current dish is burned based on the chef's
        /// skill level. Higher skill drastically reduces burn chance.
        /// Skill 0: base burn chance. Skill 10: near zero.
        /// </summary>
        private bool DidDishBurn()
        {
            float burnChance = _baseBurnChance * (1f - _cookingSkillLevel * 0.09f);
            burnChance = Mathf.Max(burnChance, 0.01f); // minimum 1% chance

            return UnityEngine.Random.value < burnChance;
        }

        /// <summary>
        /// Returns a quality bonus (0-1) based on the chef's skill level.
        /// This bonus is factored into customer satisfaction.
        /// Skill 0 = 0.0, skill 10 = 1.0.
        /// </summary>
        private float GetQualityBonus()
        {
            return _cookingSkillLevel / 10f;
        }

        // ------------------------------------------------------------------ //
        //  Navigation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Calculates a path from the chef's current position to the target
        /// grid cell and follows it step-by-step.
        /// </summary>
        private IEnumerator NavigateToGrid(Vector2Int target)
        {
            SetAnimatorBool("IsWalking", true);

            Vector2Int currentGrid = WorldToGrid(transform.position);
            List<Vector2Int> path = SimplePathfinding.FindPath(currentGrid, target, _walkableGrid);

            if (path.Count > 0)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 worldTarget = GridToWorld(path[i]);
                    yield return StartCoroutine(MoveToWorldPosition(worldTarget));
                }
            }
            else
            {
                // Fallback: lerp directly.
                Vector3 worldTarget = GridToWorld(target);
                yield return StartCoroutine(MoveToWorldPosition(worldTarget));
            }

            SetAnimatorBool("IsWalking", false);
        }

        /// <summary>
        /// Smoothly moves the transform toward <paramref name="target"/> at
        /// <see cref="Speed"/>.
        /// </summary>
        private IEnumerator MoveToWorldPosition(Vector3 target)
        {
            const float arrivalThreshold = 0.05f;

            while (Vector3.Distance(transform.position, target) > arrivalThreshold)
            {
                Vector3 direction = (target - transform.position).normalized;
                transform.position = Vector3.MoveTowards(
                    transform.position, target, Speed * Time.deltaTime);

                UpdateWalkDirection(direction);
                yield return null;
            }

            transform.position = target;
        }

        // ------------------------------------------------------------------ //
        //  Grid <-> World conversion
        // ------------------------------------------------------------------ //

        /// <summary>Converts a grid position to a world-space position.</summary>
        private Vector3 GridToWorld(Vector2Int gridPos)
        {
            return _gridOrigin + new Vector3(
                gridPos.x * _cellSize + _cellSize * 0.5f,
                gridPos.y * _cellSize + _cellSize * 0.5f,
                0f);
        }

        /// <summary>Converts a world-space position to the nearest grid position.</summary>
        private Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 local = worldPos - _gridOrigin;
            int x = Mathf.FloorToInt(local.x / _cellSize);
            int y = Mathf.FloorToInt(local.y / _cellSize);
            return new Vector2Int(x, y);
        }

        // ------------------------------------------------------------------ //
        //  Animator helpers
        // ------------------------------------------------------------------ //

        /// <summary>Safely sets a bool parameter on the animator.</summary>
        private void SetAnimatorBool(string paramName, bool value)
        {
            if (_animator != null)
                _animator.SetBool(paramName, value);
        }

        /// <summary>Safely fires a trigger on the animator.</summary>
        private void SetAnimatorTrigger(string paramName)
        {
            if (_animator != null)
                _animator.SetTrigger(paramName);
        }

        /// <summary>
        /// Sets directional animator parameters based on the current
        /// movement vector. Uses "MoveX" and "MoveY" float params.
        /// </summary>
        private void UpdateWalkDirection(Vector3 direction)
        {
            if (_animator == null)
                return;

            _animator.SetFloat("MoveX", direction.x);
            _animator.SetFloat("MoveY", direction.y);
        }

        // ------------------------------------------------------------------ //
        //  State management
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Updates <see cref="CurrentState"/> and logs the transition.
        /// </summary>
        private void SetState(ChefState newState)
        {
            if (CurrentState == newState)
                return;

            ChefState previous = CurrentState;
            CurrentState = newState;
            Debug.Log($"[ChefAI] {gameObject.name}: {previous} -> {newState}");
        }

        // ------------------------------------------------------------------ //
        //  Cleanup
        // ------------------------------------------------------------------ //

        private void OnDestroy()
        {
            if (_workLoopCoroutine != null)
            {
                StopCoroutine(_workLoopCoroutine);
                _workLoopCoroutine = null;
            }
        }
    }
}
