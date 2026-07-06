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
    /// All possible states a waiter can be in during a shift.
    /// </summary>
    public enum WaiterState
    {
        /// <summary>Waiting for a task at the home/idle position.</summary>
        Idle,
        /// <summary>Walking toward a customer to take their order.</summary>
        WalkingToCustomer,
        /// <summary>Standing at the table writing down the order.</summary>
        TakingOrder,
        /// <summary>Walking to the kitchen to submit the order ticket.</summary>
        WalkingToKitchen,
        /// <summary>Handing the order ticket to the kitchen.</summary>
        DeliveringOrder,
        /// <summary>Walking to the kitchen to pick up a ready dish.</summary>
        WalkingWithFood,
        /// <summary>Placing the dish on the customer's table.</summary>
        ServingFood,
        /// <summary>Returning to the idle position or to the next task.</summary>
        Returning
    }

    /// <summary>
    /// The kind of action a waiter can perform.
    /// </summary>
    public enum WaiterTaskType
    {
        /// <summary>Walk to a customer and record their order.</summary>
        TakeOrder,
        /// <summary>Carry a finished dish from the kitchen to the customer.</summary>
        DeliverFood,
        /// <summary>Clean a table after a customer leaves.</summary>
        Clean
    }

    /// <summary>
    /// Describes a single unit of work for a waiter.
    /// </summary>
    [Serializable]
    public struct WaiterTask
    {
        /// <summary>What the waiter needs to do.</summary>
        public WaiterTaskType Type;

        /// <summary>Grid position the waiter must walk to.</summary>
        public Vector2Int TargetPosition;

        /// <summary>
        /// The customer related to this task (nullable for Clean tasks).
        /// </summary>
        public CustomerAI RelatedCustomer;

        /// <summary>
        /// The recipe/dish associated with the task (nullable for TakeOrder / Clean).
        /// Typed as <c>ScriptableObject</c> until the Recipe SO is available.
        /// </summary>
        public ScriptableObject RelatedRecipe;

        /// <summary>
        /// Priority value. Lower numbers are executed first.
        /// Deliver-food tasks generally have higher priority than take-order tasks.
        /// </summary>
        public int Priority;
    }

    // ------------------------------------------------------------------ //
    //  Main component
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Drives a waiter's behaviour using a coroutine-based task queue.
    /// The waiter picks up <see cref="WaiterTask"/> items in priority order
    /// and executes them sequentially, moving via
    /// <see cref="SimplePathfinding"/> between positions.
    /// </summary>
    public class WaiterAI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>Raised when the waiter begins executing a new task.</summary>
        public event Action<WaiterAI, WaiterTask> OnTaskStarted;

        /// <summary>Raised when the waiter finishes executing a task.</summary>
        public event Action<WaiterAI, WaiterTask> OnTaskCompleted;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("Movement")]

        [SerializeField]
        [Tooltip("Base movement speed in world units per second.")]
        private float _baseSpeed = 4f;

        [Header("Efficiency")]

        [SerializeField]
        [Tooltip("Base time in seconds to take an order (lowered by efficiency upgrades).")]
        private float _baseOrderTakingTime = 3f;

        [SerializeField]
        [Tooltip("Base time in seconds to serve a dish.")]
        private float _baseServingTime = 1.5f;

        [SerializeField]
        [Tooltip("Base time in seconds to clean a table.")]
        private float _baseCleanTime = 4f;

        [Header("Upgrades")]

        [SerializeField]
        [Tooltip("Speed upgrade level (0 = base). Each level adds 10% speed.")]
        [Range(0, 10)]
        private int _speedLevel = 0;

        [SerializeField]
        [Tooltip("Efficiency upgrade level (0 = base). Each level reduces task times by 8%.")]
        [Range(0, 10)]
        private int _efficiencyLevel = 0;

        [SerializeField]
        [Tooltip("Capacity upgrade level (0 = carry 1 dish). Each level adds 1.")]
        [Range(0, 5)]
        private int _capacityLevel = 0;

        [Header("Assigned Tables")]

        [SerializeField]
        [Tooltip("Grid positions of the tables this waiter is responsible for.")]
        private List<Vector2Int> _assignedTables = new List<Vector2Int>();

        [Header("Positions")]

        [SerializeField]
        [Tooltip("Grid position the waiter returns to when idle.")]
        private Vector2Int _homePosition;

        [SerializeField]
        [Tooltip("Grid position of the kitchen order window.")]
        private Vector2Int _kitchenPosition;

        [SerializeField]
        [Tooltip("Grid position of the kitchen pickup window.")]
        private Vector2Int _kitchenPickupPosition;

        [Header("Animator")]

        [SerializeField]
        [Tooltip("Optional Animator for walk/idle animation triggers.")]
        private Animator _animator;

        // ------------------------------------------------------------------ //
        //  Public properties
        // ------------------------------------------------------------------ //

        /// <summary>Current behavioural state.</summary>
        public WaiterState CurrentState { get; private set; } = WaiterState.Idle;

        /// <summary>Effective movement speed factoring in upgrades.</summary>
        public float Speed => _baseSpeed * (1f + _speedLevel * 0.1f);

        /// <summary>Effective efficiency multiplier (lower = faster tasks).</summary>
        public float EfficiencyMultiplier => Mathf.Max(0.2f, 1f - _efficiencyLevel * 0.08f);

        /// <summary>Maximum number of dishes the waiter can carry at once.</summary>
        public int Capacity => 1 + _capacityLevel;

        /// <summary>Read-only view of the tables assigned to this waiter.</summary>
        public IReadOnlyList<Vector2Int> AssignedTables => _assignedTables;

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        private readonly List<WaiterTask> _taskQueue = new List<WaiterTask>();
        private WaiterTask? _currentTask;
        private Coroutine _taskLoopCoroutine;

        private bool[,] _walkableGrid;
        private float _cellSize = 1f;
        private Vector3 _gridOrigin = Vector3.zero;

        // ------------------------------------------------------------------ //
        //  Initialization
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Configures the waiter with grid data and key positions. Should be
        /// called by the restaurant manager once during setup.
        /// </summary>
        /// <param name="walkableGrid">Grid occupancy snapshot.</param>
        /// <param name="cellSize">World-space size of one grid cell.</param>
        /// <param name="gridOrigin">World-space origin of the grid.</param>
        /// <param name="homePos">Grid position the waiter idles at.</param>
        /// <param name="kitchenPos">Grid position of the kitchen order window.</param>
        /// <param name="kitchenPickupPos">Grid position of the kitchen pickup window.</param>
        public void Initialize(
            bool[,] walkableGrid,
            float cellSize,
            Vector3 gridOrigin,
            Vector2Int homePos,
            Vector2Int kitchenPos,
            Vector2Int kitchenPickupPos)
        {
            _walkableGrid = walkableGrid;
            _cellSize = cellSize;
            _gridOrigin = gridOrigin;
            _homePosition = homePos;
            _kitchenPosition = kitchenPos;
            _kitchenPickupPosition = kitchenPickupPos;

            if (_taskLoopCoroutine == null)
            {
                _taskLoopCoroutine = StartCoroutine(TaskLoop());
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Adds a task to the waiter's queue. The queue is sorted by priority
        /// each time a new task is inserted.
        /// </summary>
        /// <param name="task">The task to enqueue.</param>
        public void AssignTask(WaiterTask task)
        {
            _taskQueue.Add(task);
            _taskQueue.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            Debug.Log($"[WaiterAI] {gameObject.name}: Task assigned - {task.Type} (queue size: {_taskQueue.Count})");
        }

        /// <summary>
        /// Returns the task the waiter is currently executing, or <c>null</c>
        /// if the waiter is idle.
        /// </summary>
        public WaiterTask? GetCurrentTask() => _currentTask;

        /// <summary>
        /// Immediately completes (and removes) the current task, advancing
        /// the waiter to the next queued task.
        /// </summary>
        public void CompleteTask()
        {
            _currentTask = null;
        }

        /// <summary>
        /// Assigns a set of table positions for this waiter to be responsible for.
        /// </summary>
        /// <param name="tables">Grid positions of the tables.</param>
        public void SetAssignedTables(List<Vector2Int> tables)
        {
            _assignedTables = tables ?? new List<Vector2Int>();
        }

        /// <summary>
        /// Updates the walkable grid reference (e.g., after furniture is moved).
        /// </summary>
        /// <param name="walkableGrid">Updated grid occupancy snapshot.</param>
        public void RefreshWalkableGrid(bool[,] walkableGrid)
        {
            _walkableGrid = walkableGrid;
        }

        // ------------------------------------------------------------------ //
        //  Upgrade API
        // ------------------------------------------------------------------ //

        /// <summary>Increases the speed upgrade level by one, up to 10.</summary>
        public void UpgradeSpeed()
        {
            _speedLevel = Mathf.Min(_speedLevel + 1, 10);
            Debug.Log($"[WaiterAI] {gameObject.name}: Speed upgraded to level {_speedLevel} (speed: {Speed:F1})");
        }

        /// <summary>Increases the efficiency upgrade level by one, up to 10.</summary>
        public void UpgradeEfficiency()
        {
            _efficiencyLevel = Mathf.Min(_efficiencyLevel + 1, 10);
            Debug.Log($"[WaiterAI] {gameObject.name}: Efficiency upgraded to level {_efficiencyLevel} (multiplier: {EfficiencyMultiplier:F2})");
        }

        /// <summary>Increases the capacity upgrade level by one, up to 5.</summary>
        public void UpgradeCapacity()
        {
            _capacityLevel = Mathf.Min(_capacityLevel + 1, 5);
            Debug.Log($"[WaiterAI] {gameObject.name}: Capacity upgraded to level {_capacityLevel} (carry: {Capacity})");
        }

        // ------------------------------------------------------------------ //
        //  Task loop
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Infinite coroutine that continuously dequeues and executes tasks.
        /// When the queue is empty the waiter returns to the idle state.
        /// </summary>
        private IEnumerator TaskLoop()
        {
            while (true)
            {
                if (_taskQueue.Count > 0)
                {
                    _currentTask = _taskQueue[0];
                    _taskQueue.RemoveAt(0);

                    WaiterTask task = _currentTask.Value;
                    OnTaskStarted?.Invoke(this, task);

                    switch (task.Type)
                    {
                        case WaiterTaskType.TakeOrder:
                            yield return StartCoroutine(ExecuteTakeOrder(task));
                            break;

                        case WaiterTaskType.DeliverFood:
                            yield return StartCoroutine(ExecuteDeliverFood(task));
                            break;

                        case WaiterTaskType.Clean:
                            yield return StartCoroutine(ExecuteClean(task));
                            break;
                    }

                    OnTaskCompleted?.Invoke(this, task);
                    _currentTask = null;
                }
                else
                {
                    // Return to home if not already there.
                    if (CurrentState != WaiterState.Idle)
                    {
                        yield return StartCoroutine(ReturnToHome());
                    }

                    // Wait a short interval before checking the queue again.
                    yield return new WaitForSeconds(0.25f);
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Task executors
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Walk to the customer, take their order, then walk to the kitchen
        /// to submit the ticket.
        /// </summary>
        private IEnumerator ExecuteTakeOrder(WaiterTask task)
        {
            // Walk to the customer's table.
            SetState(WaiterState.WalkingToCustomer);
            yield return StartCoroutine(NavigateToGrid(task.TargetPosition));

            // Take the order.
            SetState(WaiterState.TakingOrder);
            float duration = _baseOrderTakingTime * EfficiencyMultiplier;
            yield return new WaitForSeconds(duration);

            // Notify the customer that the order was taken.
            if (task.RelatedCustomer != null)
            {
                task.RelatedCustomer.OrderTaken();
            }

            // Walk to the kitchen to submit the order.
            SetState(WaiterState.WalkingToKitchen);
            yield return StartCoroutine(NavigateToGrid(_kitchenPosition));

            // Submit the order (brief pause at the window).
            SetState(WaiterState.DeliveringOrder);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// Walk to the kitchen pickup, pick up the dish, walk to the customer,
        /// and serve the food.
        /// </summary>
        private IEnumerator ExecuteDeliverFood(WaiterTask task)
        {
            // Walk to the kitchen pickup window.
            SetState(WaiterState.WalkingToKitchen);
            yield return StartCoroutine(NavigateToGrid(_kitchenPickupPosition));

            // Pick up the dish (brief pause).
            yield return new WaitForSeconds(0.5f);

            // Walk to the customer's table with the food.
            SetState(WaiterState.WalkingWithFood);
            yield return StartCoroutine(NavigateToGrid(task.TargetPosition));

            // Serve the food.
            SetState(WaiterState.ServingFood);
            float duration = _baseServingTime * EfficiencyMultiplier;
            yield return new WaitForSeconds(duration);

            // Notify the customer that the food has arrived.
            if (task.RelatedCustomer != null)
            {
                task.RelatedCustomer.FoodDelivered();
            }
        }

        /// <summary>
        /// Walk to the table and spend time cleaning it.
        /// </summary>
        private IEnumerator ExecuteClean(WaiterTask task)
        {
            SetState(WaiterState.WalkingToCustomer);
            yield return StartCoroutine(NavigateToGrid(task.TargetPosition));

            // Clean the table.
            SetState(WaiterState.TakingOrder); // reuse state for "performing action at table"
            float duration = _baseCleanTime * EfficiencyMultiplier;
            yield return new WaitForSeconds(duration);
        }

        /// <summary>
        /// Walks the waiter back to the home/idle position.
        /// </summary>
        private IEnumerator ReturnToHome()
        {
            SetState(WaiterState.Returning);
            yield return StartCoroutine(NavigateToGrid(_homePosition));
            SetState(WaiterState.Idle);
        }

        // ------------------------------------------------------------------ //
        //  Navigation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Calculates a path from the waiter's current grid position to the
        /// target and follows it step-by-step.
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
        /// Smoothly moves toward <paramref name="target"/> using
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
        //  Auto-assignment
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Scans for the nearest waiting customer (in the
        /// <see cref="CustomerState.WaitingToOrder"/> state) among the
        /// waiter's assigned tables and creates a <see cref="WaiterTaskType.TakeOrder"/>
        /// task for them.
        /// </summary>
        /// <param name="customers">All active customers in the restaurant.</param>
        public void AutoAssignNearestWaitingCustomer(IReadOnlyList<CustomerAI> customers)
        {
            if (customers == null || customers.Count == 0)
                return;

            CustomerAI nearest = null;
            float bestDist = float.MaxValue;
            Vector2Int myGrid = WorldToGrid(transform.position);

            for (int i = 0; i < customers.Count; i++)
            {
                CustomerAI c = customers[i];
                if (c == null || c.CurrentState != CustomerState.WaitingToOrder)
                    continue;

                // Only consider customers at tables assigned to this waiter.
                if (_assignedTables.Count > 0 && !_assignedTables.Contains(c.AssignedSeatPosition))
                    continue;

                // Skip if there's already a task for this customer.
                if (HasTaskForCustomer(c))
                    continue;

                float dist = Vector2Int.Distance(myGrid, c.AssignedSeatPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = c;
                }
            }

            if (nearest != null)
            {
                WaiterTask task = new WaiterTask
                {
                    Type = WaiterTaskType.TakeOrder,
                    TargetPosition = nearest.AssignedSeatPosition,
                    RelatedCustomer = nearest,
                    RelatedRecipe = null,
                    Priority = 1
                };
                AssignTask(task);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the waiter's queue already contains a task
        /// targeting the given customer.
        /// </summary>
        private bool HasTaskForCustomer(CustomerAI customer)
        {
            if (_currentTask.HasValue && _currentTask.Value.RelatedCustomer == customer)
                return true;

            for (int i = 0; i < _taskQueue.Count; i++)
            {
                if (_taskQueue[i].RelatedCustomer == customer)
                    return true;
            }

            return false;
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
        private void SetState(WaiterState newState)
        {
            if (CurrentState == newState)
                return;

            WaiterState previous = CurrentState;
            CurrentState = newState;
            Debug.Log($"[WaiterAI] {gameObject.name}: {previous} -> {newState}");
        }

        // ------------------------------------------------------------------ //
        //  Cleanup
        // ------------------------------------------------------------------ //

        private void OnDestroy()
        {
            if (_taskLoopCoroutine != null)
            {
                StopCoroutine(_taskLoopCoroutine);
                _taskLoopCoroutine = null;
            }
        }
    }
}
