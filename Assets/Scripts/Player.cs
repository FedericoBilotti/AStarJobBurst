using NavigationGraph;
using UnityEngine;
using Random = UnityEngine.Random;

[SelectionBase]
public class Player : MonoBehaviour
{
    private Transform _transform;
    [SerializeField] private Transform _followTarget;
    
    private AgentNavigation _agentNavigation;

    private void Awake()
    {
        _agentNavigation = GetComponent<AgentNavigation>();
        _agentNavigation.Speed = Random.Range(1f, 10f);
        _agentNavigation.RotationSpeed = Random.Range(1f, 10f);
        _transform = transform;
    }

    private void Update()
    {
        if (_agentNavigation.HasPath) return;
        
        Vector3 myPos = _transform.position;
        Vector3 targetPos = _followTarget.position;
        
        var gridSystem = ServiceLocator.Instance.GetService<INavigationGraph>();
        Vector3 target = gridSystem.GetRandomCell().position;

        _agentNavigation.RequestPath(myPos, target);
    }
}