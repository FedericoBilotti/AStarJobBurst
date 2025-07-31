using NavigationGraph.Graph;
using UnityEngine;

namespace NavigationGraph
{
    public sealed class NavigationGraphSystem : MonoBehaviour
    {
        [SerializeField] private bool _showBox;
        [SerializeField] private bool _showLines;
        [SerializeField] private bool _showCells;
        [SerializeField] private float _cellSize = 0.5f;
        [SerializeField] private Vector2Int _gridSize = new(100, 100);

        [Header("Check Wall")] 
        [SerializeField] private float _maxDistance = 15;
        [SerializeField] private LayerMask _notWalkableMask;

        [Header("Graph Type")] [SerializeField] private NavigationGraphType _graphType;

        private NavigationGraph _graph;

        private void Awake()
        {
            _graph = _graphType == NavigationGraphType.Grid2D
                    ? new SimpleGridNavigationGraph(_cellSize, _maxDistance, _gridSize, _notWalkableMask, transform)
                    : new WorldNavigationGraph(_cellSize, _maxDistance, _gridSize, _notWalkableMask, transform);
            _graph?.Initialize();
            
            ServiceLocator.Instance.RegisterService<INavigationGraph>(_graph);
        }
        
        private void OnDestroy() => _graph?.Destroy();

        public enum NavigationGraphType
        {
            Grid2D,
            Grid3D,
        }

        #region Gizmos

        private void OnDrawGizmos()
        {
            DrawCubeForGrid();

            for (int i = 0; i < _gridSize.x * _gridSize.y; i++)
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

            if (CheckPoint(cellPosition, out RaycastHit raycastHit))
            {
                Gizmos.DrawLine(cellPosition + Vector3.up * _maxDistance, raycastHit.point);
                return;
            }

            Gizmos.DrawLine(cellPosition + Vector3.up * _maxDistance, cellPosition);
        }

        private void DrawCells(Vector3 cellPosition)
        {
            if (!_showCells) return;

            Vector3 sizeCell = new Vector3(.5f, 0, .5f) * GetCellDiameter();
            bool isWalkable = IsCellWalkable(cellPosition);

            Gizmos.color = isWalkable ? Color.green : Color.red;

            if (CheckPoint(cellPosition, out RaycastHit raycastHit))
            {
                Gizmos.DrawWireCube(raycastHit.point, sizeCell);
                return;
            }

            Gizmos.DrawWireCube(cellPosition, sizeCell);
        }

        private Vector3 GetCellPosition(int gridX, int gridY)
        {
            return transform.position + Vector3.right * (gridX * GetCellDiameter() + _cellSize) + Vector3.forward * (gridY * GetCellDiameter() + _cellSize);
        }

        private bool CheckPoint(Vector3 cellPosition, out RaycastHit raycastHit)
        {
            return Physics.Raycast(cellPosition + Vector3.up * _maxDistance, Vector3.down, out raycastHit, _maxDistance);
        }

        private bool IsCellWalkable(Vector3 cellPosition)
        {
            return !Physics.SphereCast(cellPosition + Vector3.up * _maxDistance, _cellSize, Vector3.down, out _, _maxDistance, _notWalkableMask);
        }
        
        private float GetCellDiameter() => _cellSize * 2;

        #endregion
    }
}