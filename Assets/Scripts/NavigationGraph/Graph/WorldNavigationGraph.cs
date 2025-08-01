using UnityEngine;

namespace NavigationGraph.Graph
{
    public sealed class WorldNavigationGraph : NavigationGraph
    {
        public WorldNavigationGraph(float cellSize, float maxDistance, Vector2Int gridSize, 
                LayerMask notWalkableMask, Transform transform, LayerMask walkableMask) : 
                base(cellSize, maxDistance, gridSize, notWalkableMask, transform, walkableMask)
        {
            GraphType = NavigationGraphSystem.NavigationGraphType.Grid3D;
        }

        protected override void CreateGrid()
        {
            throw new System.NotImplementedException();
        }
    }
}