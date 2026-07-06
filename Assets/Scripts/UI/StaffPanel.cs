using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SaborColombiano.Core;

namespace SaborColombiano.UI
{
    // ====================================================================== //
    //  Supporting types
    // ====================================================================== //

    /// <summary>
    /// Distinguishes the two staff roles in the restaurant.
    /// </summary>
    public enum StaffType
    {
        /// <summary>Waiter: delivers food to customers, collects payment.</summary>
        Waiter,
        /// <summary>Chef: prepares recipes in the kitchen.</summary>
        Chef
    }

    /// <summary>
    /// Identifies an upgradeable stat on a staff member.
    /// </summary>
    public enum StatType
    {
        /// <summary>Movement/preparation speed.</summary>
        Speed,
        /// <summary>Cooking/serving skill (affects quality and tips).</summary>
        Skill
    }

    /// <summary>
    /// Data class representing a single staff member. Instances are created
    /// when hiring and mutated as the player upgrades or reassigns staff.
    /// </summary>
    [Serializable]
    public class StaffData
    {
        /// <summary>Display name of the staff member.</summary>
        [Tooltip("Display name of the staff member.")]
        public string staffName;

        /// <summary>Role: Waiter or Chef.</summary>
        [Tooltip("Staff member's role.")]
        public StaffType type;

        /// <summary>Current experience level (1-based).</summary>
        [Tooltip("Current level (1-based).")]
        public int level = 1;

        /// <summary>
        /// Speed stat from 0 to 1. Higher means faster service or cooking.
        /// </summary>
        [Tooltip("Speed stat (0-1).")]
        [Range(0f, 1f)]
        public float speed = 0.3f;

        /// <summary>
        /// Skill stat from 0 to 1. Higher means better food quality and tips.
        /// </summary>
        [Tooltip("Skill stat (0-1).")]
        [Range(0f, 1f)]
        public float skill = 0.3f;

        /// <summary>
        /// Chef-only specialization. When assigned, the chef cooks dishes of
        /// that category faster and with higher quality.
        /// Null or unused for waiters.
        /// </summary>
        [Tooltip("Recipe category specialization (chefs only).")]
        public RecipeCategory specialization;

        /// <summary>Whether the chef has a specialization set.</summary>
        [Tooltip("Whether a specialization has been assigned.")]
        public bool hasSpecialization;

        /// <summary>Base cost to hire this staff member (in pesos).</summary>
        [Tooltip("Pesos cost to hire this staff member.")]
        public int hireCost;

        /// <summary>
        /// Unique identifier for save/load and lookup.
        /// </summary>
        [Tooltip("Unique staff identifier.")]
        public string staffId;
    }

    // ====================================================================== //
    //  StaffPanel MonoBehaviour
    // ====================================================================== //

    /// <summary>
    /// Controls the staff management overlay panel. The player can view all
    /// hired staff, inspect their stats, hire new staff, upgrade speed and
    /// skill, assign chef specializations, and fire staff.
    /// <para>
    /// Staff capacity (maximum number of employees) is determined by the
    /// restaurant level. Random Colombian names are assigned to new hires.
    /// </para>
    /// </summary>
    public class StaffPanel : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>Raised when a new staff member is hired.</summary>
        public event Action<StaffData> OnStaffHired;

        /// <summary>Raised when a staff member is fired.</summary>
        public event Action<StaffData> OnStaffFired;

        /// <summary>Raised when a staff member's stats change (upgrade).</summary>
        public event Action<StaffData> OnStaffUpgraded;

        /// <summary>Raised when a chef's specialization is changed.</summary>
        public event Action<StaffData> OnSpecializationChanged;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Staff list
        // ------------------------------------------------------------------ //

        [Header("Staff List")]

        [SerializeField]
        [Tooltip("ScrollRect for the staff list.")]
        private ScrollRect _staffScrollRect;

        [SerializeField]
        [Tooltip("Content container for staff cards.")]
        private RectTransform _staffListContent;

        [SerializeField]
        [Tooltip("Prefab for a single staff card in the list.")]
        private GameObject _staffCardPrefab;

        [SerializeField]
        [Tooltip("Text showing current staff count vs. capacity.")]
        private Text _staffCountText;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Hiring
        // ------------------------------------------------------------------ //

        [Header("Hiring")]

        [SerializeField]
        [Tooltip("Button to hire a new waiter.")]
        private Button _hireWaiterButton;

        [SerializeField]
        [Tooltip("Button to hire a new chef.")]
        private Button _hireChefButton;

        [SerializeField]
        [Tooltip("Text showing the cost to hire the next waiter.")]
        private Text _hireWaiterCostText;

        [SerializeField]
        [Tooltip("Text showing the cost to hire the next chef.")]
        private Text _hireChefCostText;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Staff detail
        // ------------------------------------------------------------------ //

        [Header("Staff Detail")]

        [SerializeField]
        [Tooltip("Root GameObject for the staff detail panel.")]
        private GameObject _detailPanel;

        [SerializeField]
        [Tooltip("Text displaying the staff member's name.")]
        private Text _detailName;

        [SerializeField]
        [Tooltip("Text displaying the staff member's role.")]
        private Text _detailRole;

        [SerializeField]
        [Tooltip("Text displaying the staff member's level.")]
        private Text _detailLevel;

        [SerializeField]
        [Tooltip("Image for the role icon.")]
        private Image _detailRoleIcon;

        [SerializeField]
        [Tooltip("Sprite used for the waiter role icon.")]
        private Sprite _waiterIconSprite;

        [SerializeField]
        [Tooltip("Sprite used for the chef role icon.")]
        private Sprite _chefIconSprite;

        [SerializeField]
        [Tooltip("Slider visualising the staff member's speed stat.")]
        private Slider _detailSpeedBar;

        [SerializeField]
        [Tooltip("Slider visualising the staff member's skill stat.")]
        private Slider _detailSkillBar;

        [SerializeField]
        [Tooltip("Text showing the specialization badge (chefs only).")]
        private Text _detailSpecialization;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Detail actions
        // ------------------------------------------------------------------ //

        [Header("Detail Actions")]

        [SerializeField]
        [Tooltip("Button to upgrade speed.")]
        private Button _upgradeSpeedButton;

        [SerializeField]
        [Tooltip("Text showing the cost to upgrade speed.")]
        private Text _upgradeSpeedCostText;

        [SerializeField]
        [Tooltip("Button to upgrade skill.")]
        private Button _upgradeSkillButton;

        [SerializeField]
        [Tooltip("Text showing the cost to upgrade skill.")]
        private Text _upgradeSkillCostText;

        [SerializeField]
        [Tooltip("Button to assign or change chef specialization.")]
        private Button _assignSpecializationButton;

        [SerializeField]
        [Tooltip("Button to fire the selected staff member.")]
        private Button _fireButton;

        [SerializeField]
        [Tooltip("Button to close the detail panel.")]
        private Button _detailCloseButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Specialization picker
        // ------------------------------------------------------------------ //

        [Header("Specialization Picker")]

        [SerializeField]
        [Tooltip("Root GameObject for the specialization picker popup.")]
        private GameObject _specPickerPopup;

        [SerializeField]
        [Tooltip("Content container for specialization option buttons.")]
        private RectTransform _specPickerContent;

        [SerializeField]
        [Tooltip("Prefab for a specialization option button.")]
        private GameObject _specOptionPrefab;

        [SerializeField]
        [Tooltip("Button to close the specialization picker.")]
        private Button _specPickerCloseButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Fire confirmation
        // ------------------------------------------------------------------ //

        [Header("Fire Confirmation")]

        [SerializeField]
        [Tooltip("Root GameObject for the fire confirmation dialog.")]
        private GameObject _fireConfirmDialog;

        [SerializeField]
        [Tooltip("Text summarising who will be fired.")]
        private Text _fireConfirmText;

        [SerializeField]
        [Tooltip("Confirm button to proceed with firing.")]
        private Button _fireConfirmYesButton;

        [SerializeField]
        [Tooltip("Cancel button to abort firing.")]
        private Button _fireConfirmNoButton;

        // ------------------------------------------------------------------ //
        //  Inspector fields -- Configuration
        // ------------------------------------------------------------------ //

        [Header("Configuration")]

        [SerializeField]
        [Tooltip("Base hire cost in pesos for a waiter.")]
        private int _baseWaiterHireCost = 500;

        [SerializeField]
        [Tooltip("Base hire cost in pesos for a chef.")]
        private int _baseChefHireCost = 800;

        [SerializeField]
        [Tooltip("Cost multiplier applied per existing staff of the same type.")]
        [Range(1f, 2f)]
        private float _hireCostScaling = 1.3f;

        [SerializeField]
        [Tooltip("Base cost in pesos for the first stat upgrade.")]
        private int _baseUpgradeCost = 200;

        [SerializeField]
        [Tooltip("Cost multiplier applied per upgrade level.")]
        [Range(1f, 3f)]
        private float _upgradeCostScaling = 1.5f;

        [SerializeField]
        [Tooltip("Amount added to a stat per upgrade (0-1 scale).")]
        [Range(0.01f, 0.2f)]
        private float _upgradeStatIncrement = 0.05f;

        [SerializeField]
        [Tooltip("Base number of staff slots at level 1.")]
        private int _baseStaffCapacity = 2;

        [SerializeField]
        [Tooltip("Additional staff slots per restaurant level.")]
        private int _slotsPerLevel = 1;

        [SerializeField]
        [Tooltip("Maximum staff capacity regardless of level.")]
        private int _maxStaffCapacity = 20;

        // ------------------------------------------------------------------ //
        //  Colombian name pools
        // ------------------------------------------------------------------ //

        /// <summary>Pool of Colombian first names for male staff.</summary>
        private static readonly string[] MaleNames =
        {
            "Carlos", "Andres", "Santiago", "Juan", "Miguel",
            "Diego", "Felipe", "Sebastian", "Alejandro", "Mateo",
            "Nicolas", "Samuel", "Daniel", "David", "Jose",
            "Camilo", "Esteban", "Julian", "Rafael", "Gabriel"
        };

        /// <summary>Pool of Colombian first names for female staff.</summary>
        private static readonly string[] FemaleNames =
        {
            "Maria", "Valentina", "Camila", "Sofia", "Isabella",
            "Daniela", "Laura", "Natalia", "Alejandra", "Carolina",
            "Catalina", "Mariana", "Gabriela", "Luisa", "Andrea",
            "Paula", "Juliana", "Manuela", "Sara", "Ana"
        };

        /// <summary>Pool of Colombian last names.</summary>
        private static readonly string[] LastNames =
        {
            "Rodriguez", "Garcia", "Martinez", "Lopez", "Hernandez",
            "Gonzalez", "Ramirez", "Torres", "Diaz", "Morales",
            "Restrepo", "Ospina", "Cardona", "Valencia", "Munoz",
            "Castano", "Betancourt", "Arango", "Giraldo", "Mejia"
        };

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        /// <summary>All currently hired staff members.</summary>
        private readonly List<StaffData> _hiredStaff = new List<StaffData>();

        /// <summary>The staff member currently shown in the detail panel.</summary>
        private StaffData _selectedStaff;

        /// <summary>Pool of instantiated staff-card GameObjects.</summary>
        private readonly List<GameObject> _cardPool = new List<GameObject>();

        /// <summary>Pool of instantiated specialization-option GameObjects.</summary>
        private readonly List<GameObject> _specOptionPool = new List<GameObject>();

        /// <summary>Auto-incrementing counter for unique staff IDs.</summary>
        private int _nextStaffId = 1;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            WireButtons();
            HideDetailPanel();
            HideSpecPicker();
            HideFireConfirm();
        }

        private void OnEnable()
        {
            PopulateStaffList();
            UpdateHireButtons();
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Population
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Clears and rebuilds the staff list display from the current
        /// <see cref="_hiredStaff"/> collection.
        /// </summary>
        public void PopulateStaffList()
        {
            // Return pooled cards.
            foreach (GameObject card in _cardPool)
            {
                if (card != null) card.SetActive(false);
            }

            for (int i = 0; i < _hiredStaff.Count; i++)
            {
                GameObject card = GetOrCreateCard(i);
                card.SetActive(true);
                PopulateStaffCard(card, _hiredStaff[i]);
            }

            // Update capacity text.
            int capacity = GetStaffCapacity();
            if (_staffCountText != null)
                _staffCountText.text = $"{_hiredStaff.Count} / {capacity}";

            if (_staffScrollRect != null)
                _staffScrollRect.verticalNormalizedPosition = 1f;
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Selection
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Selects a staff member and opens their detail panel.
        /// </summary>
        /// <param name="staff">The staff member to inspect.</param>
        public void SelectStaff(StaffData staff)
        {
            if (staff == null)
                return;

            _selectedStaff = staff;
            ShowDetailPanel(staff);
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Hiring
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Hires a new staff member of the specified type, deducting the
        /// hire cost from the player's pesos. Fails if the staff roster is
        /// full or the player cannot afford the cost.
        /// </summary>
        /// <param name="type">Waiter or Chef.</param>
        /// <returns>The newly hired <see cref="StaffData"/>, or <c>null</c> on failure.</returns>
        public StaffData HireStaff(StaffType type)
        {
            int capacity = GetStaffCapacity();
            if (_hiredStaff.Count >= capacity)
            {
                Debug.LogWarning($"[StaffPanel] Cannot hire: staff capacity reached ({capacity}).");
                return null;
            }

            int cost = CalculateHireCost(type);

            // Deduct pesos.
            if (GameManager.Instance != null && GameManager.Instance.Economy != null)
            {
                if (!GameManager.Instance.Economy.SpendPesos(cost))
                {
                    Debug.LogWarning($"[StaffPanel] Cannot hire {type}: insufficient pesos (need {cost}).");
                    return null;
                }
            }

            StaffData newStaff = GenerateStaff(type, cost);
            _hiredStaff.Add(newStaff);

            OnStaffHired?.Invoke(newStaff);
            Debug.Log($"[StaffPanel] Hired {type}: {newStaff.staffName} for {cost} pesos.");

            // Notify HUD.
            HUDController hud = FindObjectOfType<HUDController>();
            if (hud != null)
                hud.SpawnExpenseText(cost);

            PopulateStaffList();
            UpdateHireButtons();
            return newStaff;
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Upgrading
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Upgrades a stat (Speed or Skill) on the specified staff member.
        /// Deducts the upgrade cost from pesos.
        /// </summary>
        /// <param name="staff">The staff member to upgrade.</param>
        /// <param name="stat">Which stat to improve.</param>
        /// <returns><c>true</c> if the upgrade was successful.</returns>
        public bool UpgradeStaff(StaffData staff, StatType stat)
        {
            if (staff == null)
                return false;

            float currentValue = stat == StatType.Speed ? staff.speed : staff.skill;
            if (currentValue >= 1f)
            {
                Debug.LogWarning($"[StaffPanel] {staff.staffName}'s {stat} is already maxed.");
                return false;
            }

            int cost = CalculateUpgradeCost(staff, stat);

            if (GameManager.Instance != null && GameManager.Instance.Economy != null)
            {
                if (!GameManager.Instance.Economy.SpendPesos(cost))
                {
                    Debug.LogWarning($"[StaffPanel] Cannot upgrade: insufficient pesos (need {cost}).");
                    return false;
                }
            }

            // Apply upgrade.
            switch (stat)
            {
                case StatType.Speed:
                    staff.speed = Mathf.Clamp01(staff.speed + _upgradeStatIncrement);
                    break;
                case StatType.Skill:
                    staff.skill = Mathf.Clamp01(staff.skill + _upgradeStatIncrement);
                    break;
            }

            staff.level = CalculateStaffLevel(staff);

            OnStaffUpgraded?.Invoke(staff);
            Debug.Log($"[StaffPanel] Upgraded {staff.staffName}'s {stat} to " +
                      $"{(stat == StatType.Speed ? staff.speed : staff.skill):F2} for {cost} pesos.");

            // Refresh UI.
            if (_selectedStaff == staff)
                ShowDetailPanel(staff);
            PopulateStaffList();
            return true;
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Firing
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Fires (removes) a staff member from the roster. This is
        /// irreversible.
        /// </summary>
        /// <param name="staff">The staff member to fire.</param>
        /// <returns><c>true</c> if the staff member was found and removed.</returns>
        public bool FireStaff(StaffData staff)
        {
            if (staff == null)
                return false;

            if (!_hiredStaff.Remove(staff))
            {
                Debug.LogWarning($"[StaffPanel] '{staff.staffName}' is not in the roster.");
                return false;
            }

            OnStaffFired?.Invoke(staff);
            Debug.Log($"[StaffPanel] Fired {staff.staffName}.");

            if (_selectedStaff == staff)
            {
                _selectedStaff = null;
                HideDetailPanel();
            }

            PopulateStaffList();
            UpdateHireButtons();
            return true;
        }

        // ------------------------------------------------------------------ //
        //  Public API -- Data access
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns a read-only view of the hired staff roster.
        /// </summary>
        public IReadOnlyList<StaffData> GetHiredStaff()
        {
            return _hiredStaff.AsReadOnly();
        }

        /// <summary>
        /// Loads a pre-existing staff roster (e.g. from save data).
        /// Replaces the current roster entirely.
        /// </summary>
        /// <param name="staff">The staff list to load.</param>
        public void LoadStaffRoster(List<StaffData> staff)
        {
            _hiredStaff.Clear();
            if (staff != null)
                _hiredStaff.AddRange(staff);

            PopulateStaffList();
            UpdateHireButtons();
        }

        // ------------------------------------------------------------------ //
        //  Button wiring
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Binds all button click listeners.
        /// </summary>
        private void WireButtons()
        {
            // Hire buttons.
            if (_hireWaiterButton != null)
                _hireWaiterButton.onClick.AddListener(() => HireStaff(StaffType.Waiter));
            if (_hireChefButton != null)
                _hireChefButton.onClick.AddListener(() => HireStaff(StaffType.Chef));

            // Detail actions.
            if (_upgradeSpeedButton != null)
                _upgradeSpeedButton.onClick.AddListener(OnUpgradeSpeedClicked);
            if (_upgradeSkillButton != null)
                _upgradeSkillButton.onClick.AddListener(OnUpgradeSkillClicked);
            if (_assignSpecializationButton != null)
                _assignSpecializationButton.onClick.AddListener(OnAssignSpecClicked);
            if (_fireButton != null)
                _fireButton.onClick.AddListener(OnFireClicked);
            if (_detailCloseButton != null)
                _detailCloseButton.onClick.AddListener(HideDetailPanel);

            // Spec picker.
            if (_specPickerCloseButton != null)
                _specPickerCloseButton.onClick.AddListener(HideSpecPicker);

            // Fire confirm.
            if (_fireConfirmYesButton != null)
                _fireConfirmYesButton.onClick.AddListener(OnFireConfirmYes);
            if (_fireConfirmNoButton != null)
                _fireConfirmNoButton.onClick.AddListener(HideFireConfirm);
        }

        // ------------------------------------------------------------------ //
        //  Staff card population
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns or creates a staff card at the specified pool index.
        /// </summary>
        private GameObject GetOrCreateCard(int index)
        {
            if (index < _cardPool.Count)
                return _cardPool[index];

            if (_staffCardPrefab == null || _staffListContent == null)
                return new GameObject("EmptyCard");

            GameObject card = Instantiate(_staffCardPrefab, _staffListContent);
            _cardPool.Add(card);
            return card;
        }

        /// <summary>
        /// Fills a staff card's child UI elements with data from a
        /// <see cref="StaffData"/> instance.
        /// </summary>
        private void PopulateStaffCard(GameObject card, StaffData staff)
        {
            // Name.
            Text nameText = card.transform.Find("NameText")?.GetComponent<Text>();
            if (nameText != null)
                nameText.text = staff.staffName;

            // Role icon.
            Image roleIcon = card.transform.Find("RoleIcon")?.GetComponent<Image>();
            if (roleIcon != null)
            {
                roleIcon.sprite = staff.type == StaffType.Waiter
                    ? _waiterIconSprite
                    : _chefIconSprite;
            }

            // Level.
            Text levelText = card.transform.Find("LevelText")?.GetComponent<Text>();
            if (levelText != null)
                levelText.text = $"Nv. {staff.level}";

            // Speed bar.
            Slider speedBar = card.transform.Find("SpeedBar")?.GetComponent<Slider>();
            if (speedBar != null)
                speedBar.value = staff.speed;

            // Skill bar.
            Slider skillBar = card.transform.Find("SkillBar")?.GetComponent<Slider>();
            if (skillBar != null)
                skillBar.value = staff.skill;

            // Specialization badge.
            Text specText = card.transform.Find("SpecBadge")?.GetComponent<Text>();
            if (specText != null)
            {
                if (staff.type == StaffType.Chef && staff.hasSpecialization)
                    specText.text = GetSpecializationDisplayName(staff.specialization);
                else
                    specText.text = string.Empty;
            }

            // Click handler.
            Button cardButton = card.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.onClick.RemoveAllListeners();
                StaffData captured = staff;
                cardButton.onClick.AddListener(() => SelectStaff(captured));
            }
        }

        // ------------------------------------------------------------------ //
        //  Detail panel
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens the detail panel with information about the selected staff.
        /// </summary>
        private void ShowDetailPanel(StaffData staff)
        {
            if (_detailPanel == null)
                return;

            _detailPanel.SetActive(true);

            if (_detailName != null)
                _detailName.text = staff.staffName;

            if (_detailRole != null)
            {
                string roleLabel = staff.type == StaffType.Waiter ? "Mesero(a)" : "Chef";
                _detailRole.text = roleLabel;
            }

            if (_detailLevel != null)
                _detailLevel.text = $"Nivel {staff.level}";

            if (_detailRoleIcon != null)
            {
                _detailRoleIcon.sprite = staff.type == StaffType.Waiter
                    ? _waiterIconSprite
                    : _chefIconSprite;
            }

            if (_detailSpeedBar != null)
                _detailSpeedBar.value = staff.speed;

            if (_detailSkillBar != null)
                _detailSkillBar.value = staff.skill;

            if (_detailSpecialization != null)
            {
                if (staff.type == StaffType.Chef && staff.hasSpecialization)
                    _detailSpecialization.text = $"Especialidad: {GetSpecializationDisplayName(staff.specialization)}";
                else if (staff.type == StaffType.Chef)
                    _detailSpecialization.text = "Sin especialidad";
                else
                    _detailSpecialization.text = string.Empty;
            }

            // Upgrade costs.
            int speedCost = CalculateUpgradeCost(staff, StatType.Speed);
            int skillCost = CalculateUpgradeCost(staff, StatType.Skill);

            if (_upgradeSpeedCostText != null)
                _upgradeSpeedCostText.text = staff.speed >= 1f ? "MAX" : $"${speedCost}";
            if (_upgradeSkillCostText != null)
                _upgradeSkillCostText.text = staff.skill >= 1f ? "MAX" : $"${skillCost}";

            if (_upgradeSpeedButton != null)
                _upgradeSpeedButton.interactable = staff.speed < 1f && CanAffordPesos(speedCost);
            if (_upgradeSkillButton != null)
                _upgradeSkillButton.interactable = staff.skill < 1f && CanAffordPesos(skillCost);

            // Specialization button is only visible for chefs.
            if (_assignSpecializationButton != null)
                _assignSpecializationButton.gameObject.SetActive(staff.type == StaffType.Chef);
        }

        /// <summary>
        /// Hides the staff detail panel.
        /// </summary>
        private void HideDetailPanel()
        {
            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ //
        //  Hire button state
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Updates the hire buttons' interactability and cost labels based on
        /// current funds and capacity.
        /// </summary>
        private void UpdateHireButtons()
        {
            int capacity = GetStaffCapacity();
            bool atCapacity = _hiredStaff.Count >= capacity;

            int waiterCost = CalculateHireCost(StaffType.Waiter);
            int chefCost = CalculateHireCost(StaffType.Chef);

            if (_hireWaiterButton != null)
                _hireWaiterButton.interactable = !atCapacity && CanAffordPesos(waiterCost);
            if (_hireChefButton != null)
                _hireChefButton.interactable = !atCapacity && CanAffordPesos(chefCost);

            if (_hireWaiterCostText != null)
                _hireWaiterCostText.text = atCapacity ? "Lleno" : $"${waiterCost}";
            if (_hireChefCostText != null)
                _hireChefCostText.text = atCapacity ? "Lleno" : $"${chefCost}";
        }

        // ------------------------------------------------------------------ //
        //  Specialization picker
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens the specialization picker popup for the selected chef.
        /// </summary>
        private void ShowSpecPicker()
        {
            if (_specPickerPopup == null)
                return;

            _specPickerPopup.SetActive(true);

            // Return pooled options.
            foreach (GameObject opt in _specOptionPool)
            {
                if (opt != null) opt.SetActive(false);
            }

            // Build an option for each recipe category.
            RecipeCategory[] categories = (RecipeCategory[])Enum.GetValues(typeof(RecipeCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                GameObject option = GetOrCreateSpecOption(i);
                option.SetActive(true);

                RecipeCategory cat = categories[i];
                Text label = option.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = GetSpecializationDisplayName(cat);

                Button btn = option.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    RecipeCategory captured = cat;
                    btn.onClick.AddListener(() => OnSpecOptionSelected(captured));
                }
            }
        }

        /// <summary>
        /// Hides the specialization picker popup.
        /// </summary>
        private void HideSpecPicker()
        {
            if (_specPickerPopup != null)
                _specPickerPopup.SetActive(false);
        }

        /// <summary>
        /// Returns or creates a specialization option at the given pool index.
        /// </summary>
        private GameObject GetOrCreateSpecOption(int index)
        {
            if (index < _specOptionPool.Count)
                return _specOptionPool[index];

            if (_specOptionPrefab == null || _specPickerContent == null)
                return new GameObject("EmptyOption");

            GameObject option = Instantiate(_specOptionPrefab, _specPickerContent);
            _specOptionPool.Add(option);
            return option;
        }

        /// <summary>
        /// Handler when the player selects a specialization from the picker.
        /// </summary>
        private void OnSpecOptionSelected(RecipeCategory category)
        {
            if (_selectedStaff == null || _selectedStaff.type != StaffType.Chef)
                return;

            _selectedStaff.specialization = category;
            _selectedStaff.hasSpecialization = true;

            OnSpecializationChanged?.Invoke(_selectedStaff);
            Debug.Log($"[StaffPanel] {_selectedStaff.staffName} specialization set to {category}.");

            HideSpecPicker();
            ShowDetailPanel(_selectedStaff);
            PopulateStaffList();
        }

        // ------------------------------------------------------------------ //
        //  Fire confirmation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Shows the fire confirmation dialog.
        /// </summary>
        private void ShowFireConfirm()
        {
            if (_fireConfirmDialog == null)
            {
                // No dialog; fire immediately.
                if (_selectedStaff != null)
                    FireStaff(_selectedStaff);
                return;
            }

            _fireConfirmDialog.SetActive(true);
            if (_fireConfirmText != null && _selectedStaff != null)
                _fireConfirmText.text = $"Despedir a {_selectedStaff.staffName}?";
        }

        /// <summary>
        /// Hides the fire confirmation dialog.
        /// </summary>
        private void HideFireConfirm()
        {
            if (_fireConfirmDialog != null)
                _fireConfirmDialog.SetActive(false);
        }

        /// <summary>
        /// Confirmation "yes" handler for firing.
        /// </summary>
        private void OnFireConfirmYes()
        {
            HideFireConfirm();
            if (_selectedStaff != null)
                FireStaff(_selectedStaff);
        }

        // ------------------------------------------------------------------ //
        //  Button handlers
        // ------------------------------------------------------------------ //

        private void OnUpgradeSpeedClicked()
        {
            if (_selectedStaff != null)
                UpgradeStaff(_selectedStaff, StatType.Speed);
        }

        private void OnUpgradeSkillClicked()
        {
            if (_selectedStaff != null)
                UpgradeStaff(_selectedStaff, StatType.Skill);
        }

        private void OnAssignSpecClicked()
        {
            ShowSpecPicker();
        }

        private void OnFireClicked()
        {
            ShowFireConfirm();
        }

        // ------------------------------------------------------------------ //
        //  Staff generation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Creates a new <see cref="StaffData"/> with a random Colombian name
        /// and baseline stats.
        /// </summary>
        /// <param name="type">Waiter or Chef.</param>
        /// <param name="hireCost">The cost that was charged to hire.</param>
        /// <returns>A freshly generated staff data instance.</returns>
        private StaffData GenerateStaff(StaffType type, int hireCost)
        {
            string fullName = GenerateColombianName();
            float baseSpeed = UnityEngine.Random.Range(0.2f, 0.4f);
            float baseSkill = UnityEngine.Random.Range(0.2f, 0.4f);

            StaffData staff = new StaffData
            {
                staffId = $"staff_{_nextStaffId++}",
                staffName = fullName,
                type = type,
                level = 1,
                speed = baseSpeed,
                skill = baseSkill,
                hasSpecialization = false,
                hireCost = hireCost
            };

            staff.level = CalculateStaffLevel(staff);
            return staff;
        }

        /// <summary>
        /// Generates a random Colombian full name by combining a first name
        /// and last name from the name pools.
        /// </summary>
        private static string GenerateColombianName()
        {
            bool isMale = UnityEngine.Random.value > 0.5f;
            string[] firstNames = isMale ? MaleNames : FemaleNames;
            string firstName = firstNames[UnityEngine.Random.Range(0, firstNames.Length)];
            string lastName = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];
            return $"{firstName} {lastName}";
        }

        // ------------------------------------------------------------------ //
        //  Cost calculations
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Calculates the cost to hire the next staff member of a given type.
        /// The cost scales with the number of existing staff of that type.
        /// </summary>
        private int CalculateHireCost(StaffType type)
        {
            int baseCost = type == StaffType.Waiter ? _baseWaiterHireCost : _baseChefHireCost;
            int existingCount = CountStaffOfType(type);
            return Mathf.RoundToInt(baseCost * Mathf.Pow(_hireCostScaling, existingCount));
        }

        /// <summary>
        /// Calculates the cost to upgrade a specific stat on a staff member.
        /// The cost scales with the current stat value.
        /// </summary>
        private int CalculateUpgradeCost(StaffData staff, StatType stat)
        {
            float currentValue = stat == StatType.Speed ? staff.speed : staff.skill;
            // Number of upgrades is approximated from the stat value.
            int upgradeCount = Mathf.RoundToInt(currentValue / _upgradeStatIncrement);
            return Mathf.RoundToInt(_baseUpgradeCost * Mathf.Pow(_upgradeCostScaling, upgradeCount));
        }

        /// <summary>
        /// Calculates a staff member's effective level from their combined stats.
        /// </summary>
        private static int CalculateStaffLevel(StaffData staff)
        {
            float combinedStat = (staff.speed + staff.skill) * 0.5f;
            return Mathf.Clamp(Mathf.FloorToInt(combinedStat * 10f) + 1, 1, 10);
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the maximum number of staff the player can have,
        /// based on the restaurant level.
        /// </summary>
        private int GetStaffCapacity()
        {
            int level = 1;
            if (GameManager.Instance != null && GameManager.Instance.Restaurant != null)
                level = GameManager.Instance.Restaurant.Level;

            int capacity = _baseStaffCapacity + (_slotsPerLevel * (level - 1));
            return Mathf.Clamp(capacity, _baseStaffCapacity, _maxStaffCapacity);
        }

        /// <summary>
        /// Counts the number of currently hired staff of a specific type.
        /// </summary>
        private int CountStaffOfType(StaffType type)
        {
            int count = 0;
            foreach (StaffData s in _hiredStaff)
            {
                if (s.type == type)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Returns <c>true</c> if the player has at least the specified
        /// number of pesos.
        /// </summary>
        private bool CanAffordPesos(int amount)
        {
            if (GameManager.Instance == null || GameManager.Instance.Economy == null)
                return false;
            return GameManager.Instance.Economy.Pesos >= amount;
        }

        /// <summary>
        /// Returns a user-friendly display name for a recipe-category
        /// specialization in Spanish.
        /// </summary>
        private static string GetSpecializationDisplayName(RecipeCategory category)
        {
            switch (category)
            {
                case RecipeCategory.Entrada:         return "Entradas";
                case RecipeCategory.PlatoFuerte:     return "Platos Fuertes";
                case RecipeCategory.Sopa:            return "Sopas";
                case RecipeCategory.Bebida:          return "Bebidas";
                case RecipeCategory.Postre:          return "Postres";
                case RecipeCategory.Acompanamiento:  return "Acompanamientos";
                default:                             return category.ToString();
            }
        }
    }
}
