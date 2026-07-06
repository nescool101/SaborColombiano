using System;
using System.Collections.Generic;
using UnityEngine;
using SaborColombiano.Data;

namespace SaborColombiano.Core
{
    /// <summary>
    /// Describes the kind of economic transaction that was recorded.
    /// </summary>
    public enum TransactionType
    {
        /// <summary>Income from a customer paying for their meal.</summary>
        CustomerPayment,

        /// <summary>Tip left by a satisfied customer.</summary>
        Tip,

        /// <summary>Cost of purchasing a menu item or ingredient.</summary>
        MenuPurchase,

        /// <summary>Cost of purchasing or placing furniture.</summary>
        FurniturePurchase,

        /// <summary>Cost of an upgrade (staff, kitchen, etc.).</summary>
        Upgrade,

        /// <summary>Estrellas earned from an achievement.</summary>
        AchievementReward,

        /// <summary>Estrellas spent in the premium shop.</summary>
        PremiumPurchase,

        /// <summary>Miscellaneous / other.</summary>
        Other
    }

    /// <summary>
    /// An immutable record of a single economic transaction.
    /// </summary>
    [Serializable]
    public struct Transaction
    {
        /// <summary>Type of the transaction.</summary>
        public TransactionType type;

        /// <summary>
        /// Signed amount of Pesos involved. Positive = earned, negative = spent.
        /// </summary>
        public long pesosAmount;

        /// <summary>
        /// Signed amount of Estrellas involved. Positive = earned, negative = spent.
        /// </summary>
        public int estrellasAmount;

        /// <summary>Human-readable description (e.g. "Bandeja Paisa sold").</summary>
        public string description;

        /// <summary>In-game day when the transaction occurred.</summary>
        public int day;
    }

    /// <summary>
    /// Manages the two-currency economy of Sabor Colombiano:
    /// <list type="bullet">
    ///   <item><b>Pesos</b> -- the primary soft currency earned from customers.</item>
    ///   <item><b>Estrellas</b> -- a premium hard currency earned slowly via achievements.</item>
    /// </list>
    /// Provides spend/earn helpers, affordability checks, tip calculations,
    /// and a rolling transaction history.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>Raised whenever the Pesos balance changes. Receives the new balance.</summary>
        public event Action<long> OnPesosChanged;

        /// <summary>Raised whenever the Estrellas balance changes. Receives the new balance.</summary>
        public event Action<int> OnEstrellasChanged;

        /// <summary>
        /// Raised after any completed transaction. Receives the <see cref="Transaction"/> record.
        /// </summary>
        public event Action<Transaction> OnTransactionComplete;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("Starting Balances")]

        [SerializeField]
        [Tooltip("Pesos the player starts with on a new game.")]
        private long _startingPesos = 5000;

        [SerializeField]
        [Tooltip("Estrellas the player starts with on a new game.")]
        private int _startingEstrellas = 5;

        [Header("Tips")]

        [SerializeField]
        [Tooltip("Base tip percentage when satisfaction = 1. Actual tip is " +
                 "baseTip * satisfaction^tipCurveExponent.")]
        [Range(0f, 0.5f)]
        private float _baseTipPercentage = 0.15f;

        [SerializeField]
        [Tooltip("Exponent for the tip satisfaction curve. Higher values " +
                 "make tips drop off more steeply with lower satisfaction.")]
        [Range(1f, 4f)]
        private float _tipCurveExponent = 2f;

        [Header("History")]

        [SerializeField]
        [Tooltip("Maximum number of transactions to keep in the rolling history.")]
        [Range(10, 500)]
        private int _maxTransactionHistory = 100;

        // ------------------------------------------------------------------ //
        //  Public properties
        // ------------------------------------------------------------------ //

        /// <summary>Current Pesos balance.</summary>
        public long Pesos { get; private set; }

        /// <summary>Current Estrellas balance.</summary>
        public int Estrellas { get; private set; }

        /// <summary>
        /// Read-only view of the transaction history, newest first.
        /// </summary>
        public IReadOnlyList<Transaction> TransactionHistory => _transactionHistory;

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        private readonly List<Transaction> _transactionHistory = new List<Transaction>();

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            Pesos = _startingPesos;
            Estrellas = _startingEstrellas;
        }

        // ------------------------------------------------------------------ //
        //  Pesos
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Adds Pesos to the player's balance.
        /// </summary>
        /// <param name="amount">Non-negative amount to add.</param>
        /// <param name="type">Transaction type for history tracking.</param>
        /// <param name="description">Optional description for the ledger.</param>
        public void AddPesos(long amount, TransactionType type = TransactionType.CustomerPayment,
                             string description = "")
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[EconomyManager] AddPesos called with non-positive amount.");
                return;
            }

            Pesos += amount;
            OnPesosChanged?.Invoke(Pesos);
            RecordTransaction(type, amount, 0, description);
        }

        /// <summary>
        /// Attempts to spend Pesos. Returns <c>true</c> if the player can
        /// afford the cost and the balance was deducted; <c>false</c> otherwise.
        /// </summary>
        /// <param name="amount">Non-negative amount to spend.</param>
        /// <param name="type">Transaction type for history tracking.</param>
        /// <param name="description">Optional description for the ledger.</param>
        /// <returns><c>true</c> if the purchase succeeded.</returns>
        public bool SpendPesos(long amount, TransactionType type = TransactionType.Other,
                               string description = "")
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[EconomyManager] SpendPesos called with non-positive amount.");
                return false;
            }

            if (Pesos < amount)
            {
                Debug.Log($"[EconomyManager] Cannot afford {amount} Pesos (have {Pesos}).");
                return false;
            }

            Pesos -= amount;
            OnPesosChanged?.Invoke(Pesos);
            RecordTransaction(type, -amount, 0, description);
            return true;
        }

        // ------------------------------------------------------------------ //
        //  Estrellas
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Adds Estrellas to the player's balance.
        /// </summary>
        /// <param name="amount">Non-negative amount to add.</param>
        /// <param name="type">Transaction type for history tracking.</param>
        /// <param name="description">Optional description for the ledger.</param>
        public void AddEstrellas(int amount, TransactionType type = TransactionType.AchievementReward,
                                 string description = "")
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[EconomyManager] AddEstrellas called with non-positive amount.");
                return;
            }

            Estrellas += amount;
            OnEstrellasChanged?.Invoke(Estrellas);
            RecordTransaction(type, 0, amount, description);
        }

        /// <summary>
        /// Attempts to spend Estrellas. Returns <c>true</c> if affordable.
        /// </summary>
        /// <param name="amount">Non-negative amount to spend.</param>
        /// <param name="type">Transaction type for history tracking.</param>
        /// <param name="description">Optional description for the ledger.</param>
        /// <returns><c>true</c> if the purchase succeeded.</returns>
        public bool SpendEstrellas(int amount, TransactionType type = TransactionType.PremiumPurchase,
                                   string description = "")
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[EconomyManager] SpendEstrellas called with non-positive amount.");
                return false;
            }

            if (Estrellas < amount)
            {
                Debug.Log($"[EconomyManager] Cannot afford {amount} Estrellas (have {Estrellas}).");
                return false;
            }

            Estrellas -= amount;
            OnEstrellasChanged?.Invoke(Estrellas);
            RecordTransaction(type, 0, -amount, description);
            return true;
        }

        // ------------------------------------------------------------------ //
        //  Affordability
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Checks whether the player can afford a given cost in both currencies.
        /// Pass 0 for either currency if it is not required.
        /// </summary>
        /// <param name="pesosCost">Required Pesos.</param>
        /// <param name="estrellasCost">Required Estrellas.</param>
        /// <returns><c>true</c> if the player has enough of both currencies.</returns>
        public bool CanAfford(long pesosCost, int estrellasCost = 0)
        {
            return Pesos >= pesosCost && Estrellas >= estrellasCost;
        }

        // ------------------------------------------------------------------ //
        //  Revenue calculation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Calculates the total revenue (meal price + tip) a single customer
        /// generates, based on the dish price and their satisfaction.
        /// </summary>
        /// <param name="dishPrice">Base price of the dish in Pesos.</param>
        /// <param name="satisfaction">Customer satisfaction in [0, 1].</param>
        /// <returns>
        /// A tuple of (mealRevenue, tipAmount) both in Pesos.
        /// </returns>
        public (long mealRevenue, long tipAmount) CalculateCustomerRevenue(
            long dishPrice, float satisfaction)
        {
            satisfaction = Mathf.Clamp01(satisfaction);

            // Meal revenue scales slightly with satisfaction (80-100 %).
            float mealMultiplier = Mathf.Lerp(0.8f, 1f, satisfaction);
            long mealRevenue = Mathf.Max(1, Mathf.RoundToInt(dishPrice * mealMultiplier));

            long tipAmount = CalculateTip(dishPrice, satisfaction);

            return (mealRevenue, tipAmount);
        }

        /// <summary>
        /// Calculates the tip for a given dish price and customer satisfaction.
        /// Tip = dishPrice * baseTipPercentage * satisfaction ^ tipCurveExponent.
        /// Very unhappy customers leave no tip.
        /// </summary>
        /// <param name="dishPrice">Base price of the dish in Pesos.</param>
        /// <param name="satisfaction">Customer satisfaction in [0, 1].</param>
        /// <returns>Tip amount in Pesos (>= 0).</returns>
        public long CalculateTip(long dishPrice, float satisfaction)
        {
            satisfaction = Mathf.Clamp01(satisfaction);

            if (satisfaction < 0.2f)
                return 0; // very unhappy customers do not tip

            float tipFraction = _baseTipPercentage * Mathf.Pow(satisfaction, _tipCurveExponent);
            long tip = Mathf.Max(0, Mathf.RoundToInt(dishPrice * tipFraction));
            return tip;
        }

        // ------------------------------------------------------------------ //
        //  Transaction history
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Records a transaction in the rolling history buffer and fires
        /// <see cref="OnTransactionComplete"/>.
        /// </summary>
        private void RecordTransaction(TransactionType type, long pesos,
                                       int estrellas, string description)
        {
            int day = GameManager.Instance != null
                ? GameManager.Instance.CurrentDay
                : 0;

            Transaction tx = new Transaction
            {
                type = type,
                pesosAmount = pesos,
                estrellasAmount = estrellas,
                description = description,
                day = day
            };

            _transactionHistory.Insert(0, tx); // newest first

            // Trim history.
            while (_transactionHistory.Count > _maxTransactionHistory)
                _transactionHistory.RemoveAt(_transactionHistory.Count - 1);

            OnTransactionComplete?.Invoke(tx);
        }

        // ------------------------------------------------------------------ //
        //  Serialisation helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Writes the economy state into the supplied <see cref="GameData"/>
        /// for persistence.
        /// </summary>
        public void WriteToData(GameData data)
        {
            data.pesos = Pesos;
            data.estrellas = Estrellas;
        }

        /// <summary>
        /// Restores the economy state from a previously saved
        /// <see cref="GameData"/> object.
        /// </summary>
        public void LoadFromData(GameData data)
        {
            Pesos = Math.Max(0L, data.pesos);
            Estrellas = Math.Max(0, data.estrellas);

            OnPesosChanged?.Invoke(Pesos);
            OnEstrellasChanged?.Invoke(Estrellas);

            Debug.Log($"[EconomyManager] Loaded: {Pesos} Pesos, {Estrellas} Estrellas.");
        }
    }
}
