using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TrafficManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("List of car prefabs to spawn (must have Steering component)")]
    public List<GameObject> carPrefabs = new List<GameObject>();
    
    [Tooltip("List of path managers")]
    public List<PathNode> pathManagers = new List<PathNode>();
    
    [Tooltip("Number of cars to spawn")]
    [Range(1, 50)]
    public int numberOfCars = 5;
    
    [Tooltip("Minimum delay between car spawns (seconds)")]
    [Range(0.1f, 10f)]
    public float minSpawnDelay = 0.5f;
    
    [Tooltip("Maximum delay between car spawns (seconds)")]
    [Range(0.1f, 10f)]
    public float maxSpawnDelay = 3f;
    
    [Header("Spawn Points")]
    [Tooltip("Always spawn at first node of each path manager")]
    public bool spawnAtFirstNode = true;
    
    [Tooltip("If false and not spawning at first node, use random nodes")]
    public bool useRandomSpawnPoints = true;
    
    [Tooltip("Minimum node gap between spawned cars (prevents spawning too close)")]
    [Range(0, 20)]
    public int minNodeGap = 2;
    
    [Header("Car Customization")]
    [Tooltip("Randomize car movement speed")]
    public bool randomizeSpeed = true;
    
    [Tooltip("Minimum random speed")]
    [Range(1f, 50f)]
    public float minSpeed = 8f;
    
    [Tooltip("Maximum random speed")]
    [Range(1f, 50f)]
    public float maxSpeed = 15f;
    
    [Header("Debug")]
    [Tooltip("Enable debug logging")]
    public bool debugMode = false;
    
    // Private variables
    private List<GameObject> spawnedCars = new List<GameObject>();
    private int currentPathIndex = 0;

    void Start()
    {
        // Validate references
        if (carPrefabs == null || carPrefabs.Count == 0)
        {
            Debug.LogError("No car prefabs assigned on TrafficManager!");
            enabled = false;
            return;
        }
        
        if (pathManagers == null || pathManagers.Count == 0)
        {
            Debug.LogError("No path managers assigned on TrafficManager!");
            enabled = false;
            return;
        }
        
        // Validate all path managers have nodes
        bool allPathsValid = true;
        for (int i = 0; i < pathManagers.Count; i++)
        {
            if (pathManagers[i] == null || pathManagers[i].GetNodeCount() == 0)
            {
                Debug.LogError($"Path Manager at index {i} is null or has no nodes!");
                allPathsValid = false;
            }
        }
        
        if (!allPathsValid)
        {
            enabled = false;
            return;
        }
        
        // Start spawning cars
        StartCoroutine(SpawnCarsAsync());
    }
    
    private IEnumerator SpawnCarsAsync()
    {
        for (int i = 0; i < numberOfCars; i++)
        {
            // Random delay between spawns
            float delay = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(delay);
            
            // Spawn a car
            SpawnCar(i);
        }
        
        if (debugMode)
        {
            Debug.Log($"TrafficManager: Finished spawning {numberOfCars} cars.");
        }
    }
    
    private void SpawnCar(int carIndex)
    {
        // Select a random car prefab
        GameObject selectedCarPrefab = carPrefabs[Random.Range(0, carPrefabs.Count)];
        
        // Select path manager (cycle through them)
        PathNode selectedPathManager = pathManagers[currentPathIndex % pathManagers.Count];
        currentPathIndex++;
        
        // Determine spawn node index
        int spawnNodeIndex;
        if (spawnAtFirstNode)
        {
            spawnNodeIndex = 0; // Always spawn at first node
        }
        else if (useRandomSpawnPoints)
        {
            spawnNodeIndex = Random.Range(0, selectedPathManager.GetNodeCount());
        }
        else
        {
            spawnNodeIndex = 0; // Default to first node
        }
        
        Node spawnNode = selectedPathManager.GetNode(spawnNodeIndex);
        if (spawnNode == null || spawnNode.transform == null)
        {
            Debug.LogWarning($"TrafficManager: Spawn node {spawnNodeIndex} is invalid!");
            return;
        }
        
        // Get spawn position and rotation
        Vector3 spawnPosition = spawnNode.transform.position;
        
        // Calculate spawn rotation (face next node)
        Node nextNode = selectedPathManager.GetNextNode(spawnNodeIndex);
        Quaternion spawnRotation = Quaternion.identity;
        
        if (nextNode != null && nextNode.transform != null)
        {
            Vector3 direction = (nextNode.transform.position - spawnPosition).normalized;
            if (direction != Vector3.zero)
            {
                spawnRotation = Quaternion.LookRotation(direction);
            }
        }
        
        // Instantiate the car
        GameObject newCar = Instantiate(selectedCarPrefab, spawnPosition, spawnRotation);
        newCar.name = $"{selectedCarPrefab.name}_{carIndex}";
        
        // Get the Steering component and configure it
        Steering steeringComponent = newCar.GetComponent<Steering>();
        if (steeringComponent != null)
        {
            steeringComponent.pathManager = selectedPathManager;
            
            // Randomize speed if enabled
            if (randomizeSpeed)
            {
                steeringComponent.movementSpeed = Random.Range(minSpeed, maxSpeed);
            }
            
            if (debugMode)
            {
                Debug.Log($"Assigned PathManager to car. IsLooping: {selectedPathManager.isLooping}, DestroyAtEnd: {steeringComponent.destroyAtEnd}");
            }
        }
        else
        {
            Debug.LogWarning($"TrafficManager: Car prefab '{selectedCarPrefab.name}' does not have a Steering component!");
        }
        
        // Track spawned car
        spawnedCars.Add(newCar);
        
        if (debugMode)
        {
            Debug.Log($"TrafficManager: Spawned {selectedCarPrefab.name} (car {carIndex}) at node {spawnNodeIndex} on path {currentPathIndex - 1}, position {spawnPosition}");
        }
    }
    
    private int GetSpawnNodeIndex()
    {
        // This method is no longer used but kept for compatibility
        return 0;
    }
    
    private bool IsNodeValidForSpawn(int nodeIndex)
    {
        // This method is no longer used but kept for compatibility
        return true;
    }
    
    /// <summary>
    /// Get all currently spawned cars
    /// </summary>
    public List<GameObject> GetSpawnedCars()
    {
        // Remove null references (destroyed cars)
        spawnedCars.RemoveAll(car => car == null);
        return spawnedCars;
    }
    
    /// <summary>
    /// Get the number of active cars
    /// </summary>
    public int GetActiveCarCount()
    {
        return GetSpawnedCars().Count;
    }
    
    /// <summary>
    /// Manually spawn a single car
    /// </summary>
    public void SpawnSingleCar()
    {
        SpawnCar(spawnedCars.Count);
    }
    
    /// <summary>
    /// Destroy all spawned cars
    /// </summary>
    public void ClearAllCars()
    {
        foreach (GameObject car in spawnedCars)
        {
            if (car != null)
            {
                Destroy(car);
            }
        }
        
        spawnedCars.Clear();
        currentPathIndex = 0;
        
        if (debugMode)
        {
            Debug.Log("TrafficManager: Cleared all cars.");
        }
    }
}
