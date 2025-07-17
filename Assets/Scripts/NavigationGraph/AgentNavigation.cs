using System.Collections.Generic;
using Pathfinding;
using Unity.Collections;
using UnityEngine;

namespace NavigationGraph
{
    public class AgentNavigation : MonoBehaviour, IAgent
    {
        [SerializeField] private float _speed = 5;
        [SerializeField] private float _rotationSpeed = 10;
        [SerializeField] private float _changeWaypointDistance = 0.5f;

        [Header("Debug")] 
        [SerializeField] private bool _showPath;

        private IPathfinding _pathfinding;
        private INavigationGraph _graph;
        private List<Vector3> _waypointsPath;
        private Transform _transform;

        private int _currentWaypoint;

        public PathStatus Status { get; private set; } = PathStatus.Idle;
        public bool HasPath => _waypointsPath != null && _waypointsPath.Count > 0 && Status == PathStatus.Success;
        public float Speed { get => _speed; set => _speed = Mathf.Max(0.01f, value); }
        public float RotationSpeed { get => _rotationSpeed; set => _rotationSpeed = Mathf.Max(0.01f, value); }
        public float ChangeWaypointDistance { get => _changeWaypointDistance; set => _changeWaypointDistance = Mathf.Max(0.1f, value); }

        private void Start()
        {
            _waypointsPath = new List<Vector3>(10);
            _transform = transform;
            _pathfinding = ServiceLocator.Instance.GetService<IPathfinding>();
            _graph = ServiceLocator.Instance.GetService<INavigationGraph>();
        }

        private void OnValidate()
        {
            _speed = Mathf.Max(0.01f, _speed);
            _rotationSpeed = Mathf.Max(0.01f, _rotationSpeed);
            _changeWaypointDistance = Mathf.Max(0.1f, _changeWaypointDistance);
        }

        private void Update()
        {
            MapToGrid();
            
            if (!HasPath) return;
            if (_currentWaypoint >= _waypointsPath.Count)
            {
                ClearPath();
                Status = PathStatus.Idle;
                return;
            }
            
            Vector3 distance = _waypointsPath[_currentWaypoint] - _transform.position;
            Move(distance);
            Rotate(distance);
            CheckWaypoints(distance);
        }

        private void MapToGrid()
        {
            // If the agent is not on the grid, move it to the closest grid position
            if (IsAgentInGrid()) return;
            
            MapToNearestCellPosition();
        }

        private void MapToNearestCellPosition()
        {
            
        }

        private bool IsAgentInGrid() => _graph.IsInGrid(_transform.position);

        public void RequestPath(Vector3 startPosition, Vector3 endPosition)
        {
            if (Status == PathStatus.Requested) return;
            if (!IsAgentInGrid())
            {
                Debug.LogWarning("Agent is not in the grid");
                return;
            }
            
            Status = PathStatus.Requested;
            ClearPath();
            
            Cell startCell = _graph.GetCellWithWorldPosition(startPosition);
            Cell endCell = _graph.GetCellWithWorldPosition(endPosition);
            _pathfinding.RequestPath(this, startCell, endCell);
        }

        public void SetPath(NativeList<Cell> path)
        {
            if (!path.IsCreated || path.Length == 0)
            {
                Status = PathStatus.Failed;
                return;
            }

            foreach (var cell in path)
            {
                _waypointsPath.Add(cell.position);
            }

            Status = PathStatus.Success;
        }

        private void Move(Vector3 distance)
        {
            _transform.position += distance.normalized * (_speed * Time.deltaTime);
        }

        private void Rotate(Vector3 distance)
        {
            Quaternion lookRotation = Quaternion.LookRotation(distance);
            _transform.rotation = Quaternion.Slerp(_transform.rotation, lookRotation, _rotationSpeed * Time.deltaTime);
        }

        private void CheckWaypoints(Vector3 distance)
        {
            if (distance.magnitude > _changeWaypointDistance * _changeWaypointDistance) return;

            if (++_currentWaypoint < _waypointsPath.Count) return;
            
            ClearPath();
            Status = PathStatus.Idle;
        }

        private void ClearPath()
        {
            _currentWaypoint = 0;
            _waypointsPath.Clear();
        }

        public enum PathStatus
        {
            Idle,
            Failed,
            Requested,
            Success
        }

        private void OnDrawGizmos()
        {
            if (!_showPath) return;
            if (_waypointsPath == null || _waypointsPath.Count == 0) return;

            Gizmos.color = Color.black;
            
            for (int i = _currentWaypoint; i < _waypointsPath.Count; i++)
            {
                Gizmos.DrawLine(i == _currentWaypoint ? transform.position : _waypointsPath[i - 1], _waypointsPath[i]);
                Gizmos.DrawCube(_waypointsPath[i], Vector3.one * 0.35f);
            }
        }
    }
}