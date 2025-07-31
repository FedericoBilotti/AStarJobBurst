using Agents;
using NavigationGraph;
using Pathfinding.RequesterStrategy;
using UnityEngine;

namespace Pathfinding
{
    public class PathRequester : MonoBehaviour, IPathfinding
    {
        [SerializeField] private PathRequestType _requestType;
        private IPathRequest _aStar;
        private IPathRequest _thetaStar;

        private void Awake()
        {
            ServiceLocator.Instance.RegisterService<IPathfinding>(this);

            // Should be injected
            var navigationGraph = ServiceLocator.Instance.GetService<INavigationGraph>();
            _aStar = new AStarRequester(navigationGraph);
            _thetaStar = new ThetaStarRequester(navigationGraph);
        }

        public bool RequestPath(IAgent agent, Cell start, Cell end)
        {
            switch (_requestType)
            {
                case PathRequestType.ThetaStar: 
                    return _thetaStar.RequestPath(agent, start, end);

                case PathRequestType.AStar:
                default:
                    return _aStar.RequestPath(agent, start, end);
            }
        }

        private void LateUpdate()
        {
            switch (_requestType)
            {
                case PathRequestType.ThetaStar:
                    _thetaStar.FinishPath();
                    break;

                case PathRequestType.AStar:
                default:
                    _aStar.FinishPath();
                    break;
            }
        }

        private enum PathRequestType
        {
            None,
            AStar,
            ThetaStar
        }

        private void OnDestroy()
        {
            _thetaStar.Clear();
            _aStar.Clear();
        }
    }
}