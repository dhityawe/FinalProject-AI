using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// Enum for Robber states
public enum RobberState
{
    Walk,
    Flee,
    Hide
}

[RequireComponent(typeof(NavMeshAgent))]
public class RobberFSM : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the path manager")]
    public PathNode pathManager;
    
    [Tooltip("Detection zone trigger (child object with trigger collider)")]
    public GameObject detectionZone;
    
    [Header("Detection Settings")]
    [Tooltip("Tag for police NPCs")]
    public string policeTag = "Police";
    
    [Tooltip("Layer mask for police NPCs (alternative to tag)")]
    public LayerMask policeLayer;
    
    [Tooltip("Layer mask for bushes to hide in")]
    public LayerMask bushLayer;
    
    [Tooltip("Maximum distance to search for bushes")]
    [Range(5f, 50f)]
    public float bushSearchRadius = 20f;
    
    [Header("Movement Settings")]
    [Tooltip("Walking speed")]
    [Range(1f, 10f)]
    public float walkSpeed = 3f;
    
    [Tooltip("Running speed when fleeing")]
    [Range(3f, 15f)]
    public float fleeSpeed = 6f;
    
    [Tooltip("Distance to waypoint to consider reached")]
    [Range(0.5f, 5f)]
    public float waypointReachDistance = 2f;
    
    [Tooltip("Distance to bush center to consider hidden")]
    [Range(0.5f, 3f)]
    public float hideReachDistance = 1f;
    
    [Header("Debug")]
    [Tooltip("Enable debug logging")]
    public bool debugMode = false;
    
    [Tooltip("Show debug gizmos")]
    public bool showDebugGizmos = true;
    
    // Private variables
    private NavMeshAgent agent;
    private RobberState currentState = RobberState.Walk;
    private int currentNodeIndex = 0;
    private Node currentTargetNode;
    private GameObject currentHidingBush = null;
    private List<GameObject> policeInRange = new List<GameObject>();

    void Start()
    {
        // Get NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"RobberFSM: No NavMeshAgent on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        // Validate references
        if (pathManager == null)
        {
            Debug.LogError($"RobberFSM: No PathManager assigned on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        if (detectionZone == null)
        {
            Debug.LogError($"RobberFSM: No DetectionZone assigned on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        // Add DetectionZoneTrigger component to detection zone
        DetectionZoneTrigger trigger = detectionZone.GetComponent<DetectionZoneTrigger>();
        if (trigger == null)
        {
            trigger = detectionZone.AddComponent<DetectionZoneTrigger>();
        }
        trigger.Initialize(this);
        
        // Ensure detection zone has a trigger collider
        Collider zoneCollider = detectionZone.GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
        {
            Debug.LogWarning($"RobberFSM: DetectionZone collider on {gameObject.name} is not a trigger! Setting it now.");
            zoneCollider.isTrigger = true;
        }
        
        // Initialize path
        if (pathManager.GetNodeCount() > 0)
        {
            currentNodeIndex = pathManager.GetClosestNodeIndex(transform.position);
            currentTargetNode = pathManager.GetNode(currentNodeIndex);
        }
        else
        {
            Debug.LogError("RobberFSM: PathManager has no nodes!");
            enabled = false;
            return;
        }
        
        // Start in Walk state
        ChangeState(RobberState.Walk);
        
        if (debugMode)
        {
            Debug.Log($"[{gameObject.name}] RobberFSM initialized. Starting at node {currentNodeIndex}");
        }
    }

    void Update()
    {
        if (!agent.isOnNavMesh)
            return;
        
        if (debugMode)
        {
            Debug.Log($"[{gameObject.name}] Update() State={currentState} NodeIndex={currentNodeIndex} Target={(currentTargetNode!=null?currentTargetNode.transform.name:"null")} AgentPending={agent.pathPending} Remaining={agent.remainingDistance}");
        }

        // Update current state
        switch (currentState)
        {
            case RobberState.Walk:
                UpdateWalkState();
                break;
            case RobberState.Flee:
                UpdateFleeState();
                break;
            case RobberState.Hide:
                UpdateHideState();
                break;
        }
    }
    
    #region State Updates
    
    private void UpdateWalkState()
    {
        if (debugMode) Debug.Log($"[{gameObject.name}] UpdateWalkState() PoliceCount={policeInRange.Count}");

        // Check if police detected
        if (policeInRange.Count > 0)
        {
            if (debugMode) Debug.Log($"[{gameObject.name}] Police detected while walking -> switch to Flee");
            ChangeState(RobberState.Flee);
            return;
        }
        
        // Continue walking along path
        if (currentTargetNode != null && currentTargetNode.transform != null)
        {
            float distanceToNode = Vector3.Distance(transform.position, currentTargetNode.transform.position);
            
            if (distanceToNode < waypointReachDistance)
            {
                if (debugMode) Debug.Log($"[{gameObject.name}] Reached node {currentNodeIndex}. Distance={distanceToNode:F2}");
                // Move to next node
                Node nextNode = pathManager.GetNextNode(currentNodeIndex);
                
                if (nextNode != null)
                {
                    currentNodeIndex = (currentNodeIndex + 1) % pathManager.GetNodeCount();
                    currentTargetNode = nextNode;
                    agent.SetDestination(currentTargetNode.transform.position);
                    
                    if (debugMode)
                    {
                        Debug.Log($"[{gameObject.name}] Moving to next node {currentNodeIndex} -> {currentTargetNode.transform.position}");
                    }
                }
                else if (!pathManager.isLooping)
                {
                    // Reached end of path
                    if (debugMode)
                    {
                        Debug.Log($"[{gameObject.name}] Reached end of path (no next node)");
                    }
                }
            }
        }
    }
    
    private void UpdateFleeState()
    {
        if (debugMode) Debug.Log($"[{gameObject.name}] UpdateFleeState() CurrentBush={(currentHidingBush!=null?currentHidingBush.name:"null")}");

        // Find nearest bush if we don't have one yet
        if (currentHidingBush == null)
        {
            if (debugMode) Debug.Log($"[{gameObject.name}] Searching for nearest bush within {bushSearchRadius}");
            currentHidingBush = FindNearestBush();
            
            if (currentHidingBush != null)
            {
                Vector3 bushCenter = currentHidingBush.transform.position;
                agent.SetDestination(bushCenter);
                
                if (debugMode)
                {
                    Debug.Log($"[{gameObject.name}] Fleeing to bush: {currentHidingBush.name} at {bushCenter}");
                }
            }
            else
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[{gameObject.name}] No bush found! Choosing fallback flee target.");
                }
                // Just run away from current position
                Vector3 fleeDirection = (transform.position - GetAveragePolicePosition()).normalized;
                Vector3 fleeTarget = transform.position + fleeDirection * 10f;
                agent.SetDestination(fleeTarget);
                if (debugMode) Debug.Log($"[{gameObject.name}] Flee target: {fleeTarget}");
            }
        }
        else
        {
            // Check if reached bush
            float distanceToBush = Vector3.Distance(transform.position, currentHidingBush.transform.position);
            
            if (debugMode) Debug.Log($"[{gameObject.name}] DistanceToBush={distanceToBush:F2} HideReach={hideReachDistance}");
            if (distanceToBush < hideReachDistance)
            {
                if (debugMode) Debug.Log($"[{gameObject.name}] Reached bush -> switching to Hide state");
                ChangeState(RobberState.Hide);
            }
        }
    }
    
    private void UpdateHideState()
    {
        // Stay hidden in the bush
        agent.isStopped = true;
        if (debugMode) Debug.Log($"[{gameObject.name}] UpdateHideState() PoliceCount={policeInRange.Count}");

        // Check if police left the area
        if (policeInRange.Count == 0)
        {
            if (debugMode)
            {
                Debug.Log($"[{gameObject.name}] Police left. Returning to walk state.");
            }
            
            currentHidingBush = null;
            ChangeState(RobberState.Walk);
        }
        else
        {
            if (debugMode)
            {
                Debug.Log($"[{gameObject.name}] Staying hidden. Police count: {policeInRange.Count}");
            }
        }
    }
    
    #endregion
    
    #region State Management
    
    private void ChangeState(RobberState newState)
    {
        if (currentState == newState)
            return;
        
        if (debugMode)
        {
            Debug.Log($"[{gameObject.name}] State change: {currentState} -> {newState}");
        }
        
        // Exit current state
        ExitState(currentState);
        
        // Enter new state
        currentState = newState;
        EnterState(newState);
    }
    
    private void EnterState(RobberState state)
    {
        switch (state)
        {
            case RobberState.Walk:
                agent.isStopped = false;
                agent.speed = walkSpeed;
                if (currentTargetNode != null && currentTargetNode.transform != null)
                {
                    agent.SetDestination(currentTargetNode.transform.position);
                }
                break;
                
            case RobberState.Flee:
                agent.isStopped = false;
                agent.speed = fleeSpeed;
                currentHidingBush = null;
                break;
                
            case RobberState.Hide:
                agent.isStopped = true;
                break;
        }
    }
    
    private void ExitState(RobberState state)
    {
        // Cleanup when exiting state
    }
    
    #endregion
    
    #region Helper Methods
    
    private GameObject FindNearestBush()
    {
        Collider[] bushColliders = Physics.OverlapSphere(transform.position, bushSearchRadius, bushLayer);
        
        if (bushColliders.Length == 0)
            return null;
        
        GameObject nearestBush = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Collider bushCollider in bushColliders)
        {
            float distance = Vector3.Distance(transform.position, bushCollider.transform.position);
            
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestBush = bushCollider.gameObject;
            }
        }
        
        return nearestBush;
    }
    
    private Vector3 GetAveragePolicePosition()
    {
        if (policeInRange.Count == 0)
            return transform.position;
        
        Vector3 sum = Vector3.zero;
        int validCount = 0;
        
        foreach (GameObject police in policeInRange)
        {
            if (police != null)
            {
                sum += police.transform.position;
                validCount++;
            }
        }
        
        return validCount > 0 ? sum / validCount : transform.position;
    }
    
    /// <summary>
    /// Check if robber is currently hidden
    /// </summary>
    public bool IsHidden()
    {
        return currentState == RobberState.Hide;
    }
    
    /// <summary>
    /// Get current state
    /// </summary>
    public RobberState GetCurrentState()
    {
        return currentState;
    }
    
    #endregion
    
    #region Trigger Detection
    
    public void OnPoliceEnter(GameObject police)
    {
        if (!policeInRange.Contains(police))
        {
            policeInRange.Add(police);
            
            if (debugMode)
            {
                Debug.Log($"[{gameObject.name}] Police detected: {police.name}. Total police: {policeInRange.Count}");
            }
            
            // If not hiding, start fleeing
            if (currentState == RobberState.Walk)
            {
                ChangeState(RobberState.Flee);
            }
        }
    }
    
    public void OnPoliceExit(GameObject police)
    {
        if (policeInRange.Contains(police))
        {
            policeInRange.Remove(police);
            
            if (debugMode)
            {
                Debug.Log($"[{gameObject.name}] Police left: {police.name}. Remaining police: {policeInRange.Count}");
            }
        }
    }
    
    #endregion
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos)
            return;
        
        // Draw detection zone
        if (detectionZone != null)
        {
            Collider zoneCollider = detectionZone.GetComponent<Collider>();
            if (zoneCollider != null)
            {
                Gizmos.color = new Color(1, 0, 0, 0.2f);
                if (zoneCollider is SphereCollider)
                {
                    SphereCollider sphere = zoneCollider as SphereCollider;
                    Gizmos.DrawWireSphere(detectionZone.transform.position + sphere.center, sphere.radius);
                }
            }
        }
        
        // Draw bush search radius
        Gizmos.color = new Color(0, 1, 0, 0.1f);
        Gizmos.DrawWireSphere(transform.position, bushSearchRadius);
        
        // Draw line to current target
        if (currentTargetNode != null && currentTargetNode.transform != null && currentState == RobberState.Walk)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, currentTargetNode.transform.position);
        }
        
        // Draw line to hiding bush
        if (currentHidingBush != null && (currentState == RobberState.Flee || currentState == RobberState.Hide))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentHidingBush.transform.position);
            Gizmos.DrawSphere(currentHidingBush.transform.position, 0.5f);
        }
        
        // Draw state indicator
        if (Application.isPlaying)
        {
            Gizmos.color = GetStateColor();
            Gizmos.DrawSphere(transform.position + Vector3.up * 3f, 0.3f);
        }
    }
    
    private Color GetStateColor()
    {
        switch (currentState)
        {
            case RobberState.Walk:
                return Color.blue;
            case RobberState.Flee:
                return Color.yellow;
            case RobberState.Hide:
                return Color.green;
            default:
                return Color.white;
        }
    }
}
