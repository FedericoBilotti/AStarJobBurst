using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace NavigationGraph
{
    public abstract class NavigationGraph : INavigationGraph
    {
        private readonly LayerMask _notWalkableMask;    
        private readonly float _maxDistance;
        private float _cellSize;
        private float _cellDiameter;
        protected Vector2Int gridSize;
        protected NativeArray<Cell> grid;
        
        private readonly Transform _transform;

        public NavigationGraphSystem.NavigationGraphType GraphType { get; protected set; }

        protected NavigationGraph(float cellSize, float maxDistance, Vector2Int gridSize, LayerMask notWalkableMask, Transform transform)
        {
            _cellSize = cellSize;
            this.gridSize = gridSize;
            
            _maxDistance = maxDistance;
            _notWalkableMask = notWalkableMask;
            
            _transform = transform;
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
            Vector3 gridPos = worldPosition - _transform.position;

            int x = Mathf.FloorToInt((gridPos.x - _cellSize) / _cellDiameter);
            int y = Mathf.FloorToInt((gridPos.z - _cellSize) / _cellDiameter);

            if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y) return false;

            int gridIndex = x + y * gridSize.x;
            return grid[gridIndex].isWalkable;
        }

        public Vector3 GetNearestCellPosition(Vector3 worldPosition)
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
                    return _transform.position + new Vector3(x * _cellDiameter + _cellSize, 0f, y * _cellDiameter + _cellSize);
                }

                queue.Enqueue(new Vector2Int(x + 1, y));
                queue.Enqueue(new Vector2Int(x - 1, y));
                queue.Enqueue(new Vector2Int(x, y + 1));
                queue.Enqueue(new Vector2Int(x, y - 1));
            }

            return _transform.position;
        }

        protected bool IsCellWalkable(Vector3 cellPosition)
        {
            return !Physics.SphereCast(cellPosition + Vector3.up * _maxDistance, _cellSize, Vector3.down, out _, _maxDistance, _notWalkableMask);
        }

        protected Vector3 GetCellPosition(int gridX, int gridY)
        {
            return _transform.position + Vector3.right * (gridX * _cellDiameter + _cellSize) + Vector3.forward * (gridY * _cellDiameter + _cellSize);
        }

        private (int x, int y) GetCellsMap(Vector3 worldPosition)
        {
            Vector3 gridPos = worldPosition - _transform.position;

            int x = Mathf.Clamp(Mathf.FloorToInt((gridPos.x - _cellSize) / _cellDiameter), 0, gridSize.x - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((gridPos.z - _cellSize) / _cellDiameter), 0, gridSize.y - 1);

            return (x, y);
        }

        #region Unity Methods

        public void Initialize()
        {
            _cellSize = Mathf.Max(0.05f, _cellSize);
            _cellDiameter = _cellSize * 2;
            
            CreateGrid();
        }

        public void Destroy()
        {
            if (grid.IsCreated) grid.Dispose();
        }

        #endregion
    }
}