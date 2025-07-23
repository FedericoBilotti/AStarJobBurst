using NavigationGraph;
using Pathfinding.RequesterStrategy;
using Pathfinding.Strategy;
using UnityEngine;

namespace Pathfinding
{
    public class AStarRequest : MonoBehaviour, IPathfinding
    {
        [SerializeField] private PathRequestType _requestType;
        private IPathRequest _singlePathRequest;
        private IPathRequest _multiPathRequest;
        private IPathRequest _schedulePathRequest;

        private void Awake()
        {
            ServiceLocator.Instance.RegisterService<IPathfinding>(this);
            
            // Should be injected
            var navigationGraph = GetComponent<INavigationGraph>();
            _singlePathRequest = new OnePathRequester(navigationGraph);
            _multiPathRequest = new MultiplePathRequester(navigationGraph);
            _schedulePathRequest = new SchedulePathRequest(navigationGraph);
        }

        public bool RequestPath(IAgent agent, Cell start, Cell end)
        {
            switch (_requestType)
            {
                case PathRequestType.Single:
                    return _singlePathRequest.RequestPath(agent, start, end);
                    break;

                case PathRequestType.Multiple:
                    return _multiPathRequest.RequestPath(agent, start, end);
                    break;

                case PathRequestType.Schedule:
                default:
                    return _schedulePathRequest.RequestPath(agent, start, end);
                    break;
            }
        }

        private void LateUpdate()
        {
            switch (_requestType)
            {
                case PathRequestType.Single:
                    _singlePathRequest.FinishPath();
                    break;

                case PathRequestType.Multiple:
                    _multiPathRequest.FinishPath();
                    break;

                case PathRequestType.Schedule:
                default:
                    _schedulePathRequest.FinishPath();
                    break;
            }
        }

        private enum PathRequestType
        {
            Single,
            Multiple,
            Schedule
        }

        private void OnDestroy()
        {
            _singlePathRequest.Clear();
            _multiPathRequest.Clear();
            _schedulePathRequest.Clear();
        }
    }
}