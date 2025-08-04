using System;
using Unity.Mathematics;
using UnityEngine;

namespace NavigationGraph
{
    public struct Cell : IEquatable<Cell>
    {
        public float3 position;
        public bool isWalkable;

        public int gridIndex;
        public int gridX;
        public int gridZ;
        public float height;

        public bool Equals(Cell other) => gridX == other.gridX && gridZ == other.gridZ;
        public override int GetHashCode() => (int)math.hash(new int3(gridX, gridZ, gridIndex));
    }

    public struct PathCellData : IHeapComparable<PathCellData>
    {
        public int cellIndex;
        public int cameFrom;
        public int gCost;
        public int hCost;
        public int FCost => gCost + hCost;

        public int HeapIndex { get; set; }

        public int CompareTo(PathCellData other)
        {
            int result = FCost.CompareTo(other.FCost);
            if (result == 0) result = hCost.CompareTo(other.hCost);
            return result;
        }
    }
}