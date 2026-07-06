using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SaborColombiano.Core;
using SaborColombiano.Grid;

namespace SaborColombiano.AI
{
    /// <summary>
    /// Spawns customers at the restaurant entrance based on reputation and time of day.
    /// Manages the pool of active customers and controls spawn rate.
    /// </summary>
    public class CustomerSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject customerPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private float baseSpawnInterval = 8f;
        [SerializeField] private float minSpawnInterval = 3f;
        [SerializeField] private int maxActiveCustomers = 20;

        [Header("Customer Variety")]
        [SerializeField] private Sprite[] customerSprites;
        [SerializeField] private string[] customerNames = new string[]
        {
            "Carlos", "María", "Andrés", "Valentina", "Santiago",
            "Camila", "Sebastián", "Isabella", "Mateo", "Sofía",
            "Juan Pablo", "Daniela", "Nicolás", "Mariana", "Diego",
            "Luciana", "Alejandro", "Gabriela", "Samuel", "Paula",
            "Felipe", "Natalia", "Tomás", "Carolina", "Emilio",
            "Laura", "Martín", "Ana", "Julián", "Catalina"
        };

        [Header("Pool")]
        [SerializeField] private int poolSize = 30;

        private Queue<GameObject> customerPool = new Queue<GameObject>();
        private List<CustomerAI> activeCustomers = new List<CustomerAI>();
        private Coroutine spawnCoroutine;
        private bool isSpawning;

        /// <summary>Number of customers currently in the restaurant.</summary>
        public int ActiveCustomerCount => activeCustomers.Count;

        /// <summary>All active customers.</summary>
        public IReadOnlyList<CustomerAI> ActiveCustomers => activeCustomers.AsReadOnly();

        private void Start()
        {
            InitializePool();
            StartSpawning();
        }

        private void InitializePool()
        {
            if (customerPrefab == null)
            {
                Debug.LogWarning("[CustomerSpawner] No customer prefab assigned. Creating placeholder.");
                customerPrefab = CreatePlaceholderCustomer();
            }

            for (int i = 0; i < poolSize; i++)
            {
                var obj = Instantiate(customerPrefab, transform);
                obj.SetActive(false);
                customerPool.Enqueue(obj);
            }
        }

        /// <summary>Start the customer spawn loop.</summary>
        public void StartSpawning()
        {
            if (isSpawning) return;
            isSpawning = true;
            spawnCoroutine = StartCoroutine(SpawnLoop());
        }

        /// <summary>Stop spawning new customers.</summary>
        public void StopSpawning()
        {
            isSpawning = false;
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }
        }

        private IEnumerator SpawnLoop()
        {
            // Initial delay
            yield return new WaitForSeconds(2f);

            while (isSpawning)
            {
                float interval = CalculateSpawnInterval();
                yield return new WaitForSeconds(interval);

                if (CanSpawnCustomer())
                {
                    SpawnCustomer();
                }
            }
        }

        private float CalculateSpawnInterval()
        {
            float reputation = 2.5f; // Default
            if (GameManager.Instance != null && GameManager.Instance.Restaurant != null)
            {
                reputation = GameManager.Instance.Restaurant.Reputation;
            }

            // Higher reputation = shorter intervals (more customers)
            float reputationFactor = 1f - (reputation / 5f) * 0.6f; // 0.4 to 1.0
            float interval = baseSpawnInterval * reputationFactor;

            // Add some randomness
            interval *= Random.Range(0.7f, 1.3f);

            return Mathf.Max(interval, minSpawnInterval);
        }

        private bool CanSpawnCustomer()
        {
            if (activeCustomers.Count >= maxActiveCustomers) return false;

            // Check if there are available seats based on restaurant capacity
            if (GameManager.Instance != null && GameManager.Instance.Restaurant != null)
            {
                int maxSeats = GameManager.Instance.Restaurant.MaxCustomers;
                if (activeCustomers.Count >= maxSeats) return false;
            }

            // Check game state
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Playing)
            {
                return false;
            }

            return true;
        }

        private void SpawnCustomer()
        {
            GameObject customerObj = GetFromPool();
            if (customerObj == null) return;

            // Position at spawn point
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
            customerObj.transform.position = spawnPos;
            customerObj.SetActive(true);

            // Configure customer
            var customerAI = customerObj.GetComponent<CustomerAI>();
            if (customerAI != null)
            {
                string randomName = customerNames[Random.Range(0, customerNames.Length)];
                Sprite randomSprite = customerSprites != null && customerSprites.Length > 0
                    ? customerSprites[Random.Range(0, customerSprites.Length)]
                    : null;

                // Apply visual customisation
                customerAI.gameObject.name = $"Customer_{randomName}";
                var sr = customerAI.GetComponent<SpriteRenderer>();
                if (sr != null && randomSprite != null)
                    sr.sprite = randomSprite;

                // Determine grid parameters from GridManager
                var gridManager = FindAnyObjectByType<GridManager>();
                Vector2Int entrancePos = Vector2Int.zero;
                Vector2Int exitPos = Vector2Int.zero;
                float cellSize = 1f;
                Vector3 gridOrigin = Vector3.zero;
                bool[,] walkableGrid = null;

                if (gridManager != null)
                {
                    cellSize = gridManager.CellSize;
                    gridOrigin = gridManager.GridOrigin;
                    walkableGrid = new bool[gridManager.Width, gridManager.Height];
                    // Build a simple walkable grid (all cells without placed objects are walkable)
                    for (int x = 0; x < gridManager.Width; x++)
                    {
                        for (int y = 0; y < gridManager.Height; y++)
                        {
                            walkableGrid[x, y] = gridManager.GetObjectAt(new Vector2Int(x, y)) == null;
                        }
                    }
                    entrancePos = gridManager.WorldToGrid(spawnPos);
                    if (exitPoint != null)
                        exitPos = gridManager.WorldToGrid(exitPoint.position);
                    else
                        exitPos = entrancePos;
                }

                customerAI.Initialize(
                    entrancePos,
                    exitPos,
                    walkableGrid,
                    cellSize,
                    gridOrigin,
                    null,  // requestSeat -- to be wired by restaurant system
                    null,  // getRandomDish -- to be wired by menu system
                    null   // submitOrder -- to be wired by kitchen system
                );
                customerAI.OnCustomerLeft += HandleCustomerLeft;
                activeCustomers.Add(customerAI);
            }
        }

        private void HandleCustomerLeft(CustomerAI customer, float tip)
        {
            customer.OnCustomerLeft -= HandleCustomerLeft;
            activeCustomers.Remove(customer);
            ReturnToPool(customer.gameObject);
        }

        private GameObject GetFromPool()
        {
            if (customerPool.Count > 0)
            {
                return customerPool.Dequeue();
            }

            // Pool exhausted — create new
            if (customerPrefab != null)
            {
                var obj = Instantiate(customerPrefab, transform);
                obj.SetActive(false);
                return obj;
            }

            return null;
        }

        private void ReturnToPool(GameObject obj)
        {
            obj.SetActive(false);
            customerPool.Enqueue(obj);
        }

        private GameObject CreatePlaceholderCustomer()
        {
            var obj = new GameObject("CustomerPrefab");
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<CustomerAI>();
            obj.SetActive(false);
            return obj;
        }

        /// <summary>Remove all active customers immediately (e.g., when closing restaurant).</summary>
        public void ClearAllCustomers()
        {
            foreach (var customer in new List<CustomerAI>(activeCustomers))
            {
                customer.OnCustomerLeft -= HandleCustomerLeft;
                ReturnToPool(customer.gameObject);
            }
            activeCustomers.Clear();
        }

        private void OnDestroy()
        {
            StopSpawning();
        }
    }
}
