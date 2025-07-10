using NavigationGraph;
using UnityEngine;

namespace Pathfinding
{
    public class AStarRequest : MonoBehaviour, IPathfinding
    {
        [SerializeField] private PathRequestType _requestType;
        private IPathRequest _singlePathRequest;
        private IPathRequest _multiPathRequest;

        private void Awake()
        {
            // Should be injected
            _singlePathRequest = new OnePathRequester(GetComponent<INavigationGraph>());
            _multiPathRequest = new MultiplePathRequester(GetComponent<INavigationGraph>());
        }

        private void Start() => ServiceLocator.Instance.RegisterService<IPathfinding>(this);

        public void RequestPath(IAgent agent, Cell start, Cell end)
        {
            if (_requestType == PathRequestType.Single)
            {
                _singlePathRequest.RequestPath(agent, start, end);
            }
            else
            {
                _multiPathRequest.RequestPath(agent, start, end);
            }
        }

        private void LateUpdate()
        {
            if (_requestType == PathRequestType.Single)
            {
                _singlePathRequest.LaunchPath();
            }
            else
            {
                _multiPathRequest.LaunchPath();
            }
        }

        private enum PathRequestType
        {
            Single,
            Multiple
        }

        private void OnDestroy()
        {
            _singlePathRequest.Clear();
            _multiPathRequest.Clear();
        }
    }
}