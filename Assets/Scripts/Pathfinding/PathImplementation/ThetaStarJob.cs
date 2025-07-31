using NavigationGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Pathfinding.PathImplementation
{
    [BurstCompile]
    internal struct ThetaStarJob : IJob
    {
        [ReadOnly] public NativeArray<Cell> grid;
        [ReadOnly] public int endIndex;
        [ReadOnly] public int gridSizeX;

        public NativeList<Cell> finalPath;
        public NativeList<Cell> simplified;

        public void Execute()
        {
            SimplifyPath();
            ReversePath();
        }

        private void ReversePath()
        {
            finalPath.Clear();
            finalPath.AddRange(simplified.AsArray());
        }

        private void SimplifyPath()
        {
            // Avoid simplifying the path if it has less than 2 cells
            if (finalPath.Length <= 2) 
                return;

            int j = 0;
            simplified.Add(finalPath[0]);

            for (int i = 1; i < finalPath.Length; i++)
            {
                if (HasLineOfSight(finalPath[j], finalPath[i])) continue;

                simplified.Add(finalPath[i - 1]);
                j = i - 1;
            }
        }

        // Bresenham algorithm
        private bool HasLineOfSight(Cell startCell, Cell endCell)
        {
            int startX = startCell.gridX;
            int startY = startCell.gridZ;
            int endX = endCell.gridX;
            int endY = endCell.gridZ;

            int deltaX = Mathf.Abs(endX - startX);
            int deltaY = Mathf.Abs(endY - startY);

            int stepX = startX < endX ? 1 : -1;
            int stepY = startY < endY ? 1 : -1;

            int error = deltaX - deltaY;

            while (startX != endX || startY != endY)
            {
                int index = GetIndex(startX, startY);

                if (!grid[index].isWalkable) return false;

                int doubleError = 2 * error;

                if (doubleError > -deltaY)
                {
                    error -= deltaY;
                    startX += stepX;
                }

                if (doubleError < deltaX)
                {
                    error += deltaX;
                    startY += stepY;
                }
            }

            return true;
        }

        private int GetIndex(int x, int y)
        {
            return x + y * gridSizeX;
        }
    }
}