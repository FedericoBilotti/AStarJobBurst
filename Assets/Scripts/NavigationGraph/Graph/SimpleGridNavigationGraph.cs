using Unity.Collections;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace NavigationGraph.Graph
{
    internal sealed class SimpleGridNavigationGraph : NavigationGraph
    {
        public SimpleGridNavigationGraph(float cellSize, float maxDistance, Vector2Int gridSize, 
                LayerMask notWalkableMask, Transform transform, LayerMask walkableMask) : base(cellSize, maxDistance, gridSize, 
                notWalkableMask, transform, walkableMask)
        {
            GraphType = NavigationGraphSystem.NavigationGraphType.Grid2D;
        }

        protected override void CreateGrid()
        {
            if (grid.IsCreated)
            {
                grid.Dispose();
            }

            grid = new NativeArray<Cell>(gridSize.x * gridSize.y, Allocator.Persistent);

            for (int i = 0; i < grid.Length; i++)
            {
                int x = i % gridSize.x;
                int y = i / gridSize.x;

                Vector3 cellPosition = GetCellPositionInWorldMap(x, y);
                bool isWalkable = IsCellWalkable(cellPosition, 1);

                grid[i] = new Cell
                {
                    position = cellPosition,
                    gridIndex = i,
                    gridX = x,
                    gridZ = y,
                    isWalkable = isWalkable,
                };
            }
        }
    }
}