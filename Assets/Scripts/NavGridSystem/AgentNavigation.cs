using Unity.Collections;
using UnityEngine;

namespace NavGridSystem
{
    public class AgentNavigation : MonoBehaviour
    {
        [SerializeField] private float _speed = 5;
        [SerializeField] private float _rotationSpeed = 10;
        [SerializeField] private float _changeWaypointDistance = 0.5f;

        [SerializeField] private bool _hasPath;

        [Header("Debug")] 
        [SerializeField] private bool _showPath;

        private NativeList<Cell> _path;
        private Transform _transform;
        private Vector3 _endPosition;

        private int _currentWaypoint;
        
        public bool HasPath => _hasPath;
        public float Speed { get => _speed; set => _speed = Mathf.Max(0, value); }
        public float RotationSpeed { get => _rotationSpeed; set => _rotationSpeed = Mathf.Max(0, value); }
        public float ChangeWaypointDistance { get => _changeWaypointDistance; set => _changeWaypointDistance = Mathf.Max(0, value); }

        #region Unity Methods

        private void Awake()
        {
            _path = new NativeList<Cell>(30, Allocator.Persistent);
            _transform = transform;
        }

        private void OnDestroy() => _path.Dispose();

        private void OnValidate()
        {
            _speed = Mathf.Max(0, _speed);
            _rotationSpeed = Mathf.Max(0, _rotationSpeed);
            _changeWaypointDistance = Mathf.Max(0.1f, _changeWaypointDistance);
        }

        private void Update()
        {
            if (!_path.IsCreated) return;
            if (_path.Length == 0) return;

            Vector3 distance = _path[_currentWaypoint % _path.Length].position - _transform.position;
            CheckWaypoints(distance);
            PathMovement(distance);
        }

        private void OnDrawGizmos()
        {
            if (!_showPath) return;
            if (!_path.IsCreated || _path.Length == 0) return;

            Gizmos.color = Color.black;
            
            for (int i = _currentWaypoint; i < _path.Length; i++)
            {
                Gizmos.DrawLine(i == _currentWaypoint ? transform.position : _path[i - 1].position, _path[i].position);
                Gizmos.DrawCube(_path[i].position, Vector3.one * 0.35f);
            }
        }

        #endregion

        public void RequestPath(Vector3 start, Vector3 end)
        {
            // Prevents the path request to be called when the end position is the same.
            if (_endPosition == end) return;
            
            _endPosition = end;
            _path.Clear();
            _hasPath = AStarRequest.Instance.RequestPath(ref _path, start, end);
        }

        private void PathMovement(Vector3 distance)
        {
            Move(distance);
            Rotate(distance);
        }

        private void Move(Vector3 distance) => _transform.position += distance.normalized * (_speed * Time.deltaTime);

        private void Rotate(Vector3 distance)
        {
            Quaternion lookRotation = Quaternion.LookRotation(distance);
            _transform.rotation = Quaternion.Slerp(_transform.rotation, lookRotation, _rotationSpeed * Time.deltaTime);
        }

        private void CheckWaypoints(Vector3 distance)
        {
            if (distance.magnitude > _changeWaypointDistance * _changeWaypointDistance) return;
            
            _currentWaypoint++;
            if (_currentWaypoint < _path.Length) return;
            
            _currentWaypoint = 0;
            _hasPath = false;
            _path.Clear();
        }
    }
}