using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaborColombiano.Grid
{
    /// <summary>
    /// Central manager for the isometric grid that underpins the restaurant floor.
    /// Owns the 2-D cell array, converts between world and grid coordinates using
    /// isometric projection, and provides placement / removal APIs consumed by
    /// <see cref="PlacementSystem"/> and other gameplay systems.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Inspector fields
        // ---------------------------------------------------------------

        /// <summary>Number of cells along the X (width) axis.</summary>
        [Header("Grid Dimensions")]
        [SerializeField] private int gridWidth = 20;

        /// <summary>Number of cells along the Y (height) axis.</summary>
        [SerializeField] private int gridHeight = 20;

        /// <summary>
        /// World-space size of a single grid cell before isometric projection.
        /// The isometric tile diamond will be <c>cellSize</c> wide and
        /// <c>cellSize / 2</c> tall.
        /// </summary>
        [Header("Isometric Settings")]
        [SerializeField] private float cellSize = 1f;

        /// <summary>World-space origin of the grid (bottom-left corner in grid space).</summary>
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        /// <summary>Colour used when drawing the grid overlay in the Scene view.</summary>
        [Header("Debug")]
        [SerializeField] private Color gizmoColor = new Color(1f, 1f, 1f, 0.3f);

        /// <summary>Colour used for cells that are occupied.</summary>
        [SerializeField] private Color occupiedGizmoColor = new Color(1f, 0.3f, 0.3f, 0.35f);

        // ---------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------

        /// <summary>
        /// Fired immediately after an object has been placed on the grid.
        /// Parameters: the placed <see cref="GridObject"/> and its anchor cell.
        /// </summary>
        public event Action<GridObject, Vector2Int> OnObjectPlaced;

        /// <summary>
        /// Fired immediately after an object has been removed from the grid.
        /// Parameters: the removed <see cref="GridObject"/> and its former anchor cell.
        /// </summary>
        public event Action<GridObject, Vector2Int> OnObjectRemoved;

        // ---------------------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------------------

        /// <summary>
        /// Backing 2-D array. Each element is either <c>null</c> (empty) or a
        /// reference to the <see cref="GridObject"/> occupying that cell.
        /// Multi-cell objects store their reference in every cell they cover.
        /// </summary>
        private GridObject[,] cells;

        // ---------------------------------------------------------------
        //  Public properties
        // ---------------------------------------------------------------

        /// <summary>Grid width in cells.</summary>
        public int Width => gridWidth;

        /// <summary>Grid height in cells.</summary>
        public int Height => gridHeight;

        /// <summary>World-space size of one grid cell.</summary>
        public float CellSize => cellSize;

        /// <summary>World-space origin of the grid.</summary>
        public Vector3 GridOrigin => gridOrigin;

        // ---------------------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------------------

        /// <summary>Allocates the cell array on awake.</summary>
        private void Awake()
        {
            InitializeGrid();
        }

        // ---------------------------------------------------------------
        //  Initialization
        // ---------------------------------------------------------------

        /// <summary>
        /// (Re-)creates the internal cell array. Safe to call at runtime if the
        /// grid needs to be rebuilt (e.g. when loading a save file).
        /// </summary>
        public void InitializeGrid()
        {
            cells = new GridObject[gridWidth, gridHeight];
        }

        // ---------------------------------------------------------------
        //  Coordinate conversion  (isometric <-> grid)
        // ---------------------------------------------------------------

        /// <summary>
        /// Converts a world-space position (typically from a mouse raycast) into the
        /// corresponding grid coordinate. The conversion undoes the isometric
        /// projection: screen X maps to <c>(gridX + gridY)</c> and screen Y maps
        /// to <c>(gridY - gridX)</c>.
        /// </summary>
        /// <param name="worldPosition">Position in Unity world space.</param>
        /// <returns>
        /// Integer grid coordinate. May be outside the valid range; call
        /// <see cref="IsWithinBounds"/> to verify.
        /// </returns>
        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            // Translate so that gridOrigin becomes the local origin.
            float localX = worldPosition.x - gridOrigin.x;
            float localY = worldPosition.y - gridOrigin.y;

            // Inverse isometric transformation.
            // Iso forward:  isoX = (gx - gy) * halfCell
            //                isoY = (gx + gy) * quarterCell
            // Inverse:       gx = (isoX / halfCell + isoY / quarterCell) / 2
            //                gy = (isoY / quarterCell - isoX / halfCell) / 2
            float halfCell = cellSize * 0.5f;
            float quarterCell = cellSize * 0.25f;

            float gxFloat = (localX / halfCell + localY / quarterCell) * 0.5f;
            float gyFloat = (localY / quarterCell - localX / halfCell) * 0.5f;

            return new Vector2Int(Mathf.FloorToInt(gxFloat), Mathf.FloorToInt(gyFloat));
        }

        /// <summary>
        /// Converts a grid coordinate to its world-space centre using standard
        /// isometric projection (2:1 diamond ratio).
        /// </summary>
        /// <param name="gridPos">Grid coordinate (column, row).</param>
        /// <returns>World-space position of the cell centre.</returns>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return GridToWorldInternal(gridPos.x, gridPos.y);
        }

        /// <summary>
        /// Converts fractional grid coordinates to world space. Useful for
        /// computing the visual centre of multi-cell objects.
        /// </summary>
        private Vector3 GridToWorldInternal(float gx, float gy)
        {
            float halfCell = cellSize * 0.5f;
            float quarterCell = cellSize * 0.25f;

            float isoX = (gx - gy) * halfCell;
            float isoY = (gx + gy) * quarterCell;

            return new Vector3(isoX + gridOrigin.x, isoY + gridOrigin.y, 0f);
        }

        /// <summary>
        /// Converts a cartesian (flat grid) position to isometric screen space.
        /// This is the raw projection without grid-origin offset -- handy for
        /// direction vectors.
        /// </summary>
        /// <param name="cartesian">Flat 2-D position.</param>
        /// <returns>Isometric screen-space vector.</returns>
        public Vector2 CartesianToIsometric(Vector2 cartesian)
        {
            float halfCell = cellSize * 0.5f;
            float quarterCell = cellSize * 0.25f;

            return new Vector2(
                (cartesian.x - cartesian.y) * halfCell,
                (cartesian.x + cartesian.y) * quarterCell
            );
        }

        /// <summary>
        /// Converts an isometric screen-space position back to flat cartesian
        /// coordinates.
        /// </summary>
        /// <param name="iso">Isometric position.</param>
        /// <returns>Flat 2-D cartesian coordinate.</returns>
        public Vector2 IsometricToCartesian(Vector2 iso)
        {
            float halfCell = cellSize * 0.5f;
            float quarterCell = cellSize * 0.25f;

            float gx = (iso.x / halfCell + iso.y / quarterCell) * 0.5f;
            float gy = (iso.y / quarterCell - iso.x / halfCell) * 0.5f;

            return new Vector2(gx, gy);
        }

        // ---------------------------------------------------------------
        //  Bounds checking
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns <c>true</c> if the given cell coordinate lies within the grid
        /// boundaries.
        /// </summary>
        public bool IsWithinBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < gridWidth &&
                   pos.y >= 0 && pos.y < gridHeight;
        }

        /// <summary>
        /// Checks whether a rectangular region starting at <paramref name="anchor"/>
        /// with the given <paramref name="size"/> is entirely inside the grid and
        /// every cell within that region is currently unoccupied.
        /// </summary>
        /// <param name="anchor">Bottom-left cell of the region.</param>
        /// <param name="size">Width and height in cells.</param>
        /// <returns><c>true</c> if the region can accept a new object.</returns>
        public bool IsValidPosition(Vector2Int anchor, Vector2Int size)
        {
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Vector2Int cell = anchor + new Vector2Int(x, y);

                    if (!IsWithinBounds(cell))
                    {
                        return false;
                    }

                    if (cells[cell.x, cell.y] != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // ---------------------------------------------------------------
        //  Placement / removal
        // ---------------------------------------------------------------

        /// <summary>
        /// Places a <see cref="GridObject"/> on the grid at the specified anchor
        /// position. All cells covered by the object's rotated size are marked as
        /// occupied. The object's <see cref="GridObject.GridPosition"/> is updated
        /// and its transform is snapped to the isometric world position.
        /// </summary>
        /// <param name="gridObject">Object to place.</param>
        /// <param name="anchor">Bottom-left grid cell for the object.</param>
        /// <returns>
        /// <c>true</c> if placement succeeded; <c>false</c> if any cell was
        /// out-of-bounds or already occupied.
        /// </returns>
        public bool PlaceObject(GridObject gridObject, Vector2Int anchor)
        {
            if (gridObject == null)
            {
                Debug.LogWarning("[GridManager] PlaceObject called with a null GridObject.");
                return false;
            }

            Vector2Int effectiveSize = gridObject.GetRotatedSize();

            if (!IsValidPosition(anchor, effectiveSize))
            {
                return false;
            }

            // Mark cells.
            for (int x = 0; x < effectiveSize.x; x++)
            {
                for (int y = 0; y < effectiveSize.y; y++)
                {
                    cells[anchor.x + x, anchor.y + y] = gridObject;
                }
            }

            // Update the object itself.
            gridObject.GridPosition = anchor;

            // Snap the object's transform to the centre of its footprint.
            float centreX = anchor.x + effectiveSize.x * 0.5f;
            float centreY = anchor.y + effectiveSize.y * 0.5f;
            gridObject.transform.position = GridToWorldInternal(centreX, centreY);

            OnObjectPlaced?.Invoke(gridObject, anchor);
            return true;
        }

        /// <summary>
        /// Removes whatever object occupies the cell at <paramref name="position"/>
        /// and clears all cells that object covered.
        /// </summary>
        /// <param name="position">Any cell occupied by the target object.</param>
        /// <returns>
        /// The <see cref="GridObject"/> that was removed, or <c>null</c> if the
        /// cell was already empty.
        /// </returns>
        public GridObject RemoveObject(Vector2Int position)
        {
            if (!IsWithinBounds(position))
            {
                return null;
            }

            GridObject target = cells[position.x, position.y];
            if (target == null)
            {
                return null;
            }

            // Clear all cells the object covers (based on its stored anchor + size).
            Vector2Int anchor = target.GridPosition;
            Vector2Int effectiveSize = target.GetRotatedSize();

            for (int x = 0; x < effectiveSize.x; x++)
            {
                for (int y = 0; y < effectiveSize.y; y++)
                {
                    int cx = anchor.x + x;
                    int cy = anchor.y + y;

                    if (IsWithinBounds(new Vector2Int(cx, cy)))
                    {
                        cells[cx, cy] = null;
                    }
                }
            }

            OnObjectRemoved?.Invoke(target, anchor);
            return target;
        }

        /// <summary>
        /// Returns the <see cref="GridObject"/> occupying the given cell, or
        /// <c>null</c> if the cell is empty or out of bounds.
        /// </summary>
        /// <param name="position">Grid coordinate to query.</param>
        /// <returns>Occupying object or <c>null</c>.</returns>
        public GridObject GetObjectAt(Vector2Int position)
        {
            if (!IsWithinBounds(position))
            {
                return null;
            }

            return cells[position.x, position.y];
        }

        // ---------------------------------------------------------------
        //  Gizmos (editor only)
        // ---------------------------------------------------------------

#if UNITY_EDITOR
        /// <summary>
        /// Draws the full isometric grid in the Scene view so designers can see
        /// cell boundaries and occupied cells at a glance.
        /// </summary>
        private void OnDrawGizmos()
        {
            int w = gridWidth;
            int h = gridHeight;

            // Draw cell outlines as iso diamonds.
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    bool occupied = cells != null &&
                                    x < cells.GetLength(0) &&
                                    y < cells.GetLength(1) &&
                                    cells[x, y] != null;

                    Gizmos.color = occupied ? occupiedGizmoColor : gizmoColor;

                    DrawIsoDiamond(x, y);
                }
            }
        }

        /// <summary>
        /// Draws a single isometric diamond (rhombus) for the cell at (gx, gy).
        /// </summary>
        private void DrawIsoDiamond(int gx, int gy)
        {
            // Four corners of the flat unit cell mapped through isometric projection.
            Vector3 bl = GridToWorldInternal(gx, gy);         // bottom-left  corner
            Vector3 br = GridToWorldInternal(gx + 1, gy);     // bottom-right corner
            Vector3 tr = GridToWorldInternal(gx + 1, gy + 1); // top-right    corner
            Vector3 tl = GridToWorldInternal(gx, gy + 1);     // top-left     corner

            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
#endif
    }
}
