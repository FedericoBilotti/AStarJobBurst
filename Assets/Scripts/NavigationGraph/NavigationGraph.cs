using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace NavigationGraph
{
    internal abstract class NavigationGraph : INavigationGraph
    {
        protected readonly LayerMask notWalkableMask;
        protected readonly LayerMask walkableMask;
        protected readonly float maxDistance;
        protected float cellSize;
        protected  float cellDiameter;
        protected Vector2Int gridSize;
        protected NativeArray<Cell> grid;

        protected readonly Transform transform;

        public NavigationGraphSystem.NavigationGraphType GraphType { get; protected set; }

        protected NavigationGraph(float cellSize, float maxDistance, Vector2Int gridSize, LayerMask notWalkableMask, Transform transform, LayerMask walkableMask)
        {
            this.cellSize = cellSize;
            this.gridSize = gridSize;

            this.maxDistance = maxDistance;
            this.walkableMask = walkableMask;
            this.notWalkableMask = notWalkableMask;

            this.transform = transform;
        }

        protected abstract void CreateGrid();

        public NativeArray<Cell> GetGrid() => grid;
        public Cell GetRandomCell() => grid[Random.Range(0, grid.Length)];
        public int GetGridSize() => gridSize.x * gridSize.y;
        public int GetGridSizeX() => gridSize.x;

        public virtual Cell GetCellWithWorldPosition(Vector3 worldPosition)
        {
            var (x, y) = GetCellsMap(worldPosition);

            return grid[x + y * gridSize.x];
        }

        public virtual bool IsInGrid(Vector3 worldPosition)
        {
            Vector3 gridPos = worldPosition - transform.position;

            int x = Mathf.FloorToInt((gridPos.x - cellSize) / cellDiameter);
            int y = Mathf.FloorToInt((gridPos.z - cellSize) / cellDiameter);

            if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y) return false;

            int gridIndex = x + y * gridSize.x;
            return grid[gridIndex].isWalkable;
        }

        public Vector3 GetNearestWalkableCellPosition(Vector3 worldPosition)
        {
            var (startX, startY) = GetCellsMap(worldPosition);

            var visited = new bool[grid.Length];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int x = current.x;
                int y = current.y;

                if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y) continue;

                int index = x + y * gridSize.x;
                if (visited[index]) continue;

                visited[index] = true;

                if (grid[index].isWalkable)
                {
                    return transform.position + new Vector3(x * cellDiameter + cellSize, 0f, y * cellDiameter + cellSize);
                }

                queue.Enqueue(new Vector2Int(x + 1, y));
                queue.Enqueue(new Vector2Int(x - 1, y));
                queue.Enqueue(new Vector2Int(x, y + 1));
                queue.Enqueue(new Vector2Int(x, y - 1));
            }

            return transform.position;
        }

        protected bool IsCellWalkable(Vector3 cellPosition, float radius)
        {
            Vector3 origin = cellPosition + Vector3.up * 0.1f;
            
            bool hitObstacles = Physics.CheckSphere(origin, radius, notWalkableMask.value);
            if (hitObstacles) return false;

            bool hitWalkableArea = Physics.CheckSphere(origin, radius, walkableMask.value);

            return hitWalkableArea;
        }

        protected Vector3 GetCellPositionInWorldMap(int gridX, int gridY)
        {
            Vector3 cellPosition = GetCellPositionInGrid(gridX, gridY);

            return CheckPoint(cellPosition);
        }

        private Vector3 GetCellPositionInGrid(int gridX, int gridY)
        {
            return transform.position
                   + Vector3.right   * ((gridX + 0.5f) * cellDiameter)
                   + Vector3.forward * ((gridY + 0.5f) * cellDiameter);
        }

        private Vector3 CheckPoint(Vector3 cellPosition)
        {
            return Physics.Raycast(cellPosition + Vector3.up * maxDistance, 
                    Vector3.down, out RaycastHit raycastHit, maxDistance, walkableMask)
                    ? raycastHit.point
                    : cellPosition;
        }
        
        protected (int x, int y) GetCellsMap(Vector3 worldPosition)
        {
            Vector3 gridPos = worldPosition - transform.position;

            int x = Mathf.Clamp(Mathf.FloorToInt((gridPos.x - cellSize) / cellDiameter), 0, gridSize.x - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((gridPos.z - cellSize) / cellDiameter), 0, gridSize.y - 1);

            return (x, y);
        }

        #region Unity Methods

        public virtual void Initialize()
        {
            cellSize = Mathf.Max(0.05f, cellSize);
            cellDiameter = cellSize * 2;

            CreateGrid();
        }

        public void Destroy()
        {
            if (grid.IsCreated) grid.Dispose();
        }

        #endregion
    }
}