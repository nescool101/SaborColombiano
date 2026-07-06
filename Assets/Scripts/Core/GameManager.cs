using System;
using UnityEngine;
using SaborColombiano.Data;

namespace SaborColombiano.Core
{
    /// <summary>
    /// Possible states the game can be in at any given time.
    /// </summary>
    public enum GameState
    {
        Playing,
        Paused,
        Decorating,
        MenuOpen
    }

    /// <summary>
    /// Central singleton manager that orchestrates the entire game loop for Sabor Colombiano.
    /// Owns the game state machine, day/night cycle, time-scale control, and references
    /// to every other top-level manager. Other systems should subscribe to
    /// <see cref="OnGameStateChanged"/> and <see cref="OnDayChanged"/> rather than
    /// polling the GameManager each frame.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Singleton
        // ------------------------------------------------------------------ //

        private static GameManager _instance;

        /// <summary>
        /// Global access point for the GameManager singleton.
        /// Returns <c>null</c> if the manager has not been instantiated yet.
        /// </summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameManager>();
                    if (_instance == null)
                    {
                        Debug.LogError("[GameManager] No GameManager instance found in the scene.");
                    }
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Raised whenever the game transitions to a new <see cref="GameState"/>.
        /// Subscribers receive the previous state and the new state.
        /// </summary>
        public event Action<GameState, GameState> OnGameStateChanged;

        /// <summary>
        /// Raised at the start of each new in-game day.
        /// Subscribers receive the new day number (1-based).
        /// </summary>
        public event Action<int> OnDayChanged;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("Manager References")]

        [SerializeField]
        [Tooltip("Reference to the RestaurantManager in the scene.")]
        private RestaurantManager _restaurantManager;

        [SerializeField]
        [Tooltip("Reference to the EconomyManager in the scene.")]
        private EconomyManager _economyManager;

        // The following managers belong to namespaces we have not written yet.
        // They are typed as MonoBehaviour so the project compiles before those
        // scripts exist; swap to the concrete types once they are available.

        [SerializeField]
        [Tooltip("Reference to the MenuSystem manager.")]
        private MonoBehaviour _menuSystem;

        [SerializeField]
        [Tooltip("Reference to the GridManager.")]
        private MonoBehaviour _gridManager;

        [SerializeField]
        [Tooltip("Reference to the UIManager.")]
        private MonoBehaviour _uiManager;

        [Header("Day / Night Cycle")]

        [SerializeField]
        [Tooltip("Duration of one in-game day measured in real-time seconds.")]
        [Range(30f, 600f)]
        private float _dayDurationSeconds = 120f;

        [Header("Time")]

        [SerializeField]
        [Tooltip("Default game-speed multiplier (1 = normal).")]
        [Range(0.5f, 3f)]
        private float _defaultGameSpeed = 1f;

        [Header("Auto-Save")]

        [SerializeField]
        [Tooltip("Interval in real-time seconds between automatic saves. Set to 0 to disable.")]
        [Range(0f, 600f)]
        private float _autoSaveIntervalSeconds = 60f;

        // ------------------------------------------------------------------ //
        //  Public properties
        // ------------------------------------------------------------------ //

        /// <summary>Current game state.</summary>
        public GameState CurrentState { get; private set; } = GameState.Playing;

        /// <summary>The current in-game day (1-based).</summary>
        public int CurrentDay { get; private set; } = 1;

        /// <summary>
        /// Normalised progress through the current day (0 = dawn, 1 = end of day).
        /// Useful for lighting, NPC schedules, etc.
        /// </summary>
        public float DayProgress => _dayTimer / _dayDurationSeconds;

        /// <summary>Current game-speed multiplier applied to relevant systems.</summary>
        public float GameSpeed { get; private set; }

        /// <summary>Convenience accessor for the RestaurantManager.</summary>
        public RestaurantManager Restaurant => _restaurantManager;

        /// <summary>Convenience accessor for the EconomyManager.</summary>
        public EconomyManager Economy => _economyManager;

        /// <summary>Duration of one in-game day in real seconds.</summary>
        public float DayDurationSeconds => _dayDurationSeconds;

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        private float _dayTimer;
        private float _autoSaveTimer;
        private GameState _stateBeforePause;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            // Enforce singleton pattern.
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate GameManager destroyed.", gameObject);
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            GameSpeed = _defaultGameSpeed;
        }

        private void Start()
        {
            InitializeGame();
        }

        private void Update()
        {
            if (CurrentState != GameState.Playing)
                return;

            float scaledDelta = Time.deltaTime * GameSpeed;

            UpdateDayCycle(scaledDelta);
            UpdateAutoSave(Time.unscaledDeltaTime); // auto-save on real time
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ------------------------------------------------------------------ //
        //  Initialization
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called once at startup. Loads any existing save data and boots
        /// each sub-manager with the restored (or default) values.
        /// </summary>
        private void InitializeGame()
        {
            if (SaveSystem.HasSave())
            {
                GameData data = SaveSystem.LoadGame();
                if (data != null)
                {
                    ApplySaveData(data);
                    Debug.Log("[GameManager] Game loaded from save.");
                }
                else
                {
                    Debug.LogWarning("[GameManager] Save file found but could not be loaded. Starting fresh.");
                    StartNewGame();
                }
            }
            else
            {
                StartNewGame();
            }

            SetGameState(GameState.Playing);
        }

        /// <summary>
        /// Boots the game with default values (no save data).
        /// </summary>
        private void StartNewGame()
        {
            CurrentDay = 1;
            _dayTimer = 0f;
            Debug.Log("[GameManager] New game started. Welcome to Sabor Colombiano!");
        }

        /// <summary>
        /// Restores runtime state from a <see cref="GameData"/> snapshot.
        /// </summary>
        private void ApplySaveData(GameData data)
        {
            CurrentDay = Mathf.Max(1, data.currentDay);
            _dayTimer = data.dayTimer;

            // Delegate restoration to sub-managers.
            if (_restaurantManager != null)
                _restaurantManager.LoadFromData(data);

            if (_economyManager != null)
                _economyManager.LoadFromData(data);
        }

        // ------------------------------------------------------------------ //
        //  State machine
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Transitions the game to a new <see cref="GameState"/>.
        /// Fires <see cref="OnGameStateChanged"/> after the transition.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        public void SetGameState(GameState newState)
        {
            if (newState == CurrentState)
                return;

            GameState previous = CurrentState;
            CurrentState = newState;

            // Adjust Unity time-scale for pause states.
            switch (newState)
            {
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;
                // Decorating and MenuOpen keep time running but game logic
                // is gated by the Playing check in Update().
            }

            OnGameStateChanged?.Invoke(previous, newState);
            Debug.Log($"[GameManager] State changed: {previous} -> {newState}");
        }

        /// <summary>
        /// Pauses the game, remembering the previous state so
        /// <see cref="ResumeGame"/> can restore it.
        /// </summary>
        public void PauseGame()
        {
            if (CurrentState == GameState.Paused)
                return;

            _stateBeforePause = CurrentState;
            SetGameState(GameState.Paused);
        }

        /// <summary>
        /// Resumes the game to whatever state it was in before
        /// <see cref="PauseGame"/> was called.
        /// </summary>
        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused)
                return;

            SetGameState(_stateBeforePause);
        }

        // ------------------------------------------------------------------ //
        //  Time management
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Sets the game-speed multiplier. Clamped to [0.5, 3].
        /// This does NOT alter <c>Time.timeScale</c>; it is applied by
        /// individual systems that need scaled delta time.
        /// </summary>
        /// <param name="speed">Desired speed multiplier.</param>
        public void SetGameSpeed(float speed)
        {
            GameSpeed = Mathf.Clamp(speed, 0.5f, 3f);
            Debug.Log($"[GameManager] Game speed set to {GameSpeed:F1}x");
        }

        // ------------------------------------------------------------------ //
        //  Day / Night cycle
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Advances the day timer and triggers a new day when the
        /// configured duration elapses.
        /// </summary>
        private void UpdateDayCycle(float scaledDelta)
        {
            _dayTimer += scaledDelta;

            if (_dayTimer >= _dayDurationSeconds)
            {
                _dayTimer -= _dayDurationSeconds;
                AdvanceDay();
            }
        }

        /// <summary>
        /// Increments the day counter, fires the <see cref="OnDayChanged"/>
        /// event, and triggers an automatic save.
        /// </summary>
        private void AdvanceDay()
        {
            CurrentDay++;
            OnDayChanged?.Invoke(CurrentDay);
            Debug.Log($"[GameManager] Day {CurrentDay} has begun.");

            // End-of-day save.
            RequestSave();
        }

        // ------------------------------------------------------------------ //
        //  Saving
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Ticks the auto-save timer and performs a save when the interval
        /// elapses. Uses unscaled time so saves happen even at speed 0.
        /// </summary>
        private void UpdateAutoSave(float unscaledDelta)
        {
            if (_autoSaveIntervalSeconds <= 0f)
                return;

            _autoSaveTimer += unscaledDelta;
            if (_autoSaveTimer >= _autoSaveIntervalSeconds)
            {
                _autoSaveTimer = 0f;
                RequestSave();
            }
        }

        /// <summary>
        /// Builds a <see cref="GameData"/> snapshot from the current runtime
        /// state and hands it to <see cref="SaveSystem.SaveGame"/>.
        /// </summary>
        public void RequestSave()
        {
            GameData data = BuildSaveData();
            SaveSystem.SaveGame(data);
        }

        /// <summary>
        /// Collects data from every sub-manager into a serialisable snapshot.
        /// </summary>
        private GameData BuildSaveData()
        {
            GameData data = new GameData
            {
                currentDay = CurrentDay,
                dayTimer = _dayTimer,
                saveTimestamp = DateTime.UtcNow.ToString("o")
            };

            if (_restaurantManager != null)
                _restaurantManager.WriteToData(data);

            if (_economyManager != null)
                _economyManager.WriteToData(data);

            return data;
        }
    }
}
