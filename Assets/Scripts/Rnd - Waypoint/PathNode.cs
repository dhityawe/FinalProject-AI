using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Node
{
    public Transform transform;
    
    [Tooltip("Width of the path at this node")]
    [Range(1f, 20f)]
    public float pathWidth = 5f;
    
    [Tooltip("Speed limit at this node (0 = use default speed)")]
    public float speedLimit = 0f;
    
    [Tooltip("Should the car slow down at this node?")]
    public bool isSlowDownZone = false;
    
    [Tooltip("Should the car stop at this node?")]
    public bool isStopPoint = false;
    
    [Tooltip("Wait time in seconds if this is a stop point")]
    public float stopDuration = 2f;

    public Node(Transform t)
    {
        transform = t;
    }
}

public class PathNode : MonoBehaviour
{
    [Header("Path Nodes")]
    [Tooltip("List of all nodes in the path sequence")]
    public List<Node> nodes = new List<Node>();
    
    [Header("Path Settings")]
    [Tooltip("Should the path loop back to the first node?")]
    public bool isLooping = true;
    
    [Tooltip("Default path width for all nodes")]
    [Range(1f, 20f)]
    public float defaultPathWidth = 5f;

    private void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0)
            return;
        
        // Draw all nodes and connections
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].transform == null)
                continue;
            
            // Draw the node as a sphere
            Gizmos.color = nodes[i].isStopPoint ? Color.red : 
                          (nodes[i].isSlowDownZone ? Color.yellow : Color.green);
            Gizmos.DrawSphere(nodes[i].transform.position, 0.5f);
            
            // Draw node number
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(nodes[i].transform.position + Vector3.up * 2f, 
                "Node " + i, 
                new GUIStyle() { normal = new GUIStyleState() { textColor = Color.white } });
            #endif
            
            // Draw connection to next node
            int nextIndex = (i + 1) % nodes.Count;
            if (nextIndex < nodes.Count && nodes[nextIndex].transform != null)
            {
                if (!isLooping && i == nodes.Count - 1)
                    continue;
                
                Vector3 currentPos = nodes[i].transform.position;
                Vector3 nextPos = nodes[nextIndex].transform.position;
                
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(currentPos, nextPos);
                
                // Draw arrow direction
                Vector3 direction = (nextPos - currentPos).normalized;
                Vector3 arrowPoint = currentPos + direction * (Vector3.Distance(currentPos, nextPos) * 0.5f);
                DrawArrow(arrowPoint, direction);
                
                // Draw path width indicator
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
                float width = nodes[i].pathWidth > 0 ? nodes[i].pathWidth : defaultPathWidth;
                
                Vector3 p1 = currentPos + perpendicular * width * 0.5f;
                Vector3 p2 = currentPos - perpendicular * width * 0.5f;
                Vector3 p3 = nextPos - perpendicular * width * 0.5f;
                Vector3 p4 = nextPos + perpendicular * width * 0.5f;
                
                Gizmos.DrawLine(p1, p4);
                Gizmos.DrawLine(p2, p3);
            }
        }
    }
    
    private void DrawArrow(Vector3 position, Vector3 direction, float arrowSize = 1f)
    {
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        
        Gizmos.DrawRay(position, right * arrowSize);
        Gizmos.DrawRay(position, left * arrowSize);
    }
    
    /// <summary>
    /// Get a specific node by index
    /// </summary>
    public Node GetNode(int index)
    {
        if (index >= 0 && index < nodes.Count)
        {
            return nodes[index];
        }
        return null;
    }
    
    /// <summary>
    /// Get the next node in the sequence
    /// </summary>
    public Node GetNextNode(int currentIndex)
    {
        if (nodes.Count == 0)
            return null;
        
        int nextIndex = (currentIndex + 1) % nodes.Count;
        
        if (!isLooping && nextIndex == 0 && currentIndex == nodes.Count - 1)
            return null;
        
        return nodes[nextIndex];
    }
    
    /// <summary>
    /// Get the closest node to a given position
    /// </summary>
    public int GetClosestNodeIndex(Vector3 position)
    {
        if (nodes.Count == 0)
            return -1;
        
        int closestIndex = 0;
        float closestDistance = float.MaxValue;
        
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].transform == null)
                continue;
            
            float distance = Vector3.Distance(position, nodes[i].transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
        
        return closestIndex;
    }
    
    /// <summary>
    /// Get the total number of nodes
    /// </summary>
    public int GetNodeCount()
    {
        return nodes.Count;
    }
    
    /// <summary>
    /// Get direction from one node to the next
    /// </summary>
    public Vector3 GetDirectionBetweenNodes(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= nodes.Count || toIndex < 0 || toIndex >= nodes.Count)
            return Vector3.forward;
        
        if (nodes[fromIndex].transform == null || nodes[toIndex].transform == null)
            return Vector3.forward;
        
        return (nodes[toIndex].transform.position - nodes[fromIndex].transform.position).normalized;
    }
}
