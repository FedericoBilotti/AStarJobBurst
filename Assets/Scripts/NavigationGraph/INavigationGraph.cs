using Unity.Collections;
using UnityEngine;

namespace NavigationGraph
{
    public interface INavigationGraph
    {
        NativeArray<Cell> GetGrid();
        Cell GetRandomCell();
        Cell GetCellWithWorldPosition(Vector3 worldPosition);
        public Vector3 GetNearestCellPosition(Vector3 worldPosition);
        bool IsInGrid(Vector3 worldPosition);
        int GetGridSizeX();
        int GetGridSize();
    }
}