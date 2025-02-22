using Unity.Collections;
using UnityEngine;
using Utilities;
using Vector3 = UnityEngine.Vector3;

namespace NavGridSystem
{
    public class GridSystem : Singleton<GridSystem>, IGridSystem
    {
        [SerializeField] private bool _showGizmos;
        [SerializeField] private bool _showGrid;
        [SerializeField] private bool _showLines;
        [SerializeField] private float _cellSize;
        [SerializeField] private Vector2Int _gridSize;

        [Header("Check Wall")] 
        [SerializeField] private float _maxDistance = 100;
        [SerializeField] private LayerMask _notWalkableMask = 6;

        private float _cellDiameter;

        private NativeArray<Cell> _grid;

        public NativeArray<Cell> GetGrid() => _grid;
        public int GetGridSize() => _gridSize.x * _gridSize.y;
        public int GetGridSizeX() => _gridSize.x;

        public Cell GetCellWithWorldPosition(Vector3 worldPosition)
        {
            Vector3 gridPos = worldPosition - transform.position; 
            
            int x = Mathf.Clamp(Mathf.FloorToInt((gridPos.x - _cellSize) / _cellDiameter), 0, _gridSize.x - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((gridPos.z - _cellSize) / _cellDiameter), 0, _gridSize.y - 1);
            
            return _grid[x + y * _gridSize.x];
        }

        private void CreateGrid()
        {
            if (_grid.IsCreated)
            {
                _grid.Dispose();
            }
            
            _grid = new NativeArray<Cell>(_gridSize.x * _gridSize.y, Allocator.Persistent);
            
            for (int i = 0; i < _grid.Length; i++)
            {
                int x = i % _gridSize.x;
                int y = i / _gridSize.x;
                
                Vector3 cellPosition = transform.position + Vector3.right * (x * _cellDiameter + _cellSize) + Vector3.forward * (y * _cellDiameter + _cellSize);

                bool isWalkable = !Physics.SphereCast(cellPosition + Vector3.up * _maxDistance, _cellSize, Vector3.down, out RaycastHit raycastHit, _maxDistance, _notWalkableMask);
                
                _grid[i] = new Cell
                {
                    position = !isWalkable ? raycastHit.point : cellPosition,
                    gridIndex = i,
                    x = x,
                    y = y,
                    isWalkable = isWalkable,
                };
            }
        }
        
        #region Unity Methods

        private void OnValidate()
        {
            _cellSize = Mathf.Max(0.05f, _cellSize);
            _cellDiameter = _cellSize * 2;
            
            CreateGrid();
        }

#if UNITY_EDITOR

        private void OnDisable()
        {
            if (_grid.IsCreated) 
                _grid.Dispose();
        }

#endif
        
        private void OnDestroy()
        {
            if (_grid.IsCreated)
                _grid.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;
            if (!_grid.IsCreated || _grid.Length == 0) return;

            for (int i = 0; i < _grid.Length; i++)
            {
                Vector3 cellPosition = _grid[i].position;
                
                if (_showLines)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(cellPosition + Vector3.up * _maxDistance, cellPosition);
                }

                if (_showGrid)
                {
                    Vector3 cellSize = new Vector3(.75f, 0, .75f) * _cellDiameter;
                    Gizmos.color = _grid[i].isWalkable ? Color.green : Color.red;
                    Gizmos.DrawWireCube(_grid[i].position, cellSize);
                }
            }
        }
        
        #endregion
    }
}