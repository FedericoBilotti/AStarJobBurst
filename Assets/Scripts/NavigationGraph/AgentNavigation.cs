using Pathfinding;
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

        private Cell[] _waypointsPath;
        private Transform _transform;

        private int _currentWaypoint;

        public PathStatus Status { get; private set; } = PathStatus.Idle;
        public bool HasPath => _waypointsPath is { Length: > 0 };
        public float Speed { get => _speed; set => _speed = Mathf.Max(0.01f, value); }
        public float RotationSpeed { get => _rotationSpeed; set => _rotationSpeed = Mathf.Max(0.01f, value); }
        public float ChangeWaypointDistance { get => _changeWaypointDistance; set => _changeWaypointDistance = Mathf.Max(0.1f, value); }

        private void Start()
        {
            _transform = transform;
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
            if (_currentWaypoint >= _waypointsPath.Length)
            {
                _currentWaypoint = 0;
                return;
            }
            
            Vector3 distance = _waypointsPath[_currentWaypoint].position - _transform.position;
            Move(distance);
            Rotate(distance);
            CheckWaypoints(distance);
        }

        private void MapToGrid()
        {
            // If the agent is not on the grid, move it to the closest grid position
        }

        public void RequestPath(Vector3 startPosition, Vector3 endPosition)
        {
            if (Status == PathStatus.Requested) return;
            
            ClearPath();
            
            var navigationGraph = ServiceLocator.Instance.GetService<INavigationGraph>();
            
            Cell startCell = navigationGraph.GetCellWithWorldPosition(startPosition);
            Cell endCell = navigationGraph.GetCellWithWorldPosition(endPosition);
            ServiceLocator.Instance.GetService<IPathfinding>().RequestPath(this, startCell, endCell);
        }

        public void SetPath(Cell[] path)
        {
            if (path == null)
            {
                Status = PathStatus.Failed;
                return;
            }

            _waypointsPath = path;
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

            _currentWaypoint++;
            if (_currentWaypoint++ >= _waypointsPath.Length)
            {
                ClearPath();
            }
        }

        private void ClearPath()
        {
            _waypointsPath = null;
            // Status = PathStatus.Idle; ¿?
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
            if (_waypointsPath == null || _waypointsPath.Length == 0) return;

            Gizmos.color = Color.black;
            
            for (int i = _currentWaypoint; i < _waypointsPath.Length; i++)
            {
                Gizmos.DrawLine(i == _currentWaypoint ? transform.position : _waypointsPath[i - 1].position, _waypointsPath[i].position);
                Gizmos.DrawCube(_waypointsPath[i].position, Vector3.one * 0.35f);
            }
        }
    }
}