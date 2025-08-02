using System.Linq;
using NavigationGraph.Graph;
using UnityEditor;
using UnityEngine;

namespace NavigationGraph
{
    public sealed class NavigationGraphSystem : MonoBehaviour
    {
        [Header("Gizmos")]
        [SerializeField] private bool _showBox;
        [SerializeField] private bool _showRaycasts;
        [SerializeField] private bool _showCells;
        [SerializeField] private Vector2 _cellSizeGizmos;

        [Header("Graph")] 
        [SerializeField] private NavigationGraphType _graphType;
        [SerializeField] private float _cellSize = 0.5f;
        [SerializeField] private Vector2Int _gridSize = new(100, 100);
        
        [Header("Check Wall")] 
        [SerializeField] private float _maxDistance = 15;
        [SerializeField] private LayerMask _notWalkableMask;
        [SerializeField] private LayerMask _walkableMask;


        private NavigationGraph _graph;

        private void Awake()
        {
            _graph = _graphType == NavigationGraphType.Grid2D
                    ? new SimpleGridNavigationGraph(_cellSize, _maxDistance, _gridSize, _notWalkableMask, transform, _walkableMask)
                    : new WorldNavigationGraph(_cellSize, _maxDistance, _gridSize, _notWalkableMask, transform, _walkableMask);
            _graph?.Initialize();
            
            ServiceLocator.Instance.RegisterService<INavigationGraph>(_graph);
        }

        private void OnValidate()
        {
            _cellSizeGizmos.x = Mathf.Min(1f, _cellSizeGizmos.x);
            _cellSizeGizmos.y = Mathf.Min(1f, _cellSizeGizmos.y);
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

                Vector3 cellPosition = GetCellPositionInWorldMap(x, y);

                DrawCells(cellPosition);
                DrawLinesForCells(cellPosition);
            }
        }

        private void DrawCubeForGrid()
        {
            if (!_showBox) return;

            float width  = _gridSize.x * GetCellDiameter();
            float depth  = _gridSize.y * GetCellDiameter();
            float height = _maxDistance;

            Vector3 gridCenter = transform.position
                                 + Vector3.right   * (width  * 0.5f)
                                 + Vector3.forward * (depth  * 0.5f)
                                 + Vector3.up      * (height * 0.5f);

            Vector3 boxSize = new Vector3(width, height, depth);

            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(gridCenter, boxSize);
        }

        private void DrawLinesForCells(Vector3 cellPosition)
        {
            if (!_showRaycasts) return;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(cellPosition + Vector3.up * _maxDistance, cellPosition);
        }

        private void DrawCells(Vector3 cellPosition)
        {
            if (!_showCells) return;

            Vector3 sizeCell = new Vector3(_cellSizeGizmos.x, 0.05f, _cellSizeGizmos.y) * GetCellDiameter();
            Vector3 cellPositionForGizmos = cellPosition + Vector3.up * 0.1f;
            bool isWalkable = IsCellWalkable(cellPosition);
            
            Gizmos.color = isWalkable ? Color.green : Color.red;
            Gizmos.DrawWireCube(cellPositionForGizmos, sizeCell);
        }

        private Vector3 GetCellPositionInWorldMap(int gridX, int gridY)
        {
            Vector3 cellPosition = GetCellPositionInGrid(gridX, gridY);

            return CheckPoint(cellPosition);
        }

        private Vector3 GetCellPositionInGrid(int gridX, int gridY)
        {
            return transform.position
                   + Vector3.right   * ((gridX + 0.5f) * GetCellDiameter())
                   + Vector3.forward * ((gridY + 0.5f) * GetCellDiameter());
        }

        private Vector3 CheckPoint(Vector3 cellPosition)
        {
            return Physics.Raycast(cellPosition + Vector3.up * _maxDistance, 
                    Vector3.down, out RaycastHit raycastHit, _maxDistance, _walkableMask)
                    ? raycastHit.point
                    : cellPosition;
        }

        private bool IsCellWalkable(Vector3 cellPosition)
        {
            Vector3 origin = cellPosition + Vector3.up * _maxDistance;
            
            bool hitObstacles = Physics.SphereCast(origin, _cellSize, Vector3.down, out _, _maxDistance, _notWalkableMask.value);

            if (hitObstacles) return false;
            
            // This is for check the air, so if it touches walkable area, it's okay, but if it doesn't, it's not walkable because it's the air.
            bool hitWalkableArea = Physics.SphereCast(origin, 0.1f, Vector3.down, out _, _maxDistance, _walkableMask.value);

            return hitWalkableArea;
        }

        private float GetCellDiameter() => _cellSize * 2;

        #endregion
    }
}