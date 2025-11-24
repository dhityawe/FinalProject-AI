using UnityEngine;

/// <summary>
/// Helper component for RobberFSM to detect police in trigger zone
/// Automatically added to detection zone GameObject
/// </summary>
public class DetectionZoneTrigger : MonoBehaviour
{
    private RobberFSM robberFSM;
    private string policeTag;
    private LayerMask policeLayer;
    
    public void Initialize(RobberFSM robber)
    {
        robberFSM = robber;
        policeTag = robber.policeTag;
        policeLayer = robber.policeLayer;
        
        if (robber.debugMode)
        {
            Debug.Log($"DetectionZoneTrigger initialized on {gameObject.name}. Police tag: '{policeTag}', Police layer: {policeLayer.value}");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (robberFSM == null)
            return;
        
        if (robberFSM.debugMode)
        {
            Debug.Log($"[DetectionZone] Trigger Enter: {other.gameObject.name}, Tag: '{other.tag}', Layer: {other.gameObject.layer}");
        }
        
        // Check by tag
        if (other.CompareTag(policeTag))
        {
            if (robberFSM.debugMode)
            {
                Debug.Log($"[DetectionZone] Police detected by TAG: {other.gameObject.name}");
            }
            robberFSM.OnPoliceEnter(other.gameObject);
            return;
        }
        
        // Check by layer
        if (((1 << other.gameObject.layer) & policeLayer) != 0)
        {
            if (robberFSM.debugMode)
            {
                Debug.Log($"[DetectionZone] Police detected by LAYER: {other.gameObject.name}");
            }
            robberFSM.OnPoliceEnter(other.gameObject);
            return;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (robberFSM == null)
            return;
        
        if (robberFSM.debugMode)
        {
            Debug.Log($"[DetectionZone] Trigger Exit: {other.gameObject.name}");
        }
        
        // Check by tag
        if (other.CompareTag(policeTag))
        {
            robberFSM.OnPoliceExit(other.gameObject);
            return;
        }
        
        // Check by layer
        if (((1 << other.gameObject.layer) & policeLayer) != 0)
        {
            robberFSM.OnPoliceExit(other.gameObject);
            return;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (robberFSM == null)
            return;
        
        if (robberFSM.debugMode)
        {
            Debug.Log($"[DetectionZone] Collision Enter: {collision.gameObject.name}, Tag: '{collision.gameObject.tag}', Layer: {collision.gameObject.layer}");
        }
        
        // Check by tag
        if (collision.gameObject.CompareTag(policeTag))
        {
            if (robberFSM.debugMode)
            {
                Debug.Log($"[DetectionZone] Police detected by TAG (collision): {collision.gameObject.name}");
            }
            robberFSM.OnPoliceEnter(collision.gameObject);
            return;
        }
        
        // Check by layer
        if (((1 << collision.gameObject.layer) & policeLayer) != 0)
        {
            if (robberFSM.debugMode)
            {
                Debug.Log($"[DetectionZone] Police detected by LAYER (collision): {collision.gameObject.name}");
            }
            robberFSM.OnPoliceEnter(collision.gameObject);
            return;
        }
    }
    
    private void OnCollisionExit(Collision collision)
    {
        if (robberFSM == null)
            return;
        
        if (robberFSM.debugMode)
        {
            Debug.Log($"[DetectionZone] Collision Exit: {collision.gameObject.name}");
        }
        
        // Check by tag
        if (collision.gameObject.CompareTag(policeTag))
        {
            robberFSM.OnPoliceExit(collision.gameObject);
            return;
        }
        
        // Check by layer
        if (((1 << collision.gameObject.layer) & policeLayer) != 0)
        {
            robberFSM.OnPoliceExit(collision.gameObject);
            return;
        }
    }

    private void Awake()
    {
        // Ensure the detection zone has a collider and a kinematic Rigidbody so triggers/collisions fire reliably
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            if (robberFSM != null && robberFSM.debugMode)
                Debug.Log($"[DetectionZoneTrigger] Added SphereCollider to {gameObject.name}");
        }
        else
        {
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                if (robberFSM != null && robberFSM.debugMode)
                    Debug.Log($"[DetectionZoneTrigger] Set collider.isTrigger=true on {gameObject.name}");
            }
        }

        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            if (robberFSM != null && robberFSM.debugMode)
                Debug.Log($"[DetectionZoneTrigger] Added kinematic Rigidbody to {gameObject.name}");
        }
    }
}
