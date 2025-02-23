using Unity.Collections;
using UnityEngine;

namespace NavGridSystem
{
    public class AgentNavigation : MonoBehaviour
    {
        [SerializeField] private float _speed = 5;
        [SerializeField] private float _rotationSpeed = 10;
        [SerializeField] private float _changeWaypointDistance = 0.5f;

        [Header("Debug")] 
        [SerializeField] private bool _showPath;

        private NativeList<Cell> _waypointsPath;
        private Transform _transform;
        private Vector3 _endPosition;

        private int _currentWaypoint;
        
        public bool HasPath => _waypointsPath.Length > 0;
        public float Speed { get => _speed; set => _speed = Mathf.Max(0, value); }
        public float RotationSpeed { get => _rotationSpeed; set => _rotationSpeed = Mathf.Max(0, value); }
        public float ChangeWaypointDistance { get => _changeWaypointDistance; set => _changeWaypointDistance = Mathf.Max(0, value); }

        #region Unity Methods

        private void Awake()
        {
            _waypointsPath = new NativeList<Cell>(30, Allocator.Persistent);
            _transform = transform;
        }

        private void OnDestroy() => _waypointsPath.Dispose();

        private void OnValidate()
        {
            _speed = Mathf.Max(0, _speed);
            _rotationSpeed = Mathf.Max(0, _rotationSpeed);
            _changeWaypointDistance = Mathf.Max(0.1f, _changeWaypointDistance);
        }

        private void Update()
        {
            if (!_waypointsPath.IsCreated) return;
            if (!HasPath) return;

            if (_currentWaypoint >= _waypointsPath.Length) 
                _currentWaypoint = 0;
            
            Vector3 distance = _waypointsPath[_currentWaypoint].position - _transform.position;
            PathMovement(distance);
            CheckWaypoints(distance);
        }

        private void OnDrawGizmos()
        {
            if (!_showPath) return;
            if (!_waypointsPath.IsCreated || _waypointsPath.Length == 0) return;

            Gizmos.color = Color.black;
            
            for (int i = _currentWaypoint; i < _waypointsPath.Length; i++)
            {
                Gizmos.DrawLine(i == _currentWaypoint ? transform.position : _waypointsPath[i - 1].position, _waypointsPath[i].position);
                Gizmos.DrawCube(_waypointsPath[i].position, Vector3.one * 0.35f);
            }
        }

        #endregion

        public void RequestPath(Vector3 start, Vector3 end)
        {
            _waypointsPath.Clear();
            AStarRequest.Instance.RequestPath(ref _waypointsPath, start, end);
        }
        
        public void RequestPath(Cell start, Cell end)
        {
            _waypointsPath.Clear();
            AStarRequest.Instance.RequestPath(ref _waypointsPath, start, end);
        }

        private void PathMovement(Vector3 distance)
        {
            Move(distance);
            Rotate(distance);
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
            if (_currentWaypoint >= _waypointsPath.Length)
            {
                ClearPath();
            }
        }

        private void ClearPath()
        {
            _waypointsPath.Clear();
            _currentWaypoint = 0;
        }
    }
}