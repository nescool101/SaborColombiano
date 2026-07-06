using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Types from other game namespaces. These are referenced for design intent;
// the concrete classes will be implemented in their respective assemblies.
// Until those files exist the project will show unresolved-reference warnings
// which will clear up as the other systems are written.
using SaborColombiano.Core;
// using SaborColombiano.Grid;
// using SaborColombiano.Menu;

namespace SaborColombiano.AI
{
    // ------------------------------------------------------------------ //
    //  Enums & helper structs
    // ------------------------------------------------------------------ //

    /// <summary>
    /// All possible states a customer can be in during their visit.
    /// </summary>
    public enum CustomerState
    {
        /// <summary>Customer has spawned and is walking toward the entrance.</summary>
        Entering,
        /// <summary>Standing at the entrance waiting for a free table.</summary>
        WaitingForSeat,
        /// <summary>Walking to the assigned table/seat.</summary>
        WalkingToSeat,
        /// <summary>Seated and waiting for a waiter to take the order.</summary>
        WaitingToOrder,
        /// <summary>Order placed; waiting for the food to arrive.</summary>
        WaitingForFood,
        /// <summary>Food has arrived; customer is eating.</summary>
        Eating,
        /// <summary>Customer is paying the bill.</summary>
        Paying,
        /// <summary>Customer is walking out of the restaurant.</summary>
        Leaving
    }

    /// <summary>
    /// Lightweight data carrier for a customer's visual indicator
    /// (thought bubble content, patience bar fill, emoji type).
    /// </summary>
    [Serializable]
    public struct CustomerVisualState
    {
        /// <summary>Sprite to display in the thought bubble (dish icon, "?", etc.).</summary>
        public Sprite thoughtBubbleIcon;

        /// <summary>Normalised patience (0-1).</summary>
        public float patienceFill;

        /// <summary>
        /// Simple satisfaction tier used for the emoji indicator.
        /// 0 = angry, 1 = unhappy, 2 = neutral, 3 = happy, 4 = ecstatic.
        /// </summary>
        public int satisfactionTier;
    }

    // ------------------------------------------------------------------ //
    //  Main component
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Drives a single customer's behaviour inside the restaurant using a
    /// coroutine-based state machine. The customer enters, waits for a seat,
    /// orders, eats, pays, and leaves -- with patience draining at every
    /// waiting stage. Satisfaction and tip are calculated at checkout.
    /// </summary>
    public class CustomerAI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>Raised when the customer sits down at a table.</summary>
        public event Action<CustomerAI> OnCustomerSeated;

        /// <summary>Raised when the customer places their order.</summary>
        public event Action<CustomerAI> OnCustomerOrdered;

        /// <summary>Raised when the customer receives their food.</summary>
        public event Action<CustomerAI> OnCustomerServed;

        /// <summary>
        /// Raised when the customer leaves. The <c>float</c> payload is the
        /// tip amount (0 if the customer left angry).
        /// </summary>
        public event Action<CustomerAI, float> OnCustomerLeft;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("Movement")]

        [SerializeField]
        [Tooltip("Movement speed in world units per second.")]
        private float _moveSpeed = 3f;

        [Header("Patience")]

        [SerializeField]
        [Tooltip("Maximum patience in seconds. Patience drains while the customer waits.")]
        private float _maxPatience = 60f;

        [SerializeField]
        [Tooltip("Patience drain rate multiplier while waiting for a seat (before being seated).")]
        private float _seatWaitDrainMultiplier = 1.5f;

        [SerializeField]
        [Tooltip("Patience drain rate multiplier while waiting for a waiter to take the order.")]
        private float _orderWaitDrainMultiplier = 1.0f;

        [SerializeField]
        [Tooltip("Patience drain rate multiplier while waiting for the food to arrive.")]
        private float _foodWaitDrainMultiplier = 0.8f;

        [Header("Eating")]

        [SerializeField]
        [Tooltip("Base eating duration in seconds (scaled by dish complexity).")]
        private float _baseEatingDuration = 10f;

        [Header("Paying")]

        [SerializeField]
        [Tooltip("Duration in seconds the paying animation/state takes.")]
        private float _payDuration = 2f;

        [Header("Satisfaction")]

        [SerializeField]
        [Tooltip("Base satisfaction score (0-100). Modified by wait times, food quality, etc.")]
        private float _baseSatisfaction = 75f;

        [SerializeField]
        [Tooltip("Satisfaction penalty per second of total wait time beyond a grace period.")]
        private float _waitTimePenaltyPerSecond = 0.5f;

        [SerializeField]
        [Tooltip("Grace period (seconds) before wait-time penalties begin.")]
        private float _waitTimeGraceSeconds = 10f;

        [Header("Tips")]

        [SerializeField]
        [Tooltip("Base tip percentage of the dish price at 100% satisfaction.")]
        [Range(0f, 1f)]
        private float _baseTipPercent = 0.15f;

        [Header("Visuals")]

        [SerializeField]
        [Tooltip("SpriteRenderer or UI Image used for the thought bubble icon.")]
        private SpriteRenderer _thoughtBubbleRenderer;

        [SerializeField]
        [Tooltip("Transform of the patience bar fill (local x-scale = fill).")]
        private Transform _patienceBarFill;

        [SerializeField]
        [Tooltip("SpriteRenderer for the satisfaction emoji.")]
        private SpriteRenderer _satisfactionEmojiRenderer;

        [SerializeField]
        [Tooltip("Sprites for satisfaction tiers (0 = angry .. 4 = ecstatic).")]
        private Sprite[] _satisfactionEmojis;

        [Header("Animator")]

        [SerializeField]
        [Tooltip("Optional Animator for walk/idle/eat triggers.")]
        private Animator _animator;

        // ------------------------------------------------------------------ //
        //  Public properties
        // ------------------------------------------------------------------ //

        /// <summary>Current behavioural state.</summary>
        public CustomerState CurrentState { get; private set; } = CustomerState.Entering;

        /// <summary>Remaining patience in seconds.</summary>
        public float Patience { get; private set; }

        /// <summary>Normalised patience (0-1).</summary>
        public float PatienceNormalised => Mathf.Clamp01(Patience / _maxPatience);

        /// <summary>Overall satisfaction score (0-100). Finalised at checkout.</summary>
        public float Satisfaction { get; private set; }

        /// <summary>
        /// The dish this customer ordered. <c>null</c> until the order is placed.
        /// Typed as <c>ScriptableObject</c> until the Recipe SO is available;
        /// cast to <c>Recipe</c> at point of use.
        /// </summary>
        public ScriptableObject CurrentOrder { get; private set; }

        /// <summary>Grid position of the assigned seat, if any.</summary>
        public Vector2Int AssignedSeatPosition { get; private set; }

        /// <summary>The table instance the customer is assigned to (set externally).</summary>
        public Transform AssignedTable { get; private set; }

        /// <summary>Whether the customer has been fully served (food delivered).</summary>
        public bool HasBeenServed { get; private set; }

        /// <summary>Whether the customer left angry (patience ran out).</summary>
        public bool LeftAngry { get; private set; }

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        private List<Vector2Int> _currentPath;
        private int _pathIndex;
        private Coroutine _stateMachineCoroutine;
        private float _totalWaitTime;

        /// <summary>
        /// The grid position of the restaurant entrance. Should be set by the
        /// spawner via <see cref="Initialize"/> before the state machine starts.
        /// </summary>
        private Vector2Int _entrancePosition;

        /// <summary>
        /// The grid position of the exit. May be the same as the entrance.
        /// </summary>
        private Vector2Int _exitPosition;

        /// <summary>
        /// Callback the customer uses to request a free seat from the
        /// restaurant system. Returns <c>true</c> and sets the out params
        /// when a seat is available.
        /// </summary>
        private Func<CustomerAI, (bool success, Vector2Int seatPos, Transform table)> _requestSeatFunc;

        /// <summary>
        /// Delegate used to fetch a random dish from the active menu.
        /// Returns a Recipe ScriptableObject (typed as SO until the Menu
        /// namespace exists).
        /// </summary>
        private Func<ScriptableObject> _getRandomDishFunc;

        /// <summary>
        /// Delegate to submit a placed order to the restaurant's kitchen queue.
        /// </summary>
        private Action<CustomerAI, ScriptableObject> _submitOrderAction;

        /// <summary>
        /// Reference to the walkable grid snapshot provided at init.
        /// </summary>
        private bool[,] _walkableGrid;

        /// <summary>
        /// Cached world-space offset per grid cell for coordinate conversion.
        /// </summary>
        private float _cellSize = 1f;

        /// <summary>
        /// World-space origin of the grid (bottom-left).
        /// </summary>
        private Vector3 _gridOrigin = Vector3.zero;

        // ------------------------------------------------------------------ //
        //  Initialization
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Configures the customer with all external dependencies and starts
        /// the state machine. Should be called by the customer spawner
        /// immediately after instantiation.
        /// </summary>
        /// <param name="entrancePos">Grid position of the restaurant entrance.</param>
        /// <param name="exitPos">Grid position of the exit (may equal entrance).</param>
        /// <param name="walkableGrid">Grid occupancy snapshot (<c>true</c> = walkable).</param>
        /// <param name="cellSize">World-space size of one grid cell.</param>
        /// <param name="gridOrigin">World-space position of grid cell (0,0).</param>
        /// <param name="requestSeat">
        /// Callback to request a seat. Returns success flag, seat grid pos, and table transform.
        /// </param>
        /// <param name="getRandomDish">Returns a random Recipe SO from the active menu.</param>
        /// <param name="submitOrder">Submits the order to the kitchen queue.</param>
        public void Initialize(
            Vector2Int entrancePos,
            Vector2Int exitPos,
            bool[,] walkableGrid,
            float cellSize,
            Vector3 gridOrigin,
            Func<CustomerAI, (bool, Vector2Int, Transform)> requestSeat,
            Func<ScriptableObject> getRandomDish,
            Action<CustomerAI, ScriptableObject> submitOrder)
        {
            _entrancePosition = entrancePos;
            _exitPosition = exitPos;
            _walkableGrid = walkableGrid;
            _cellSize = cellSize;
            _gridOrigin = gridOrigin;
            _requestSeatFunc = requestSeat;
            _getRandomDishFunc = getRandomDish;
            _submitOrderAction = submitOrder;

            Patience = _maxPatience;
            Satisfaction = _baseSatisfaction;
            HasBeenServed = false;
            LeftAngry = false;
            _totalWaitTime = 0f;

            _stateMachineCoroutine = StartCoroutine(RunStateMachine());
        }

        // ------------------------------------------------------------------ //
        //  External triggers (called by WaiterAI / restaurant systems)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called by the waiter when taking this customer's order.
        /// Transitions the customer from <see cref="CustomerState.WaitingToOrder"/>
        /// to <see cref="CustomerState.WaitingForFood"/>.
        /// </summary>
        public void OrderTaken()
        {
            // The coroutine polls a flag; setting the order triggers the transition.
            if (CurrentState == CustomerState.WaitingToOrder && CurrentOrder != null)
            {
                // Order already chosen; the waiter "writes it down".
                _orderTakenFlag = true;
            }
        }

        /// <summary>
        /// Called by the waiter when delivering the food to this customer.
        /// Transitions from <see cref="CustomerState.WaitingForFood"/> to
        /// <see cref="CustomerState.Eating"/>.
        /// </summary>
        public void FoodDelivered()
        {
            if (CurrentState == CustomerState.WaitingForFood)
            {
                HasBeenServed = true;
                _foodDeliveredFlag = true;
            }
        }

        // Flags polled by the coroutine state machine.
        private volatile bool _orderTakenFlag;
        private volatile bool _foodDeliveredFlag;

        // ------------------------------------------------------------------ //
        //  State machine coroutine
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Master coroutine that drives the customer through every state
        /// sequentially. Each state is a separate helper method that yields
        /// until its exit condition is met.
        /// </summary>
        private IEnumerator RunStateMachine()
        {
            // -- Entering --
            yield return StartCoroutine(State_Entering());

            // -- WaitingForSeat --
            yield return StartCoroutine(State_WaitingForSeat());
            if (LeftAngry) yield break;

            // -- WalkingToSeat --
            yield return StartCoroutine(State_WalkingToSeat());

            // -- WaitingToOrder --
            yield return StartCoroutine(State_WaitingToOrder());
            if (LeftAngry) yield break;

            // -- WaitingForFood --
            yield return StartCoroutine(State_WaitingForFood());
            if (LeftAngry) yield break;

            // -- Eating --
            yield return StartCoroutine(State_Eating());

            // -- Paying --
            yield return StartCoroutine(State_Paying());

            // -- Leaving --
            yield return StartCoroutine(State_Leaving());
        }

        // ------------------------------------------------------------------ //
        //  Individual state coroutines
        // ------------------------------------------------------------------ //

        /// <summary>Walk from spawn position to the restaurant entrance.</summary>
        private IEnumerator State_Entering()
        {
            SetState(CustomerState.Entering);
            SetAnimatorBool("IsWalking", true);

            // Move toward the entrance world position.
            Vector3 entranceWorld = GridToWorld(_entrancePosition);
            yield return StartCoroutine(MoveToWorldPosition(entranceWorld));

            SetAnimatorBool("IsWalking", false);
        }

        /// <summary>
        /// Wait at the entrance until a seat is assigned or patience runs out.
        /// </summary>
        private IEnumerator State_WaitingForSeat()
        {
            SetState(CustomerState.WaitingForSeat);
            UpdateThoughtBubble(null); // generic "waiting" icon

            while (true)
            {
                // Try to get a seat.
                if (_requestSeatFunc != null)
                {
                    var result = _requestSeatFunc.Invoke(this);
                    if (result.success)
                    {
                        AssignedSeatPosition = result.seatPos;
                        AssignedTable = result.table;
                        break;
                    }
                }

                // Drain patience.
                float drain = Time.deltaTime * _seatWaitDrainMultiplier;
                Patience -= drain;
                _totalWaitTime += Time.deltaTime;
                UpdatePatienceVisual();

                if (Patience <= 0f)
                {
                    yield return StartCoroutine(LeaveAngry());
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>Walk from the entrance to the assigned seat via pathfinding.</summary>
        private IEnumerator State_WalkingToSeat()
        {
            SetState(CustomerState.WalkingToSeat);
            SetAnimatorBool("IsWalking", true);

            Vector2Int currentGrid = WorldToGrid(transform.position);
            List<Vector2Int> path = SimplePathfinding.FindPath(currentGrid, AssignedSeatPosition, _walkableGrid);

            if (path.Count > 0)
            {
                yield return StartCoroutine(FollowPath(path));
            }
            else
            {
                // Fallback: lerp directly if pathfinding fails.
                Vector3 seatWorld = GridToWorld(AssignedSeatPosition);
                yield return StartCoroutine(MoveToWorldPosition(seatWorld));
            }

            SetAnimatorBool("IsWalking", false);
            OnCustomerSeated?.Invoke(this);
        }

        /// <summary>
        /// Seated -- choose a dish and wait for the waiter to take the order.
        /// </summary>
        private IEnumerator State_WaitingToOrder()
        {
            SetState(CustomerState.WaitingToOrder);

            // Choose a random dish from the menu.
            if (_getRandomDishFunc != null)
            {
                CurrentOrder = _getRandomDishFunc.Invoke();
            }

            // Show what the customer wants in the thought bubble.
            // (The thought bubble icon would normally come from Recipe.icon;
            //  here we just pass null and let the UI system handle defaults.)
            UpdateThoughtBubble(null);

            _orderTakenFlag = false;

            while (!_orderTakenFlag)
            {
                float drain = Time.deltaTime * _orderWaitDrainMultiplier;
                Patience -= drain;
                _totalWaitTime += Time.deltaTime;
                UpdatePatienceVisual();

                if (Patience <= 0f)
                {
                    yield return StartCoroutine(LeaveAngry());
                    yield break;
                }

                yield return null;
            }

            // Order has been taken by the waiter.
            if (_submitOrderAction != null && CurrentOrder != null)
            {
                _submitOrderAction.Invoke(this, CurrentOrder);
            }

            OnCustomerOrdered?.Invoke(this);
        }

        /// <summary>
        /// Wait for the waiter to deliver the food.
        /// </summary>
        private IEnumerator State_WaitingForFood()
        {
            SetState(CustomerState.WaitingForFood);
            _foodDeliveredFlag = false;

            while (!_foodDeliveredFlag)
            {
                float drain = Time.deltaTime * _foodWaitDrainMultiplier;
                Patience -= drain;
                _totalWaitTime += Time.deltaTime;
                UpdatePatienceVisual();

                if (Patience <= 0f)
                {
                    yield return StartCoroutine(LeaveAngry());
                    yield break;
                }

                yield return null;
            }

            OnCustomerServed?.Invoke(this);
        }

        /// <summary>
        /// Eat the food. Duration is based on the dish plus a base time.
        /// </summary>
        private IEnumerator State_Eating()
        {
            SetState(CustomerState.Eating);
            SetAnimatorTrigger("Eat");

            // Eating duration: base + small random variation.
            float eatTime = _baseEatingDuration + UnityEngine.Random.Range(-2f, 3f);
            eatTime = Mathf.Max(eatTime, 3f);

            float elapsed = 0f;
            while (elapsed < eatTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Calculate satisfaction and tip, then play a short paying animation.
        /// </summary>
        private IEnumerator State_Paying()
        {
            SetState(CustomerState.Paying);

            CalculateFinalSatisfaction();
            UpdateSatisfactionVisual();

            yield return new WaitForSeconds(_payDuration);
        }

        /// <summary>
        /// Walk to the exit and then despawn.
        /// </summary>
        private IEnumerator State_Leaving()
        {
            SetState(CustomerState.Leaving);
            SetAnimatorBool("IsWalking", true);

            Vector2Int currentGrid = WorldToGrid(transform.position);
            List<Vector2Int> path = SimplePathfinding.FindPath(currentGrid, _exitPosition, _walkableGrid);

            if (path.Count > 0)
            {
                yield return StartCoroutine(FollowPath(path));
            }
            else
            {
                Vector3 exitWorld = GridToWorld(_exitPosition);
                yield return StartCoroutine(MoveToWorldPosition(exitWorld));
            }

            SetAnimatorBool("IsWalking", false);

            float tip = CalculateTip();
            OnCustomerLeft?.Invoke(this, tip);

            // Give the event a frame to propagate before destroying.
            yield return null;
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ //
        //  Leave angry
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Early exit path when patience hits zero. The customer storms out
        /// with zero tip and flags <see cref="LeftAngry"/>.
        /// </summary>
        private IEnumerator LeaveAngry()
        {
            LeftAngry = true;
            Satisfaction = 0f;
            UpdateSatisfactionVisual();

            Debug.Log($"[CustomerAI] Customer {gameObject.name} ran out of patience and is leaving angry!");

            SetState(CustomerState.Leaving);
            SetAnimatorBool("IsWalking", true);

            Vector2Int currentGrid = WorldToGrid(transform.position);
            List<Vector2Int> path = SimplePathfinding.FindPath(currentGrid, _exitPosition, _walkableGrid);

            if (path.Count > 0)
            {
                yield return StartCoroutine(FollowPath(path));
            }
            else
            {
                Vector3 exitWorld = GridToWorld(_exitPosition);
                yield return StartCoroutine(MoveToWorldPosition(exitWorld));
            }

            SetAnimatorBool("IsWalking", false);
            OnCustomerLeft?.Invoke(this, 0f);

            yield return null;
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ //
        //  Movement helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Follows a sequence of grid positions, lerping between each waypoint.
        /// </summary>
        private IEnumerator FollowPath(List<Vector2Int> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 target = GridToWorld(path[i]);
                yield return StartCoroutine(MoveToWorldPosition(target));
            }
        }

        /// <summary>
        /// Smoothly moves the transform toward <paramref name="target"/> at
        /// <see cref="_moveSpeed"/> until within a tiny threshold.
        /// </summary>
        private IEnumerator MoveToWorldPosition(Vector3 target)
        {
            const float arrivalThreshold = 0.05f;

            while (Vector3.Distance(transform.position, target) > arrivalThreshold)
            {
                Vector3 direction = (target - transform.position).normalized;
                transform.position = Vector3.MoveTowards(
                    transform.position, target, _moveSpeed * Time.deltaTime);

                UpdateWalkDirection(direction);
                yield return null;
            }

            transform.position = target;
        }

        // ------------------------------------------------------------------ //
        //  Satisfaction & tips
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Computes the final satisfaction score factoring in total wait time.
        /// Additional factors (food quality, cleanliness, staff speed) can be
        /// plugged in here once those systems are online.
        /// </summary>
        private void CalculateFinalSatisfaction()
        {
            float waitPenalty = 0f;
            float excessWait = _totalWaitTime - _waitTimeGraceSeconds;
            if (excessWait > 0f)
            {
                waitPenalty = excessWait * _waitTimePenaltyPerSecond;
            }

            Satisfaction = Mathf.Clamp(_baseSatisfaction - waitPenalty, 0f, 100f);
        }

        /// <summary>
        /// Calculates the tip amount based on satisfaction.
        /// At 100 satisfaction the tip equals <c>dishPrice * _baseTipPercent</c>.
        /// Below 25 satisfaction the customer tips nothing.
        /// </summary>
        private float CalculateTip()
        {
            if (LeftAngry || Satisfaction < 25f)
                return 0f;

            // Placeholder dish price -- replace with CurrentOrder.price once
            // the Recipe ScriptableObject is available.
            float dishPrice = 15000f; // COP (Colombian pesos) placeholder

            float tipMultiplier = Satisfaction / 100f;
            return dishPrice * _baseTipPercent * tipMultiplier;
        }

        // ------------------------------------------------------------------ //
        //  Visual helpers
        // ------------------------------------------------------------------ //

        /// <summary>Updates the thought bubble sprite.</summary>
        private void UpdateThoughtBubble(Sprite icon)
        {
            if (_thoughtBubbleRenderer != null)
            {
                _thoughtBubbleRenderer.sprite = icon;
                _thoughtBubbleRenderer.enabled = icon != null;
            }
        }

        /// <summary>Updates the patience bar fill based on current patience.</summary>
        private void UpdatePatienceVisual()
        {
            if (_patienceBarFill != null)
            {
                Vector3 scale = _patienceBarFill.localScale;
                scale.x = PatienceNormalised;
                _patienceBarFill.localScale = scale;
            }
        }

        /// <summary>
        /// Sets the satisfaction emoji based on the current satisfaction tier.
        /// </summary>
        private void UpdateSatisfactionVisual()
        {
            if (_satisfactionEmojiRenderer == null || _satisfactionEmojis == null || _satisfactionEmojis.Length == 0)
                return;

            int tier = SatisfactionToTier(Satisfaction);
            tier = Mathf.Clamp(tier, 0, _satisfactionEmojis.Length - 1);
            _satisfactionEmojiRenderer.sprite = _satisfactionEmojis[tier];
            _satisfactionEmojiRenderer.enabled = true;
        }

        /// <summary>
        /// Maps a 0-100 satisfaction value to a 0-4 tier.
        /// 0 = angry, 1 = unhappy, 2 = neutral, 3 = happy, 4 = ecstatic.
        /// </summary>
        private static int SatisfactionToTier(float satisfaction)
        {
            if (satisfaction < 20f) return 0;
            if (satisfaction < 40f) return 1;
            if (satisfaction < 60f) return 2;
            if (satisfaction < 80f) return 3;
            return 4;
        }

        /// <summary>
        /// Returns a snapshot of the customer's visual state for external UI
        /// systems to read without coupling to internal fields.
        /// </summary>
        public CustomerVisualState GetVisualState()
        {
            return new CustomerVisualState
            {
                thoughtBubbleIcon = _thoughtBubbleRenderer != null ? _thoughtBubbleRenderer.sprite : null,
                patienceFill = PatienceNormalised,
                satisfactionTier = SatisfactionToTier(Satisfaction)
            };
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
        //  State setter
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Updates <see cref="CurrentState"/> and logs the transition for
        /// debugging purposes.
        /// </summary>
        private void SetState(CustomerState newState)
        {
            if (CurrentState == newState)
                return;

            CustomerState previous = CurrentState;
            CurrentState = newState;
            Debug.Log($"[CustomerAI] {gameObject.name}: {previous} -> {newState}");
        }

        // ------------------------------------------------------------------ //
        //  Cleanup
        // ------------------------------------------------------------------ //

        private void OnDestroy()
        {
            if (_stateMachineCoroutine != null)
            {
                StopCoroutine(_stateMachineCoroutine);
                _stateMachineCoroutine = null;
            }
        }
    }
}
