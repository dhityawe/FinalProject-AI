using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshAnimal : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Minimum distance for random movement")]
    [Range(5f, 50f)]
    public float minWanderDistance = 10f;
    
    [Tooltip("Maximum distance for random movement")]
    [Range(10f, 100f)]
    public float maxWanderDistance = 30f;
    
    [Tooltip("Time to wait at destination before moving again")]
    [Range(0f, 10f)]
    public float waitTimeAtDestination = 2f;
    
    [Tooltip("Distance to destination to be considered 'arrived'")]
    [Range(0.1f, 5f)]
    public float stoppingDistance = 1f;
    
    [Header("Collision Avoidance")]
    [Tooltip("Enable obstacle detection and avoidance")]
    public bool enableObstacleDetection = true;
    
    [Tooltip("Distance to check for obstacles ahead")]
    [Range(1f, 10f)]
    public float obstacleDetectionRange = 3f;
    
    [Tooltip("Layers to detect as obstacles")]
    public LayerMask obstacleLayer = -1;
    
    [Tooltip("Time to wait before finding new path if stuck")]
    [Range(1f, 10f)]
    public float stuckCheckTime = 3f;
    
    [Header("Animation (Optional)")]
    [Tooltip("Animator component reference (optional)")]
    public Animator animator;
    
    [Tooltip("Name of the speed parameter in animator")]
    public string speedParameterName = "Speed";
    
    [Header("Debug")]
    [Tooltip("Enable debug logging")]
    public bool debugMode = false;
    
    [Tooltip("Show debug gizmos in scene view")]
    public bool showDebugGizmos = true;
    
    // Private variables
    private NavMeshAgent agent;
    private Vector3 currentDestination;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private bool hasDestination = false;

    void Start()
    {
        // Get NavMeshAgent component
        agent = GetComponent<NavMeshAgent>();
        
        if (agent == null)
        {
            Debug.LogError($"NavMeshAnimal: No NavMeshAgent component found on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        // Set stopping distance
        agent.stoppingDistance = stoppingDistance;
        
        // Get animator if not assigned
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        // Initialize
        lastPosition = transform.position;
        
        // Start moving immediately
        FindNewDestination();
        
        if (debugMode)
        {
            Debug.Log($"[{gameObject.name}] NavMeshAnimal initialized. Agent speed: {agent.speed}");
        }
    }

    void Update()
    {
        // Check if agent is on NavMesh
        if (!agent.isOnNavMesh)
        {
            if (debugMode)
            {
                Debug.LogWarning($"[{gameObject.name}] Not on NavMesh!");
            }
            return;
        }
        
        // Update animator if available
        UpdateAnimation();
        
        // Check for obstacles ahead
        if (enableObstacleDetection && !isWaiting)
        {
            CheckForObstacles();
        }
        
        // Check if stuck
        CheckIfStuck();
        
        // Handle waiting at destination
        if (isWaiting)
        {
            waitTimer += Time.deltaTime;
            
            if (waitTimer >= waitTimeAtDestination)
            {
                isWaiting = false;
                waitTimer = 0f;
                FindNewDestination();
            }
        }
        // Check if reached destination
        else if (hasDestination && !agent.pathPending)
        {
            float distanceToDestination = Vector3.Distance(transform.position, currentDestination);
            
            if (distanceToDestination <= stoppingDistance || agent.remainingDistance <= agent.stoppingDistance)
            {
                if (debugMode)
                {
                    Debug.Log($"[{gameObject.name}] Reached destination. Waiting for {waitTimeAtDestination}s");
                }
                
                isWaiting = true;
                agent.isStopped = true;
            }
        }
    }
    
    private void FindNewDestination()
    {
        Vector3 randomDestination = GetRandomPointOnNavMesh();
        
        if (randomDestination != Vector3.zero)
        {
            agent.isStopped = false;
            agent.SetDestination(randomDestination);
            currentDestination = randomDestination;
            hasDestination = true;
            lastPosition = transform.position;
            stuckTimer = 0f;
            
            if (debugMode)
            {
                Debug.Log($"[{gameObject.name}] New destination set: {randomDestination}, Distance: {Vector3.Distance(transform.position, randomDestination):F2}");
            }
        }
        else
        {
            if (debugMode)
            {
                Debug.LogWarning($"[{gameObject.name}] Could not find valid destination on NavMesh!");
            }
        }
    }
    
    private Vector3 GetRandomPointOnNavMesh()
    {
        // Generate random direction
        Vector3 randomDirection = Random.insideUnitSphere * Random.Range(minWanderDistance, maxWanderDistance);
        randomDirection += transform.position;
        
        // Try to find a point on NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, maxWanderDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        // If failed, try multiple times
        for (int i = 0; i < 5; i++)
        {
            randomDirection = Random.insideUnitSphere * Random.Range(minWanderDistance, maxWanderDistance);
            randomDirection += transform.position;
            
            if (NavMesh.SamplePosition(randomDirection, out hit, maxWanderDistance, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        
        return Vector3.zero;
    }
    
    private void CheckForObstacles()
    {
        // Raycast forward to detect obstacles
        Vector3 forward = transform.forward;
        RaycastHit hit;
        
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, forward, out hit, obstacleDetectionRange, obstacleLayer))
        {
            if (debugMode)
            {
                Debug.Log($"[{gameObject.name}] Obstacle detected ahead: {hit.collider.gameObject.name}. Finding new path.");
            }
            
            // Find new destination to avoid obstacle
            FindNewDestination();
        }
    }
    
    private void CheckIfStuck()
    {
        // Check if the agent hasn't moved much
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        if (distanceMoved < 0.1f && !isWaiting && hasDestination)
        {
            stuckTimer += Time.deltaTime;
            
            if (stuckTimer >= stuckCheckTime)
            {
                if (debugMode)
                {
                    Debug.Log($"[{gameObject.name}] Stuck detected! Finding new destination.");
                }
                
                FindNewDestination();
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }
    }
    
    private void UpdateAnimation()
    {
        if (animator != null && !string.IsNullOrEmpty(speedParameterName))
        {
            // Set speed parameter based on agent velocity
            float speed = agent.velocity.magnitude;
            animator.SetFloat(speedParameterName, speed);
        }
    }
    
    /// <summary>
    /// Manually set a specific destination
    /// </summary>
    public void SetDestination(Vector3 destination)
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
            currentDestination = destination;
            hasDestination = true;
            isWaiting = false;
            waitTimer = 0f;
            
            if (debugMode)
            {
                Debug.Log($"[{gameObject.name}] Manual destination set: {destination}");
            }
        }
    }
    
    /// <summary>
    /// Stop the animal movement
    /// </summary>
    public void StopMovement()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            hasDestination = false;
            
            if (debugMode)
            {
                Debug.Log($"[{gameObject.name}] Movement stopped.");
            }
        }
    }
    
    /// <summary>
    /// Resume random wandering
    /// </summary>
    public void ResumeMovement()
    {
        FindNewDestination();
        
        if (debugMode)
        {
            Debug.Log($"[{gameObject.name}] Movement resumed.");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || agent == null)
            return;
        
        // Draw current destination
        if (hasDestination)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentDestination, 0.5f);
            Gizmos.DrawLine(transform.position, currentDestination);
        }
        
        // Draw obstacle detection range
        if (enableObstacleDetection)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * obstacleDetectionRange);
        }
        
        // Draw wander range
        Gizmos.color = new Color(0, 1, 0, 0.1f);
        Gizmos.DrawWireSphere(transform.position, minWanderDistance);
        Gizmos.color = new Color(0, 1, 0, 0.05f);
        Gizmos.DrawWireSphere(transform.position, maxWanderDistance);
    }
}
