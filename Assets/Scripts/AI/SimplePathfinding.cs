using System.Collections.Generic;
using UnityEngine;

namespace SaborColombiano.AI
{
    /// <summary>
    /// Static utility class that implements A* pathfinding on a 2D grid.
    /// Designed to work with <c>GridManager</c>'s occupancy data using
    /// 4-directional movement (no diagonals) for an isometric look.
    /// </summary>
    public static class SimplePathfinding
    {
        // ------------------------------------------------------------------ //
        //  Constants
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Maximum number of iterations the search loop will execute before
        /// bailing out. Prevents the game from freezing on unreachable goals
        /// or very large grids.
        /// </summary>
        private const int MaxIterations = 1000;

        /// <summary>
        /// The four cardinal directions used for neighbour expansion.
        /// Diagonal movement is intentionally excluded to match the
        /// isometric grid aesthetic.
        /// </summary>
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,    // ( 0,  1)
            Vector2Int.down,  // ( 0, -1)
            Vector2Int.left,  // (-1,  0)
            Vector2Int.right  // ( 1,  0)
        };

        // ------------------------------------------------------------------ //
        //  Internal node representation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Lightweight node used during the A* search.
        /// Stores movement costs and a parent reference for path reconstruction.
        /// </summary>
        private class Node
        {
            /// <summary>Grid position of this node.</summary>
            public readonly Vector2Int Position;

            /// <summary>Cost from the start node to this node.</summary>
            public int GCost;

            /// <summary>Heuristic estimate from this node to the goal.</summary>
            public int HCost;

            /// <summary>Total estimated cost (<c>GCost + HCost</c>).</summary>
            public int FCost => GCost + HCost;

            /// <summary>
            /// The node from which we reached this one. <c>null</c> for the
            /// start node.
            /// </summary>
            public Node Parent;

            /// <summary>Creates a new pathfinding node at the given position.</summary>
            /// <param name="position">Grid coordinates of this node.</param>
            public Node(Vector2Int position)
            {
                Position = position;
                GCost = int.MaxValue;
                HCost = 0;
                Parent = null;
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Finds the shortest path from <paramref name="start"/> to
        /// <paramref name="end"/> on the supplied walkable grid using A*.
        /// <para>
        /// The returned list contains grid positions ordered from
        /// <paramref name="start"/> to <paramref name="end"/>, inclusive.
        /// If no path exists or the search exceeds <see cref="MaxIterations"/>,
        /// an empty list is returned.
        /// </para>
        /// </summary>
        /// <param name="start">Grid coordinates of the starting cell.</param>
        /// <param name="end">Grid coordinates of the target cell.</param>
        /// <param name="walkableGrid">
        /// A 2D boolean array where <c>true</c> marks a cell the agent can
        /// walk through. Dimensions define the grid bounds (width = GetLength(0),
        /// height = GetLength(1)).
        /// </param>
        /// <returns>
        /// An ordered list of <see cref="Vector2Int"/> positions from start to
        /// end, or an empty list when no valid path is found.
        /// </returns>
        public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, bool[,] walkableGrid)
        {
            if (walkableGrid == null)
            {
                Debug.LogWarning("[SimplePathfinding] walkableGrid is null. Returning empty path.");
                return new List<Vector2Int>();
            }

            int gridWidth = walkableGrid.GetLength(0);
            int gridHeight = walkableGrid.GetLength(1);

            // Quick validation.
            if (!IsInBounds(start, gridWidth, gridHeight) ||
                !IsInBounds(end, gridWidth, gridHeight))
            {
                Debug.LogWarning(
                    $"[SimplePathfinding] Start {start} or end {end} is out of bounds " +
                    $"({gridWidth}x{gridHeight}). Returning empty path.");
                return new List<Vector2Int>();
            }

            if (!walkableGrid[start.x, start.y])
            {
                Debug.LogWarning(
                    $"[SimplePathfinding] Start cell {start} is not walkable. Returning empty path.");
                return new List<Vector2Int>();
            }

            if (!walkableGrid[end.x, end.y])
            {
                Debug.LogWarning(
                    $"[SimplePathfinding] End cell {end} is not walkable. Returning empty path.");
                return new List<Vector2Int>();
            }

            // Trivial case.
            if (start == end)
            {
                return new List<Vector2Int> { start };
            }

            // -------------------------------------------------------------- //
            //  A* search
            // -------------------------------------------------------------- //

            // Node look-up by grid position to avoid duplicates.
            Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>();

            Node startNode = GetOrCreateNode(start, allNodes);
            startNode.GCost = 0;
            startNode.HCost = ManhattanDistance(start, end);

            // Open set: nodes to evaluate. Using a list and sorting is simple;
            // for larger grids a priority queue would be more efficient, but
            // restaurant maps are small enough that this is fine.
            List<Node> openSet = new List<Node> { startNode };

            // Closed set: positions already fully evaluated.
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

            int iterations = 0;

            while (openSet.Count > 0)
            {
                iterations++;
                if (iterations > MaxIterations)
                {
                    Debug.LogWarning(
                        $"[SimplePathfinding] Exceeded {MaxIterations} iterations " +
                        $"searching from {start} to {end}. Aborting.");
                    return new List<Vector2Int>();
                }

                // Pick the node with the lowest fCost (tie-break on hCost).
                Node current = GetLowestFCostNode(openSet);

                // Reached the goal -- reconstruct and return the path.
                if (current.Position == end)
                {
                    return ReconstructPath(current);
                }

                openSet.Remove(current);
                closedSet.Add(current.Position);

                // Expand neighbours.
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int neighbourPos = current.Position + Directions[i];

                    // Skip out-of-bounds, unwalkable, or already-evaluated cells.
                    if (!IsInBounds(neighbourPos, gridWidth, gridHeight))
                        continue;
                    if (!walkableGrid[neighbourPos.x, neighbourPos.y])
                        continue;
                    if (closedSet.Contains(neighbourPos))
                        continue;

                    Node neighbour = GetOrCreateNode(neighbourPos, allNodes);

                    // Cost to reach the neighbour through the current node.
                    int tentativeG = current.GCost + 1; // uniform cost per step

                    if (tentativeG < neighbour.GCost)
                    {
                        neighbour.GCost = tentativeG;
                        neighbour.HCost = ManhattanDistance(neighbourPos, end);
                        neighbour.Parent = current;

                        if (!openSet.Contains(neighbour))
                        {
                            openSet.Add(neighbour);
                        }
                    }
                }
            }

            // Open set exhausted without reaching the goal.
            Debug.LogWarning(
                $"[SimplePathfinding] No path found from {start} to {end}.");
            return new List<Vector2Int>();
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns <c>true</c> when <paramref name="pos"/> lies within the
        /// grid dimensions.
        /// </summary>
        private static bool IsInBounds(Vector2Int pos, int width, int height)
        {
            return pos.x >= 0 && pos.x < width &&
                   pos.y >= 0 && pos.y < height;
        }

        /// <summary>
        /// Manhattan distance heuristic -- the sum of absolute differences
        /// along each axis. Consistent and admissible for 4-directional grids.
        /// </summary>
        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// Retrieves an existing <see cref="Node"/> for the position or
        /// creates a new one and adds it to the dictionary.
        /// </summary>
        private static Node GetOrCreateNode(Vector2Int pos, Dictionary<Vector2Int, Node> nodes)
        {
            if (!nodes.TryGetValue(pos, out Node node))
            {
                node = new Node(pos);
                nodes[pos] = node;
            }
            return node;
        }

        /// <summary>
        /// Linearly scans the open set for the node with the lowest
        /// <c>fCost</c>. Ties are broken by preferring the lower <c>hCost</c>.
        /// </summary>
        private static Node GetLowestFCostNode(List<Node> openSet)
        {
            Node best = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                Node candidate = openSet[i];
                if (candidate.FCost < best.FCost ||
                    (candidate.FCost == best.FCost && candidate.HCost < best.HCost))
                {
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>
        /// Walks the <see cref="Node.Parent"/> chain from the goal back to
        /// the start, then reverses the list so it runs start-to-end.
        /// </summary>
        private static List<Vector2Int> ReconstructPath(Node endNode)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Node current = endNode;
            while (current != null)
            {
                path.Add(current.Position);
                current = current.Parent;
            }
            path.Reverse();
            return path;
        }
    }
}
