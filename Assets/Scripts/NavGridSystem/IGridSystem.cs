using Unity.Collections;
using UnityEngine;

namespace NavGridSystem
{
    public interface IGridSystem
    {
        NativeArray<Cell> GetGrid();
        Cell GetRandomCell();
        Cell GetCellWithWorldPosition(Vector3 worldPosition);
        int GetGridSizeX();
        int GetGridSize();
    }
}