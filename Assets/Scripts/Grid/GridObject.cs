using System.Collections.Generic;
using UnityEngine;

namespace SaborColombiano.Grid
{
    /// <summary>
    /// Broad classification for objects placed on the restaurant grid.
    /// </summary>
    public enum GridObjectType
    {
        Furniture,
        Equipment,
        Decoration,
        Wall,
        Floor
    }

    /// <summary>
    /// Fine-grained category describing the specific role of a grid object
    /// within the Colombian restaurant theme.
    /// </summary>
    public enum GridObjectCategory
    {
        Table,
        Chair,
        Stove,
        Oven,
        Counter,
        Fridge,
        WashStation,
        CashRegister,
        Decoration
    }

    /// <summary>
    /// Base MonoBehaviour for any object that can be placed on the isometric grid.
    /// Tracks its grid footprint, interaction points, and visual representation.
    /// Subclass or configure in the Inspector to create tables, stoves, chairs, etc.
    /// </summary>
    public class GridObject : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Inspector fields
        // ---------------------------------------------------------------

        /// <summary>Display name shown in the UI (e.g. "Mesa Redonda", "Estufa Industrial").</summary>
        [Header("Identity")]
        [SerializeField] private string objectName = "Grid Object";

        /// <summary>High-level type used for placement rules and layer sorting.</summary>
        [SerializeField] private GridObjectType objectType = GridObjectType.Furniture;

        /// <summary>Specific restaurant category used for gameplay logic.</summary>
        [SerializeField] private GridObjectCategory objectCategory = GridObjectCategory.Table;

        /// <summary>
        /// Size of this object on the grid in cells (x = width, y = height).
        /// A standard chair is 1x1; a kitchen counter might be 2x1.
        /// </summary>
        [Header("Grid Footprint")]
        [SerializeField] private Vector2Int gridSize = Vector2Int.one;

        /// <summary>
        /// Local offsets (relative to <see cref="gridPosition"/>) where characters
        /// stand when interacting with this object. For a 1x1 chair the list typically
        /// contains a single entry at (0, -1) meaning "stand one cell south".
        /// </summary>
        [SerializeField] private List<Vector2Int> interactionOffsets = new List<Vector2Int>();

        /// <summary>
        /// When true, pathfinding treats cells occupied by this object as passable.
        /// Typical for floor tiles and some small decorations; false for furniture.
        /// </summary>
        [Header("Behaviour")]
        [SerializeField] private bool isWalkable = false;

        /// <summary>Reference to the sprite renderer used for the visual representation.</summary>
        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        /// <summary>Bottom-left grid coordinate where this object is anchored.</summary>
        private Vector2Int gridPosition;

        /// <summary>
        /// Current rotation step.  Valid values are 0, 90, 180, 270.
        /// Rotation affects which cells are occupied and where interaction points sit.
        /// </summary>
        private int rotation;

        // ---------------------------------------------------------------
        //  Public properties
        // ---------------------------------------------------------------

        /// <summary>Display name of this object.</summary>
        public string ObjectName => objectName;

        /// <summary>High-level type (Furniture, Equipment, Decoration, Wall, Floor).</summary>
        public GridObjectType ObjectType => objectType;

        /// <summary>Restaurant-specific category (Table, Chair, Stove, etc.).</summary>
        public GridObjectCategory ObjectCategory => objectCategory;

        /// <summary>Bottom-left grid cell where this object is anchored.</summary>
        public Vector2Int GridPosition
        {
            get => gridPosition;
            set => gridPosition = value;
        }

        /// <summary>Cell footprint before rotation is applied.</summary>
        public Vector2Int GridSize => gridSize;

        /// <summary>Current rotation in degrees (0, 90, 180, 270).</summary>
        public int Rotation
        {
            get => rotation;
            set => rotation = NormalizeRotation(value);
        }

        /// <summary>Whether pathfinding can pass through cells occupied by this object.</summary>
        public bool IsWalkable => isWalkable;

        /// <summary>
        /// The sprite renderer used for visuals. May be null if the object uses a
        /// 3D mesh instead.
        /// </summary>
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        // ---------------------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------------------

        /// <summary>
        /// Caches the <see cref="SpriteRenderer"/> reference when not assigned in
        /// the Inspector.
        /// </summary>
        protected virtual void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        // ---------------------------------------------------------------
        //  Grid occupancy
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns every grid cell this object covers, accounting for the current
        /// <see cref="Rotation"/>.  The anchor cell (<see cref="GridPosition"/>)
        /// is always included.
        /// </summary>
        /// <returns>List of absolute grid coordinates this object occupies.</returns>
        public List<Vector2Int> GetOccupiedCells()
        {
            Vector2Int effectiveSize = GetRotatedSize();
            List<Vector2Int> cells = new List<Vector2Int>(effectiveSize.x * effectiveSize.y);

            for (int x = 0; x < effectiveSize.x; x++)
            {
                for (int y = 0; y < effectiveSize.y; y++)
                {
                    cells.Add(gridPosition + new Vector2Int(x, y));
                }
            }

            return cells;
        }

        /// <summary>
        /// Returns the effective grid size after applying the current rotation.
        /// A 2x1 object rotated by 90 degrees becomes 1x2.
        /// </summary>
        /// <returns>Rotated size in grid cells.</returns>
        public Vector2Int GetRotatedSize()
        {
            int normalizedRotation = NormalizeRotation(rotation);
            bool isSwapped = normalizedRotation == 90 || normalizedRotation == 270;
            return isSwapped ? new Vector2Int(gridSize.y, gridSize.x) : gridSize;
        }

        // ---------------------------------------------------------------
        //  Interaction
        // ---------------------------------------------------------------

        /// <summary>
        /// Checks whether this object supports character interaction based on its
        /// <see cref="ObjectCategory"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if a character can interact with this object
        /// (sit in a chair, place food on a table, cook on a stove, etc.).
        /// </returns>
        public bool CanInteract()
        {
            switch (objectCategory)
            {
                case GridObjectCategory.Chair:
                case GridObjectCategory.Table:
                case GridObjectCategory.Stove:
                case GridObjectCategory.Oven:
                case GridObjectCategory.Counter:
                case GridObjectCategory.Fridge:
                case GridObjectCategory.WashStation:
                case GridObjectCategory.CashRegister:
                    return true;
                case GridObjectCategory.Decoration:
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns world-relative grid positions where a character should stand in
        /// order to interact with this object. Each offset is rotated to match the
        /// object's current <see cref="Rotation"/> and then translated to an
        /// absolute grid coordinate.
        /// </summary>
        /// <returns>
        /// List of absolute grid positions suitable for character pathfinding targets.
        /// Returns an empty list when no offsets have been configured or the object
        /// is non-interactive.
        /// </returns>
        public List<Vector2Int> GetInteractionPoints()
        {
            List<Vector2Int> points = new List<Vector2Int>(interactionOffsets.Count);

            foreach (Vector2Int offset in interactionOffsets)
            {
                Vector2Int rotatedOffset = RotateOffset(offset);
                points.Add(gridPosition + rotatedOffset);
            }

            return points;
        }

        // ---------------------------------------------------------------
        //  Visual helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Sets the colour tint of the <see cref="SpriteRenderer"/>. Used by the
        /// placement system to show ghost previews (semi-transparent green / red).
        /// </summary>
        /// <param name="color">Desired tint colour including alpha.</param>
        public void SetColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        /// <summary>
        /// Applies the current <see cref="Rotation"/> to the transform so the
        /// visual matches the logical orientation.  Uses Z-axis rotation which is
        /// the standard for 2D / isometric sprites.
        /// </summary>
        public void ApplyRotationVisual()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, -rotation);
        }

        // ---------------------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Rotates a 2D offset around the origin by the current <see cref="Rotation"/>.
        /// </summary>
        private Vector2Int RotateOffset(Vector2Int offset)
        {
            int normalizedRotation = NormalizeRotation(rotation);

            switch (normalizedRotation)
            {
                case 90:
                    return new Vector2Int(offset.y, -offset.x);
                case 180:
                    return new Vector2Int(-offset.x, -offset.y);
                case 270:
                    return new Vector2Int(-offset.y, offset.x);
                default: // 0
                    return offset;
            }
        }

        /// <summary>
        /// Clamps a rotation value to one of the four cardinal steps: 0, 90, 180, 270.
        /// </summary>
        private static int NormalizeRotation(int degrees)
        {
            int mod = ((degrees % 360) + 360) % 360;

            // Snap to nearest 90-degree step.
            int snapped = Mathf.RoundToInt(mod / 90f) * 90;
            return snapped % 360;
        }

        // ---------------------------------------------------------------
        //  Gizmos (editor only)
        // ---------------------------------------------------------------

#if UNITY_EDITOR
        /// <summary>
        /// Draws a wireframe box in the Scene view showing the object footprint and
        /// small spheres at each interaction point.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector2Int size = GetRotatedSize();

            // Footprint outline.
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
            Vector3 center = transform.position + new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(size.x, size.y, 0.1f));

            // Interaction points.
            Gizmos.color = Color.yellow;
            foreach (Vector2Int offset in interactionOffsets)
            {
                Vector2Int rotated = RotateOffset(offset);
                Vector3 worldPoint = transform.position + new Vector3(rotated.x + 0.5f, rotated.y + 0.5f, 0f);
                Gizmos.DrawSphere(worldPoint, 0.15f);
            }
        }
#endif
    }
}
