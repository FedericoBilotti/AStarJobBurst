using Agents;
using NavigationGraph;
using UnityEngine;

[SelectionBase]
public class Player : MonoBehaviour
{
    private Transform _transform;
    [SerializeField] private Transform _followTarget;
    
    private IAgent _agentNavigation;

    private void Awake()
    {
        _agentNavigation = GetComponent<IAgent>();
        _transform = transform;
    }

    private void Update()
    {
        // if (_agentNavigation.HasPath) return;
        
        Vector3 myPos = _transform.position;
        Vector3 target = _followTarget.position;
        
        // var gridSystem = ServiceLocator.Instance.GetService<INavigationGraph>();
        //var target = GetRandomTarget(gridSystem);

        _agentNavigation.RequestPath(myPos, target);
    }

    private Cell GetRandomTarget(INavigationGraph graph)
    {
        Cell target = graph.GetRandomCell();

        return target.isWalkable ? target : GetRandomTarget(graph);
    }
}