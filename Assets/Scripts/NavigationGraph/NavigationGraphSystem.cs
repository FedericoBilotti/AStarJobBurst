using System.Collections.Generic;
using System.Linq;
using NavigationGraph.Graph;
using UnityEngine;

namespace NavigationGraph
{
    public sealed class NavigationGraphSystem : MonoBehaviour
    {
        [Header("Gizmos")] [SerializeField] private bool _showBox;
        [SerializeField] private bool _showRaycasts;
        [SerializeField] private bool _showPreviewOfCells;
        [SerializeField] private Vector2 _cellSizeGizmos;

        [Header("Graph")] [SerializeField] private NavigationGraphType _graphType;
        [SerializeField] private Vector2Int _gridSize = new(100, 100);
        [SerializeField] private float _maxDistance = 15;
        [SerializeField] private float _cellSize = 0.5f;

        [Header("Check Wall")] [SerializeField] private LayerMask _notWalkableMask;
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

        // Each gizmo is going to be with his own grid.

        private void OnDrawGizmos()
        {
            DrawCubeForGrid();

            float boxBottomY = transform.position.y;
            float boxTopY = transform.position.y + _maxDistance;

            for (int x = 0; x < _gridSize.x; x++)
            for (int y = 0; y < _gridSize.y; y++)
            {
                Vector3[] positions = GetCellPositionInWorldMap(x, y);

                // Dibujo las líneas de techo a suelo
                foreach (var pos in positions) DrawLineForCell(pos, boxBottomY, boxTopY);

                // Dibujo las celdas (cubo) en la altura real, pero siempre dentro del rango
                DrawCells(positions, boxBottomY, boxTopY);
            }
        }

        private void DrawCubeForGrid()
        {
            if (!_showBox) return;

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
            if (!_showRaycasts) return;

            Vector3 topPoint = new Vector3(cellPosition.x, topY, cellPosition.z);
            Vector3 bottomPoint = new Vector3(cellPosition.x, bottomY, cellPosition.z);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(topPoint, bottomPoint);
        }

        private void DrawCells(Vector3[] cellPositions, float bottomY, float topY)
        {
            if (!_showPreviewOfCells) return;

            Vector3 sizeCell = new Vector3(_cellSizeGizmos.x, 0.05f, _cellSizeGizmos.y) * GetCellDiameter();

            foreach (var pos in cellPositions)
            {
                float clampedY = Mathf.Clamp(pos.y, bottomY, topY);
                Vector3 drawPos = new Vector3(pos.x, clampedY + 0.1f, pos.z);

                Gizmos.color = IsCellWalkable(pos) ? Color.green : Color.red;
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
            return transform.position + Vector3.right * ((gridX + 0.5f) * GetCellDiameter()) + Vector3.forward * ((gridY + 0.5f) * GetCellDiameter());
        }

        private Vector3[] CheckPoint(Vector3 cellPosition)
        {
            Vector3 from = cellPosition + Vector3.up * _maxDistance;
            LayerMask combined = _walkableMask | _notWalkableMask;
            return RaycastContinuous(from, combined).Select(h => h.point).ToArray();

            // return Physics.Raycast(cellPosition + Vector3.up * _maxDistance, 
            //         Vector3.down, out RaycastHit raycastHit, _maxDistance, _walkableMask)
            //         ? raycastHit.point
            //         : cellPosition;
        }

        private List<RaycastHit> RaycastContinuous(Vector3 from, LayerMask mask)
        {
            List<RaycastHit> hits = new List<RaycastHit>();
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, _maxDistance * 2, mask)) return hits;

            hits.Add(hit);
            float minDist = _cellSize * 0.5f;

            for (int i = 0; i < 10; i++)
            {
                Vector3 nextOrigin = hit.point + Vector3.down * minDist;
                if (!Physics.Raycast(nextOrigin, Vector3.down, out hit, _maxDistance * 2, mask)) break;

                if (hits.Any(h => Mathf.Abs(h.point.y - hit.point.y) < minDist)) continue;

                hits.Add(hit);
            }

            return hits;
        }

        private bool IsCellWalkable(Vector3 cellPosition)
        {
            Vector3 origin = cellPosition + Vector3.up * _maxDistance;

            bool hitObstacles = Physics.SphereCast(origin, 0.1f, Vector3.down, out _, _maxDistance, _notWalkableMask.value);

            if (hitObstacles) return false;

            // This is for check the air, so if it touches walkable area, it's okay, but if it doesn't, it's not walkable because it's the air.
            bool hitWalkableArea = Physics.SphereCast(origin, 0.1f, Vector3.down, out _, _maxDistance, _walkableMask.value);

            return hitWalkableArea;
        }

        private float GetCellDiameter() => _cellSize * 2;

        #endregion
    }
}