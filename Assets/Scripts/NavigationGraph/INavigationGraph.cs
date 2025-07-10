using Unity.Collections;
using UnityEngine;

namespace NavigationGraph
{
    public interface INavigationGraph
    {
        NativeArray<Cell> GetGrid();
        Cell GetRandomCell();
        Cell GetCellWithWorldPosition(Vector3 worldPosition);
        int GetGridSizeX();
        int GetGridSize();
    }
}