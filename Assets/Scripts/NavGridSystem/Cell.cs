using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace NavGridSystem
{
    public struct Cell : IEquatable<Cell>
    {
        public Vector3 position;

        public int gridIndex;
        public int x;
        public int y;
        public bool isWalkable;

        public bool Equals(Cell other) => x == other.x && y == other.y;
        public override int GetHashCode() => (int)math.hash(new int3(x, y, gridIndex));
    }

    public struct PathCellData : IHeapComparable<PathCellData>
    {
        public int cellIndex;
        public int cameFrom;
        public int gCost;
        public int hCost;
        public int FCost => gCost + hCost;
        
        public int HeapIndex {get; set; }
        
        public int CompareTo(PathCellData other)
        {  
            int result = FCost.CompareTo(other.FCost);
            if (result == 0) result = hCost.CompareTo(other.hCost);
            return result;
        }
    }
}