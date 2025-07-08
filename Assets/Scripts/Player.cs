using NavGridSystem;
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
        // if (_agentNavigation.HasPath) return;
        
        var gridSystem = ServiceLocator.Instance.GetService<IGridSystem>();
        Cell myCell = gridSystem.GetCellWithWorldPosition(_transform.position);
        Cell target = gridSystem.GetCellWithWorldPosition(_followTarget.position);

        _agentNavigation.RequestPath(myCell, target);
    }
}