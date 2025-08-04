using System.Collections.Generic;
using System.Linq;
using NavigationGraph.Graph;
using UnityEngine;

namespace NavigationGraph
{
    public sealed class NavigationGraphSystem : MonoBehaviour
    {
        [Header("Gizmos")] 
        [SerializeField] private bool _boxGrid;
        [SerializeField] private bool _scanDistance;
        [SerializeField] private bool _previewOfCells;
        [SerializeField] private bool _debugOfWalkableCells;
        [SerializeField] private Vector2 _cellSizeGizmos;

        [Header("Graph")] 
        [SerializeField] private NavigationGraphType _graphType;
        [SerializeField] private Vector2Int _gridSize = new(100, 100);
        [SerializeField] private float _maxDistance = 15;
        [SerializeField] private float _cellSize = 0.5f;

        [Header("Check Wall")] 
        [SerializeField] private int _maxHits = 10;
        [SerializeField] private LayerMask _notWalkableMask;
        [SerializeField] private LayerMask _walkableMask;
        [SerializeField] private LayerMask _agentMask;

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
            _cellSizeGizmos.x = Mathf.Max(0.01f, _cellSizeGizmos.x);
            _cellSizeGizmos.x = Mathf.Min(0.95f, _cellSizeGizmos.x);
            _cellSizeGizmos.y = Mathf.Max(0.01f, _cellSizeGizmos.y);
            _cellSizeGizmos.y = Mathf.Min(0.95f, _cellSizeGizmos.y);
        }

        private void OnDestroy() => _graph?.Destroy();

        public enum NavigationGraphType
        {
            Grid2D,
            Grid3D,
        }

        #region Gizmos

        // Each gizmo is going to be with his own grid.

        private void OnDrawGizmos()
        {
            DrawCubeForGrid();

            float boxBottomY = transform.position.y;
            float boxTopY = boxBottomY + _maxDistance;

            for (int x = 0; x < _gridSize.x; x++)
            for (int y = 0; y < _gridSize.y; y++)
            {
                Vector3[] positions = GetCellPositionInWorldMap(x, y);

                if (positions.Length == 0) continue;

                if (_previewOfCells)
                    DrawCells(positions, boxBottomY, boxTopY);

                if (!_scanDistance) continue;
                
                DrawLineForCell(positions[0], boxBottomY, boxTopY);
            }
        }

        private void DrawCubeForGrid()
        {
            if (!_boxGrid) return;

            float width = _gridSize.x * GetCellDiameter();
            float depth = _gridSize.y * GetCellDiameter();
            float height = _maxDistance;

            Vector3 gridCenter = transform.position + Vector3.right * (width * 0.5f) + Vector3.forward * (depth * 0.5f) + Vector3.up * (height * 0.5f);

            Vector3 boxSize = new Vector3(width, height, depth);

            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(gridCenter, boxSize);
        }

        private void DrawLineForCell(Vector3 cellPosition, float bottomY, float topY)
        {
            Vector3 topPoint = new Vector3(cellPosition.x, topY, cellPosition.z);
            Vector3 bottomPoint = new Vector3(cellPosition.x, bottomY, cellPosition.z);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(topPoint, bottomPoint);
        }

        private void DrawCells(Vector3[] cellPositions, float bottomY, float topY)
        {
            Vector3 sizeCell = new Vector3(_cellSizeGizmos.x, 0.05f, _cellSizeGizmos.y) * GetCellDiameter();

            foreach (var pos in cellPositions)
            {
                float clampedY = Mathf.Clamp(pos.y, bottomY, topY);
                Vector3 drawPos = new Vector3(pos.x, clampedY + 0.1f, pos.z);
                bool isWalkable = IsCellWalkable(pos, 1.5f);
                
                Gizmos.color = isWalkable ? Color.green : Color.red;
                Gizmos.DrawWireCube(drawPos, sizeCell);
            }
        }

        private Vector3[] GetCellPositionInWorldMap(int gridX, int gridY)
        {
            Vector3 cellPosition = GetCellPositionWorld(gridX, gridY);

            return CheckPoint(cellPosition);
        }

        private Vector3 GetCellPositionWorld(int gridX, int gridY)
        {
            return transform.position 
                   + Vector3.right * ((gridX + 0.5f) * GetCellDiameter()) 
                   + Vector3.forward * ((gridY + 0.5f) * GetCellDiameter());
        }

        private Vector3[] CheckPoint(Vector3 cellPosition)
        {
            Vector3 from = cellPosition + Vector3.up * _maxDistance;
            LayerMask combined = _walkableMask | _notWalkableMask;
            return RaycastContinuous(from, combined).Select(h => h.point).ToArray();

            // 2d grid: 
            // return Physics.Raycast(cellPosition + Vector3.up * _maxDistance, 
            //         Vector3.down, out RaycastHit raycastHit, _maxDistance, _walkableMask)
            //         ? raycastHit.point
            //         : cellPosition;
        }

        private List<RaycastHit> RaycastContinuous(Vector3 from, LayerMask mask)
        {
            List<RaycastHit> hits = new List<RaycastHit>();
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, _maxDistance, mask)) return hits;

            hits.Add(hit);
            float minDist = _cellSize * 0.5f;
            
            for (int i = 0; i < _maxHits; i++)
            {
                Vector3 nextOrigin = hit.point + Vector3.down * minDist;
                if (!Physics.Raycast(nextOrigin, Vector3.down, out hit, _maxDistance, mask)) break;

                if (hits.Any(h => Mathf.Abs(h.point.y - hit.point.y) < minDist)) continue;

                hits.Add(hit);
            }

            return hits;
        }

        private bool IsCellWalkable(Vector3 cellPosition, float radius)
        {
            Vector3 origin = cellPosition + Vector3.up * 0.1f;

            if (_debugOfWalkableCells)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawWireSphere(origin, radius);
            }

            var hitObstacles = Physics.CheckSphere(origin, radius, _notWalkableMask.value);
            if (hitObstacles) return false;

            // Check if it's something up.
            var ray = new Ray(origin + Vector3.up * 0.1f, Vector3.up);
            bool hitHeight = Physics.SphereCast(ray, 0.5f, 1.5f, ~_agentMask.value);
            if (hitHeight) return false;
            
            // This is for check the air, so if it touches walkable area, it's okay, but if it doesn't, it's not walkable because it's the air.
            bool hitWalkableArea = Physics.CheckSphere(origin, radius, _walkableMask.value);

            return hitWalkableArea;
        }

        private float GetCellDiameter() => _cellSize * 2;

        #endregion
    }
}