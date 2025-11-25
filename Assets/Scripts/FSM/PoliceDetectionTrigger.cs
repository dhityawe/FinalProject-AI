using UnityEngine;

/// <summary>
/// Detection trigger helper for PoliceFSM.
/// Automatically added to the police detection zone GameObject (child).
/// </summary>
public class PoliceDetectionTrigger : MonoBehaviour
{
    private PoliceFSM policeFSM;
    private string robberTag;
    private LayerMask robberLayer;

    public void Initialize(PoliceFSM police)
    {
        policeFSM = police;
        robberTag = police.robberTag;
        robberLayer = police.robberLayer;

        if (police.debugMode)
            Debug.Log($"PoliceDetectionTrigger initialized on {gameObject.name}. RobberTag='{robberTag}', RobberLayer={robberLayer.value}");
    }

    private void Awake()
    {
        // ensure collider and rigidbody exist like robber detection
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
        }
        else if (!col.isTrigger)
        {
            col.isTrigger = true;
        }

        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (policeFSM == null) return;
        if (policeFSM.debugMode)
            Debug.Log($"[PoliceDetection] Trigger Enter: {other.gameObject.name}, Tag: '{other.tag}', Layer: {other.gameObject.layer}");

        if (other.CompareTag(robberTag))
        {
            // ignore if robber is currently hidden
            var robberComp = other.GetComponent<RobberFSM>();
            if (robberComp != null && robberComp.IsHidden())
            {
                if (policeFSM.debugMode) Debug.Log($"[PoliceDetection] Ignoring hidden robber (tag) {other.gameObject.name}");
                return;
            }
            if (policeFSM.debugMode) Debug.Log($"[PoliceDetection] Robber detected by TAG: {other.gameObject.name}");
            policeFSM.OnRobberEnter(other.gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & robberLayer) != 0)
        {
            // ignore if robber is currently hidden
            var robberComp2 = other.GetComponent<RobberFSM>();
            if (robberComp2 != null && robberComp2.IsHidden())
            {
                if (policeFSM.debugMode) Debug.Log($"[PoliceDetection] Ignoring hidden robber (layer) {other.gameObject.name}");
                return;
            }
            if (policeFSM.debugMode) Debug.Log($"[PoliceDetection] Robber detected by LAYER: {other.gameObject.name}");
            policeFSM.OnRobberEnter(other.gameObject);
            return;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (policeFSM == null) return;
        if (policeFSM.debugMode)
            Debug.Log($"[PoliceDetection] Trigger Exit: {other.gameObject.name}");

        if (other.CompareTag(robberTag))
        {
            policeFSM.OnRobberExit(other.gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & robberLayer) != 0)
        {
            policeFSM.OnRobberExit(other.gameObject);
            return;
        }
    }
}
