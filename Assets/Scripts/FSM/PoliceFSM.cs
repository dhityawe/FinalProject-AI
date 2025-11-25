using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum PoliceState
{
    Patrol,
    Chase,
    Search
}

[RequireComponent(typeof(NavMeshAgent))]
public class PoliceFSM : MonoBehaviour
{
    [Header("References")]
    public PathNode pathManager;

    [Tooltip("Detection zone gameobject (child with collider)")]
    public GameObject detectionZone;

    [Header("Detection Settings")]
    public string robberTag = "Robber";
    public LayerMask robberLayer;

    [Header("Movement")]
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 6f;
    public float searchWaitTime = 3f;

    [Header("Behavior")]
    [Tooltip("How close to node before moving to next")]
    public float waypointReachDistance = 1.5f;

    [Tooltip("Ignore a robber for this many seconds after they leave detection")]
    public float ignoreRobberDuration = 2f;

    [Header("Debug")]
    public bool debugMode = false;

    // private
    private NavMeshAgent agent;
    private PoliceState currentState = PoliceState.Patrol;
    private int currentNodeIndex = 0;
    private Node currentTargetNode;

    private GameObject currentRobber = null;
    private Vector3 lastKnownRobberPosition;
    private float searchTimer = 0f;

    // track per-robber ignore timestamps (Time.time until which we ignore re-detection)
    private System.Collections.Generic.Dictionary<GameObject, float> robberIgnoreTimestamps = new System.Collections.Generic.Dictionary<GameObject, float>();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"PoliceFSM: No NavMeshAgent on {gameObject.name}!");
            enabled = false; return;
        }

        if (pathManager == null)
        {
            Debug.LogError($"PoliceFSM: No PathNode assigned on {gameObject.name}!");
            enabled = false; return;
        }

        if (pathManager.GetNodeCount() == 0)
        {
            Debug.LogError($"PoliceFSM: PathManager has no nodes!");
            enabled = false; return;
        }

        // init path
        currentNodeIndex = pathManager.GetClosestNodeIndex(transform.position);
        currentTargetNode = pathManager.GetNode(currentNodeIndex);

        // detection zone init
        if (detectionZone == null)
        {
            Debug.LogWarning($"PoliceFSM: detectionZone not assigned for {gameObject.name}. Trying to find child named 'DetectionZone'.");
            Transform t = transform.Find("DetectionZone");
            if (t != null) detectionZone = t.gameObject;
        }

        if (detectionZone != null)
        {
            PoliceDetectionTrigger trigger = detectionZone.GetComponent<PoliceDetectionTrigger>();
            if (trigger == null) trigger = detectionZone.AddComponent<PoliceDetectionTrigger>();
            trigger.Initialize(this);
        }
        else
        {
            Debug.LogWarning($"PoliceFSM: detectionZone missing on {gameObject.name}. Detection will not work until assigned.");
        }

        EnterState(PoliceState.Patrol);
        if (debugMode) Debug.Log($"[{gameObject.name}] PoliceFSM started. State={currentState} Node={currentNodeIndex}");
    }

    void Update()
    {
        if (!agent.isOnNavMesh) return;

        if (debugMode)
            Debug.Log($"[{gameObject.name}] Update State={currentState} Node={currentNodeIndex} Target={(currentTargetNode!=null?currentTargetNode.transform.name:"null")} Remaining={agent.remainingDistance}");

        switch (currentState)
        {
            case PoliceState.Patrol: UpdatePatrol(); break;
            case PoliceState.Chase: UpdateChase(); break;
            case PoliceState.Search: UpdateSearch(); break;
        }
    }

    #region States
    private void UpdatePatrol()
    {
        if (currentTargetNode == null) return;

        float dist = Vector3.Distance(transform.position, currentTargetNode.transform.position);
        if (debugMode) Debug.Log($"[{gameObject.name}] Patrol distToNode={dist:F2}");

        if (dist < waypointReachDistance)
        {
            // next
            Node next = pathManager.GetNextNode(currentNodeIndex);
            if (next != null)
            {
                currentNodeIndex = (currentNodeIndex + 1) % pathManager.GetNodeCount();
                currentTargetNode = next;
                agent.SetDestination(currentTargetNode.transform.position);
                if (debugMode) Debug.Log($"[{gameObject.name}] Patrol -> next node {currentNodeIndex}");
            }
        }
    }

    private void UpdateChase()
    {
        if (currentRobber == null)
        {
            // lost target -> switch to search
            if (debugMode) Debug.Log($"[{gameObject.name}] Chase: robber null -> switching to Search");
            ChangeState(PoliceState.Search);
            return;
        }

        // follow robber
        agent.SetDestination(currentRobber.transform.position);
        lastKnownRobberPosition = currentRobber.transform.position;

        // If robber hidden flag available, check it (RobberFSM has IsHidden())
        RobberFSM r = currentRobber.GetComponent<RobberFSM>();
        if (r != null && r.IsHidden())
        {
            if (debugMode) Debug.Log($"[{gameObject.name}] Robber went hidden -> abandoning chase and returning to Patrol");
            // clear current target and return to patrol
            currentRobber = null;
            ChangeState(PoliceState.Patrol);
            return;
        }
    }

    private void UpdateSearch()
    {
        // go to last known
        if (agent.remainingDistance < 0.5f && !agent.pathPending)
        {
            searchTimer += Time.deltaTime;
            if (debugMode) Debug.Log($"[{gameObject.name}] Searching... timer={searchTimer:F2}");

            if (searchTimer >= searchWaitTime)
            {
                // give up and return to patrol
                if (debugMode) Debug.Log($"[{gameObject.name}] Search timeout -> returning to Patrol");
                searchTimer = 0f;
                ChangeState(PoliceState.Patrol);
            }
        }
    }
    #endregion

    #region State changes
    private void ChangeState(PoliceState newState)
    {
        if (currentState == newState) return;
        if (debugMode) Debug.Log($"[{gameObject.name}] State {currentState} -> {newState}");
        ExitState(currentState);
        currentState = newState;
        EnterState(newState);
    }

    private void EnterState(PoliceState s)
    {
        switch (s)
        {
            case PoliceState.Patrol:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                if (currentTargetNode != null) agent.SetDestination(currentTargetNode.transform.position);
                break;
            case PoliceState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                break;
            case PoliceState.Search:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                // move to last known
                agent.SetDestination(lastKnownRobberPosition);
                searchTimer = 0f;
                break;
        }
    }

    private void ExitState(PoliceState s)
    {
        // nothing special
    }
    #endregion

    #region Detection callbacks
    public void OnRobberEnter(GameObject robber)
    {
        if (robber == null) return;
            // If this robber was recently exited, ignore re-entry until the timer expires
            if (robberIgnoreTimestamps != null && robberIgnoreTimestamps.TryGetValue(robber, out float ignoreUntil) && Time.time < ignoreUntil)
            {
                if (debugMode) Debug.Log($"[{gameObject.name}] Ignoring robber enter for {robber.name} for another {(ignoreUntil - Time.time):F2}s");
                return;
            }
        if (debugMode) Debug.Log($"[{gameObject.name}] OnRobberEnter: {robber.name}");

        currentRobber = robber;
        lastKnownRobberPosition = robber.transform.position;
        ChangeState(PoliceState.Chase);
    }

    public void OnRobberExit(GameObject robber)
    {
        if (robber == null) return;
        if (debugMode) Debug.Log($"[{gameObject.name}] OnRobberExit: {robber.name}");

            // set an ignore window so re-detection is prevented for a short time
            if (robberIgnoreTimestamps != null)
            {
                robberIgnoreTimestamps[robber] = Time.time + ignoreRobberDuration;
                if (debugMode) Debug.Log($"[{gameObject.name}] Will ignore {robber.name} for {ignoreRobberDuration:F2}s");
            }
        // clear target but keep last known
        if (currentRobber == robber) currentRobber = null;
        ChangeState(PoliceState.Search);
    }
    #endregion

    #region Utilities
    public void SetPatrolStartNode(int index)
    {
        if (index >= 0 && index < pathManager.GetNodeCount())
        {
            currentNodeIndex = index;
            currentTargetNode = pathManager.GetNode(currentNodeIndex);
            agent.SetDestination(currentTargetNode.transform.position);
        }
    }
    #endregion
}
