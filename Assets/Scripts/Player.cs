using System;
using NavGridSystem;
using UnityEngine;

[SelectionBase]
public class Player : MonoBehaviour
{
    [SerializeField] private Transform _endPosition;
    
    private AgentNavigation _agentNavigation;

    private void Awake() => _agentNavigation = GetComponent<AgentNavigation>();

    private void Update()
    {
        _agentNavigation.RequestPath(transform.position, _endPosition.position);
    }
}