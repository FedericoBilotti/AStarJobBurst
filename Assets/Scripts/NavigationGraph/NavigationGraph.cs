using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

namespace NavigationGraph
{
    public class NavigationGraph : MonoBehaviour, INavigationGraph
    {
        [SerializeField] private bool _showGizmos;
        [SerializeField] private bool _showBox;
        [SerializeField] private bool _showLines;
        [SerializeField] private bool _showCells;
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

        public Cell GetRandomCell() => _grid[Random.Range(0, _grid.Length)];

        public Cell GetCellWithWorldPosition(Vector3 worldPosition)
        {
            Vector3 gridPos = worldPosition - transform.position; 
            
            int x = Mathf.Clamp(Mathf.FloorToInt((gridPos.x - _cellSize) / _cellDiameter), 0, _gridSize.x - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((gridPos.z - _cellSize) / _cellDiameter), 0, _gridSize.y - 1);
            
            return _grid[x + y * _gridSize.x];
        }

        public bool IsInGrid(Vector3 worldPosition)
        {
            Vector3 gridPos = worldPosition - transform.position;
            
            int x = Mathf.FloorToInt((gridPos.x - _cellSize) / _cellDiameter);
            int y = Mathf.FloorToInt((gridPos.z - _cellSize) / _cellDiameter);
            
            if (x < 0 || x >= _gridSize.x || y < 0 || y >= _gridSize.y)
                return false;

            int gridIndex = x + y * _gridSize.x;
            return _grid[gridIndex].isWalkable;
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

                Vector3 cellPosition = GetCellPosition(x, y);

                bool isWalkable = IsCellWalkable(cellPosition);
                
                _grid[i] = new Cell
                {
                    position = cellPosition,
                    gridIndex = i,
                    x = x,
                    y = y,
                    isWalkable = isWalkable,
                };
            }
        }

        private Vector3 GetCellPosition(int gridX, int gridY)
        {
            return transform.position + Vector3.right * (gridX * _cellDiameter + _cellSize) + Vector3.forward * (gridY * _cellDiameter + _cellSize);
        }

        private bool IsCellWalkable(Vector3 cellPosition)
        {
            return !Physics.SphereCast(cellPosition + Vector3.up * _maxDistance, _cellSize, Vector3.down, out RaycastHit raycastHit, _maxDistance, _notWalkableMask);
        }
        
        #region Unity Methods

        private void OnValidate()
        {
            _cellSize = Mathf.Max(0.05f, _cellSize);
            _cellDiameter = _cellSize * 2;
        }

        private void Awake()
        {
            CreateGrid();
            ServiceLocator.Instance.RegisterService<INavigationGraph>(this);
        }

        private void OnDestroy()
        {
            if (_grid.IsCreated) 
                _grid.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            DrawCubeForGrid();

            if (!_showLines && !_showCells) return;

            for (int i = 0; i < GetGridSize(); i++)
            {
                int x = i % _gridSize.x;
                int y = i / _gridSize.x;

                Vector3 cellPosition = GetCellPosition(x, y);
                
                DrawLinesForCells(cellPosition);
                DrawCells(cellPosition);
            }
        }

        private void DrawCubeForGrid()
        {
            if (!_showBox) return;
            
            var gridPosition = transform.position + Vector3.right * _gridSize.x / 2 + Vector3.forward * _gridSize.y / 2;
            
            var boxSize = new Vector3(_gridSize.x, 1, _gridSize.y);
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(gridPosition, boxSize);
        }

        private void DrawLinesForCells(Vector3 cellPosition)
        {
            if (!_showLines) return;
            
            Gizmos.color = Color.white;
            Gizmos.DrawLine(cellPosition + Vector3.up * _maxDistance, cellPosition);
        }

        private void DrawCells(Vector3 cellPosition)
        {
            if (!_showCells) return;
            
            Vector3 cellSize = new Vector3(.5f, 0, .5f) * _cellDiameter;
            bool isWalkable = IsCellWalkable(cellPosition);
            
            Gizmos.color = isWalkable ? Color.green : Color.red;
            Gizmos.DrawWireCube(cellPosition, cellSize);
        }

        #endregion
    }
}