using NavGridSystem;
using UnityEngine;

[SelectionBase]
public class Player : MonoBehaviour
{
    [SerializeField] private Transform _endPosition;
    
    private AgentNavigation _agentNavigation;

    private void Awake()
    {
        _agentNavigation = GetComponent<AgentNavigation>();
        _agentNavigation.Speed = Random.Range(1f, 10f);
        _agentNavigation.RotationSpeed = Random.Range(1f, 10f);
    }

    private void Update()
    {
        if (_agentNavigation.HasPath) return;
        
        Cell myCell = GridSystem.Instance.GetCellWithWorldPosition(transform.position);
        Cell randomCell = GridSystem.Instance.GetRandomCell();
        _agentNavigation.RequestPath(myCell, randomCell);
    }
}