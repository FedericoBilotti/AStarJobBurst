using System;
using Unity.Collections;
using UnityEngine;

namespace NavGridSystem
{
    public class AgentNavigation : MonoBehaviour
    {
        [SerializeField] private float _speed;
        [SerializeField] private float _rotationSpeed;
        
        [SerializeField] private bool _showPath;
        
        private bool _hasPath;
        
        private NativeList<int> _path;
        
        #region Unity Methods

        private void Awake()
        {
            _path = new NativeList<int>(30, Allocator.Persistent);
        }

        private void OnDestroy() => _path.Dispose();

        private void Update()
        {
            
        }

        private void FixedUpdate()
        {
            if (!_hasPath) return;
            
        }

        private void OnDrawGizmos()
        {
            if (!_showPath) return;
            if (!_path.IsCreated || _path.Length == 0) return;

            Gizmos.color = Color.red;
            NativeArray<Cell> grid = GridSystem.Instance.GetGrid();

            for (int i = 0; i < _path.Length - 1; i++)
            {
                Vector3 startPos = grid[_path[i]].position;
                Vector3 endPos = grid[_path[i + 1]].position;
                Gizmos.DrawLine(startPos, endPos);
            }
        }
        
        #endregion

        private void MoveInPath()
        {
            
        }

        public void RequestPath(Vector3 start, Vector3 end)
        {
            _path.Clear();

            _hasPath = AStarRequest.Instance.RequestPath(ref _path, start, end);
        }
    }
}
