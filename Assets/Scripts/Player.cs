using NavGridSystem;
using UnityEngine;
using Random = UnityEngine.Random;

[SelectionBase]
public class Player : MonoBehaviour
{
    private Transform _transform;
    
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
                
        var gridSystem = ServiceLocator.Instance.GetService<IGridSystem>();
        Cell myCell = gridSystem.GetCellWithWorldPosition(_transform.position);
        Cell randomCell = gridSystem.GetRandomCell();
        
        _agentNavigation.RequestPath(myCell, randomCell);
    }
}