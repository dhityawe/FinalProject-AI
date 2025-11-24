using UnityEngine;

public class Steering : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The direction object that will rotate smoothly toward the target")]
    public GameObject directionObject;
    
    [Tooltip("Reference to the path manager")]
    public PathNode pathManager;
    
    [Tooltip("Front and rear tire GameObjects that will rotate when steering")]
    public GameObject[] tires;
    
    [Header("Movement Settings")]
    [Tooltip("Movement speed of the car")]
    [Range(1f, 50f)]
    public float movementSpeed = 10f;
    
    [Tooltip("How smoothly the car follows the direction object (lower = smoother)")]
    [Range(0.1f, 10f)]
    public float followSmoothing = 2f;
    
    [Header("Steering Settings")]
    [Tooltip("How quickly the direction object rotates toward target (degrees per second)")]
    [Range(10f, 360f)]
    public float rotationSpeed = 90f;
    
    [Tooltip("Distance to node before moving to next target")]
    [Range(1f, 10f)]
    public float waypointReachDistance = 3f;
    
    [Header("Tire Settings")]
    [Tooltip("Maximum tire turning angle")]
    [Range(0f, 90f)]
    public float maxTireAngle = 50f;
    
    [Tooltip("How quickly tires rotate to steering angle")]
    [Range(1f, 20f)]
    public float tireRotationSpeed = 10f;
    
    [Tooltip("Multiplier for tire rotation (2x means tires turn twice as much as car)")]
    [Range(1f, 50f)]
    public float tireAngleMultiplier = 20f;
    
    [Header("Debug")]
    [Tooltip("Enable debug logging for tire rotation")]
    public bool debugTireRotation = false;
    
    [Tooltip("Enable debug logging for path navigation")]
    public bool debugNavigation = false;
    
    [Header("Node Settings")]
    [Tooltip("Destroy car when reaching the end of the path")]
    public bool destroyAtEnd = true;
    
    // Private variables
    private int currentNodeIndex = 0;
    private Node currentTargetNode;
    private bool reachedEnd = false;
    private float currentTireAngle = 0f;
    private Quaternion previousRotation;

    void Start()
    {
        // Validate references
        if (directionObject == null)
        {
            Debug.LogError("Direction Object is not assigned on " + gameObject.name);
            enabled = false;
            return;
        }
        
        if (pathManager == null)
        {
            Debug.LogError("Path Manager is not assigned on " + gameObject.name);
            enabled = false;
            return;
        }
        
        // Find closest node to start
        if (pathManager.GetNodeCount() > 0)
        {
            currentNodeIndex = pathManager.GetClosestNodeIndex(transform.position);
            currentTargetNode = pathManager.GetNode(currentNodeIndex);
            
            if (debugNavigation)
            {
                Debug.Log($"[{gameObject.name}] Started at node {currentNodeIndex}. Path has {pathManager.GetNodeCount()} nodes. IsLooping: {pathManager.isLooping}. DestroyAtEnd: {destroyAtEnd}");
            }
        }
        else
        {
            Debug.LogError("Path Manager has no nodes!");
            enabled = false;
        }
        
        // Initialize previous rotation
        previousRotation = transform.rotation;
    }

    void Update()
    {
        if (reachedEnd || currentTargetNode == null)
            return;
        
        // Update target node if needed
        UpdateTargetNode();
        
        // Rotate direction object toward target
        RotateDirectionObject();
        
        // Move car following direction object
        MoveCarSmoothly();
        
        // Update tire rotation based on steering
        UpdateTireRotation();
    }
    
    private void UpdateTargetNode()
    {
        if (currentTargetNode.transform == null)
            return;
        
        // Check if we're close enough to the current node
        float distanceToNode = Vector3.Distance(transform.position, currentTargetNode.transform.position);
        
        if (distanceToNode < waypointReachDistance)
        {
            if (debugNavigation)
            {
                Debug.Log($"[{gameObject.name}] Reached node {currentNodeIndex}/{pathManager.GetNodeCount()-1}. Distance: {distanceToNode:F2}");
            }
            
            // Check if this is the last node and path is not looping
            if (!pathManager.isLooping && currentNodeIndex == pathManager.GetNodeCount() - 1)
            {
                // Reached the end of the path
                reachedEnd = true;
                
                if (debugNavigation)
                {
                    Debug.Log($"[{gameObject.name}] Reached LAST NODE (end of path)! CurrentIndex: {currentNodeIndex}, TotalNodes: {pathManager.GetNodeCount()}. DestroyAtEnd: {destroyAtEnd}");
                }
                
                if (destroyAtEnd)
                {
                    if (debugNavigation)
                    {
                        Debug.Log($"[{gameObject.name}] DESTROYING car now!");
                    }
                    Destroy(gameObject);
                    return;
                }
            }
            
            // Get next node
            Node nextNode = pathManager.GetNextNode(currentNodeIndex);
            
            if (nextNode != null)
            {
                currentNodeIndex = (currentNodeIndex + 1) % pathManager.GetNodeCount();
                currentTargetNode = nextNode;
                
                if (debugNavigation)
                {
                    Debug.Log($"[{gameObject.name}] Moving to next node {currentNodeIndex}");
                }
            }
            else
            {
                // This should not happen but keep as fallback
                reachedEnd = true;
                
                if (debugNavigation)
                {
                    Debug.Log($"[{gameObject.name}] NextNode is NULL (fallback). DestroyAtEnd: {destroyAtEnd}");
                }
                
                if (destroyAtEnd)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
    
    private void RotateDirectionObject()
    {
        if (currentTargetNode.transform == null)
            return;
        
        // Calculate direction to target
        Vector3 targetPosition = currentTargetNode.transform.position;
        Vector3 directionToTarget = (targetPosition - directionObject.transform.position).normalized;
        
        // Calculate target rotation
        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            
            // Smoothly rotate toward target
            directionObject.transform.rotation = Quaternion.RotateTowards(
                directionObject.transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
    
    private void MoveCarSmoothly()
    {
        // Get the forward direction from the direction object
        Vector3 moveDirection = directionObject.transform.forward;
        
        // Move the car forward
        transform.position += moveDirection * movementSpeed * Time.deltaTime;
        
        // Smoothly rotate the car to align with direction object
        Quaternion targetRotation = directionObject.transform.rotation;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            followSmoothing * Time.deltaTime
        );
    }
    
    private void UpdateTireRotation()
    {
        if (tires == null || tires.Length == 0)
        {
            if (debugTireRotation)
                Debug.LogWarning("No tires assigned!");
            return;
        }
        
        if (directionObject == null)
        {
            if (debugTireRotation)
                Debug.LogWarning("Direction object is null!");
            return;
        }
        
        // Calculate the angle difference between car body and direction object
        // This gives us the "steering input" - how much the car WANTS to turn
        float steeringAngle = Vector3.SignedAngle(
            transform.forward,
            directionObject.transform.forward,
            Vector3.up
        );
        
        if (debugTireRotation)
        {
            Debug.Log($"=== TIRE ROTATION DEBUG ===");
            Debug.Log($"Car Forward: {transform.forward}");
            Debug.Log($"Direction Forward: {directionObject.transform.forward}");
            Debug.Log($"Steering Angle (car vs direction): {steeringAngle:F2}");
        }
        
        // Apply multiplier and clamp to max tire angle range
        float targetTireAngle = steeringAngle * tireAngleMultiplier;
        targetTireAngle = Mathf.Clamp(targetTireAngle, -maxTireAngle, maxTireAngle);
        
        // Smoothly interpolate current tire angle to target
        currentTireAngle = Mathf.Lerp(
            currentTireAngle,
            targetTireAngle,
            tireRotationSpeed * Time.deltaTime
        );
        
        if (debugTireRotation)
        {
            Debug.Log($"Target Tire Angle (with {tireAngleMultiplier}x multiplier): {targetTireAngle:F2}");
            Debug.Log($"Current Tire Angle: {currentTireAngle:F2} | Max Tire Angle: {maxTireAngle}");
        }
        
        // Apply rotation to all tires
        int appliedCount = 0;
        foreach (GameObject tire in tires)
        {
            if (tire != null)
            {
                // Set the Y rotation (local rotation)
                tire.transform.localRotation = Quaternion.Euler(0, currentTireAngle, 0);
                appliedCount++;
                
                if (debugTireRotation)
                {
                    Debug.Log($"Tire '{tire.name}' rotated to Y: {currentTireAngle:F2} | Local Rotation: {tire.transform.localEulerAngles}");
                }
            }
        }
        
        if (debugTireRotation && appliedCount == 0)
        {
            Debug.LogWarning("No valid tire objects found in tires array!");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (directionObject != null && currentTargetNode != null && currentTargetNode.transform != null)
        {
            // Draw line from car to direction object
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, directionObject.transform.position);
            
            // Draw line from direction object to target node
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(directionObject.transform.position, currentTargetNode.transform.position);
            
            // Draw reach distance sphere
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(currentTargetNode.transform.position, waypointReachDistance);
        }
    }
}
