using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace NavigationGraph.Graph
{
    internal sealed class WorldNavigationGraph : NavigationGraph
    {
        public WorldNavigationGraph(float cellSize, float maxDistance, Vector2Int gridSize, 
                LayerMask notWalkableMask, Transform transform, LayerMask walkableMask) : 
                base(cellSize, maxDistance, gridSize, notWalkableMask, transform, walkableMask)
        {
            GraphType = NavigationGraphSystem.NavigationGraphType.Grid3D;
        }

        protected override void CreateGrid()
        {
            if (grid.IsCreated)
            {
                grid.Dispose();
            }

            List<Cell> tempCells = new List<Cell>();

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int z = 0; z < gridSize.y; z++)
                {
                    Vector3 origin = transform.position
                                     + Vector3.right * ((x + 0.5f) * cellDiameter)
                                     + Vector3.forward * ((z + 0.5f) * cellDiameter)
                                     + Vector3.up * maxDistance;

                    List<RaycastHit> hits = RaycastContinuous(origin, walkableMask | notWalkableMask);

                    foreach (var hit in hits)
                    {
                        bool isWalkable = IsCellWalkable(hit.point, 2f);
                        if (!isWalkable) continue;

                        var cell = new Cell
                        {
                            position = hit.point,
                            gridX = x,
                            gridZ = z,
                            height = hit.point.y, // TODO: Change this.
                            gridIndex = tempCells.Count,
                            isWalkable = isWalkable
                        };

                        tempCells.Add(cell);
                    }
                }
            }

            grid = new NativeArray<Cell>(tempCells.Count, Allocator.Persistent);
            for (int i = 0; i < tempCells.Count; i++)
                grid[i] = tempCells[i];
        }
        
        private List<RaycastHit> RaycastContinuous(Vector3 from, LayerMask mask)
        {
            List<RaycastHit> hits = new List<RaycastHit>();
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, maxDistance * 2, mask))
                return hits;

            hits.Add(hit);
            float minDist = cellSize * 0.5f;

            for (int i = 0; i < 10; i++)
            {
                Vector3 nextOrigin = hit.point + Vector3.down * minDist;
                if (!Physics.Raycast(nextOrigin, Vector3.down, out hit, maxDistance * 2, mask))
                    break;

                // Replace this because it's generate GC allocations. (Use ZLinq?)
                if (hits.Any(h => Mathf.Abs(h.point.y - hit.point.y) < minDist))
                    continue;

                hits.Add(hit);
            }
            return hits;
        }
        
        
        public Cell GetClosestCell(Vector3 worldPos)
        {
            var (x, z) = GetCellsMap(worldPos);

            Cell? closest = null;
            float closestDist = float.MaxValue;

            foreach (var cell in grid)
            {
                if (cell.gridX != x || cell.gridZ != z) continue;

                float dist = Mathf.Abs(cell.position.y - worldPos.y);
                if (dist < closestDist)
                {
                    closest = cell;
                    closestDist = dist;
                }
            }

            return closest ?? default;
        }
    }
}