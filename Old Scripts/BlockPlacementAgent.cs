using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class BlockPlacementAgent : Agent
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;

    [Header("Agent Settings")]
    [SerializeField] private bool visualizeActions = true;
    [SerializeField] private float actionDelay = 0.5f; // Delay between actions in seconds
    [SerializeField] private int maxPlacementsPerEpisode = 50; // Maximum placements per episode
    [SerializeField] private bool bypassDelayInTraining = true; // Skip delay when in training mode

    // Cached grid dimensions
    private int gridWidth;
    private int gridHeight;

    // Episode tracking
    private bool gameEnded = false;
    private bool isDelaying = false; // Flag to track if we're in a delay
    private int placementCount = 0; // Track number of placements in current episode
    private int initialBlockCount = 0; // Track the initial block count
    private int placedBlocksFromSet = 0; // Track how many blocks have been placed from the current set
    private int stepCount = 0; // Track the number of steps taken

    private void Start()
    {
        // Ensure GridManager is assigned
        if (gridManager == null)
        {
            gridManager = FindObjectOfType<GridManager>();
            if (gridManager == null)
            {
                Debug.LogError("GridManager not found! The BlockPlacementAgent requires a GridManager to function.");
                enabled = false;
                return;
            }
        }

        // Cache grid dimensions
        gridWidth = gridManager.Width;
        gridHeight = gridManager.Height;
    }

    public override void OnEpisodeBegin()
    {
        // Reset game-ended flag
        gameEnded = false;
        // Reset placement counter
        placementCount = 0;
        // Reset the placed blocks counter
        placedBlocksFromSet = 0;
        // Reset step counter
        stepCount = 0;
        // Store the initial block count
        initialBlockCount = gridManager.currentBlocks.Count;
        Debug.Log("Episode begin");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Get the current state of the grid
        int[,] gridState = gridManager.GetGridState();

        // 1. Observe the grid state (flattened 2D array)
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                sensor.AddObservation(gridState[x, y]);
            }
        }

        // 2. Observe the available blocks (up to 3)
        for (int i = 0; i < 3; i++)
        {
            if (i < gridManager.currentBlocks.Count)
            {
                // Block exists
                BlockShape shape = gridManager.currentBlocks[i].shape;
                
                // Add a "block exists" flag
                sensor.AddObservation(1f);
                
                // Observe block shape (normalized to a fixed size grid representation)
                ObserveBlockShape(sensor, shape);
            }
            else
            {
                // No block in this slot
                sensor.AddObservation(0f);
                
                // Add empty data for consistency
                AddEmptyBlockData(sensor);
            }
        }
    }

    private void ObserveBlockShape(VectorSensor sensor, BlockShape shape)
    {
        // Determine the boundaries of the shape to normalize it
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (Vector2Int cell in shape.cells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxY = Mathf.Max(maxY, cell.y);
        }
        
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        
        // Create a normalized representation (assuming max 5x5 block size)
        bool[,] normalizedShape = new bool[5, 5];
        
        // Fill in the shape
        foreach (Vector2Int cell in shape.cells)
        {
            int normalizedX = cell.x - minX;
            int normalizedY = cell.y - minY;
            
            if (normalizedX < 5 && normalizedY < 5)
            {
                normalizedShape[normalizedX, normalizedY] = true;
            }
        }
        
        // Add the normalized shape to observations
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                sensor.AddObservation(normalizedShape[x, y] ? 1f : 0f);
            }
        }
        
        // Add shape dimensions
        sensor.AddObservation(width);
        sensor.AddObservation(height);
    }
    
    private void AddEmptyBlockData(VectorSensor sensor)
    {
        // Add empty block data (25 zeros for the 5x5 grid + 2 zeros for dimensions)
        for (int i = 0; i < 25 + 2; i++)
        {
            sensor.AddObservation(0f);
        }
    }

    // Implementation of action masking to prevent invalid moves
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // First, check if there are any blocks
        if (gridManager.currentBlocks.Count == 0)
        {
            // No blocks available, enable all actions as default
            // Enable the first block option
            actionMask.SetActionEnabled(0, 0, true);
            
            // Enable all grid positions
            for (int x = 0; x < gridWidth; x++) {
                actionMask.SetActionEnabled(1, x, true);
            }
            
            for (int y = 0; y < gridHeight; y++) {
                actionMask.SetActionEnabled(2, y, true);
            }
            
            return;
        }
        
        // Initialize arrays to track which blocks can be placed
        bool[] blockCanBePlacedAnywhere = new bool[3];
        
        // Check each block to see if it can be placed anywhere on the grid
        for (int blockIdx = 0; blockIdx < Mathf.Min(3, gridManager.currentBlocks.Count); blockIdx++)
        {
            // Skip if block doesn't exist
            if (blockIdx >= gridManager.currentBlocks.Count || 
                gridManager.currentBlocks[blockIdx] == null || 
                gridManager.currentBlocks[blockIdx].shape == null)
            {
                continue;
            }
            
            BlockShape blockShape = gridManager.currentBlocks[blockIdx].shape;
            
            // Check every position on the grid
            bool canPlaceAnywhere = false;
            
            // Scan the entire grid for valid placements
            for (int x = 0; x < gridWidth && !canPlaceAnywhere; x++)
            {
                for (int y = 0; y < gridHeight && !canPlaceAnywhere; y++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    if (gridManager.CanPlaceBlock(blockShape, position))
                    {
                        canPlaceAnywhere = true;
                        break;
                    }
                }
            }
            
            // Record if this block can be placed
            blockCanBePlacedAnywhere[blockIdx] = canPlaceAnywhere;
            
            // Always enable the block action, regardless if it can be placed or not
            // This ensures at least one action is always available in branch 0
            actionMask.SetActionEnabled(0, blockIdx, true);
        }
        
        // Make sure blocks that don't exist are disabled
        for (int i = gridManager.currentBlocks.Count; i < 3; i++)
        {
            actionMask.SetActionEnabled(0, i, false);
        }
        
        // If we have at least one block but none can be placed anywhere, enable the first block
        bool anyBlockCanBePlaced = false;
        for (int i = 0; i < Mathf.Min(3, gridManager.currentBlocks.Count); i++)
        {
            if (blockCanBePlacedAnywhere[i])
            {
                anyBlockCanBePlaced = true;
                break;
            }
        }
        
        // CRITICAL: Ensure at least one block is always enabled
        // If none can be placed, at least enable the first one
        if (!anyBlockCanBePlaced && gridManager.currentBlocks.Count > 0)
        {
            actionMask.SetActionEnabled(0, 0, true);
        }
        
        // Enable all grid positions
        for (int x = 0; x < gridWidth; x++) {
            actionMask.SetActionEnabled(1, x, true);
        }
        
        for (int y = 0; y < gridHeight; y++) {
            actionMask.SetActionEnabled(2, y, true);
        }
        
       
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Skip if game has ended or if we're currently in a delay
        if (gameEnded || isDelaying)
        {
            return;
        }

        // Check if we're in training mode
        bool isTraining = Academy.Instance.IsCommunicatorOn;

        // If delay is enabled and we're not bypassing it during training
        if (actionDelay > 0 && !(isTraining && bypassDelayInTraining))
        {
            StartCoroutine(DelayedAction(actionBuffers));
            return;
        }
        
        // Otherwise, process the action immediately
        ProcessAction(actionBuffers);
    }
    
    private IEnumerator DelayedAction(ActionBuffers actionBuffers)
    {
        // Set the delay flag
        isDelaying = true;
        
        // Wait for the specified time
        yield return new WaitForSeconds(actionDelay);
        
        // Process the action
        ProcessAction(actionBuffers);
        
        // Clear the delay flag
        isDelaying = false;
    }
    
    private void ProcessAction(ActionBuffers actionBuffers)
    {
        // Skip if game has ended
        if (gameEnded)
        {
            return;
        }

        // Increment step count
        stepCount++;

        // Check if we're in training mode
        bool isTraining = Academy.Instance.IsCommunicatorOn;

        // Get discrete actions
        int blockIndex = actionBuffers.DiscreteActions[0]; // 0, 1, or 2 (which block to place)
        int xPos = actionBuffers.DiscreteActions[1]; // Position on grid (x)
        int yPos = actionBuffers.DiscreteActions[2]; // Position on grid (y)

        // Ensure actions are within valid range
        blockIndex = Mathf.Clamp(blockIndex, 0, 2);
        xPos = Mathf.Clamp(xPos, 0, gridWidth - 1);
        yPos = Mathf.Clamp(yPos, 0, gridHeight - 1);

        // Check if we have enough blocks
        if (gridManager.currentBlocks.Count == 0)
        {
            if (placedBlocksFromSet >= initialBlockCount)
            {
                // Only spawn new blocks after all initial blocks have been placed
                gridManager.SpawnBlocks(3);
                // Reset the counter for the new set of blocks
                placedBlocksFromSet = 0;
                // Store the count of the new set
                initialBlockCount = gridManager.currentBlocks.Count;
            }
            
            // If still no blocks, end episode
            if (gridManager.currentBlocks.Count == 0)
            {
                gameEnded = true;
                EndEpisode();
                return;
            }
        }

        // Ensure blockIndex is valid based on available blocks
        if (blockIndex >= gridManager.currentBlocks.Count)
        {
            blockIndex = gridManager.currentBlocks.Count - 1;
        }

        // Get the selected block and its shape
        BlockController selectedBlock = gridManager.currentBlocks[blockIndex];
        if (selectedBlock == null || selectedBlock.shape == null)
        {
            // Invalid block, check if we should spawn new ones
            if (placedBlocksFromSet >= initialBlockCount)
            {
                gridManager.SpawnBlocks(3);
                // Reset the counter for the new set of blocks
                placedBlocksFromSet = 0;
                // Store the count of the new set
                initialBlockCount = gridManager.currentBlocks.Count;
            }
            return;
        }
        
        BlockShape blockShape = selectedBlock.shape;
        Vector2Int position = new Vector2Int(xPos, yPos);

        // Check if the placement is valid at the chosen position
        bool canPlace = gridManager.CanPlaceBlock(blockShape, position);
        
        // If the original position doesn't work, try to find a valid position on the grid
        if (!canPlace)
        {
            // Search the entire grid for valid placement positions
            List<Vector2Int> validPositions = new List<Vector2Int>();
            
            // Scan the entire grid
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector2Int testPos = new Vector2Int(x, y);
                    if (gridManager.CanPlaceBlock(blockShape, testPos))
                    {
                        validPositions.Add(testPos);
                    }
                }
            }
            
            // If we found valid positions, choose the one closest to the original selection
            if (validPositions.Count > 0)
            {
                // Sort by distance to the original position
                validPositions.Sort((a, b) => 
                    Vector2Int.Distance(a, position).CompareTo(Vector2Int.Distance(b, position)));
                
                // Use the closest valid position
                position = validPositions[0];
                canPlace = true;
            }
        }
        
        // Try to place the block
        if (canPlace)
        {
            bool placed = gridManager.TryPlaceBlock(blockShape, position);

            if (placed)
            {
                // Increment placement counter
                placementCount++;
                // Increment the counter for blocks placed from this set
                placedBlocksFromSet++;
                
                // Calculate reward
                float reward = CalculateReward(position, blockShape, gridHeight);
                AddReward(reward);

                // Remove the block from available blocks
                gridManager.RemoveBlock(selectedBlock);
                
                // Only spawn new blocks if all from the current set have been placed
                if (gridManager.currentBlocks.Count == 0 && placedBlocksFromSet >= initialBlockCount)
                {
                    gridManager.SpawnBlocks(3);
                    // Reset the counter for the new set of blocks
                    placedBlocksFromSet = 0;
                    // Store the count of the new set
                    initialBlockCount = gridManager.currentBlocks.Count;
                }

                // Check if we've reached the max placements
                if (placementCount >= maxPlacementsPerEpisode)
                {
                    gameEnded = true;
                    Debug.Log("Episode end on 50 placements");
                    EndEpisode();
                    return;
                }
                
                // Only log every 10 steps in training mode to reduce overhead
                if (!isTraining || stepCount % 10 == 0)
                {
                    Debug.Log("Step count: " + stepCount + ", Placement count: " + placementCount + 
                             ", Training mode: " + (isTraining ? "Yes" : "No"));
                }
            }
        }
    }

    private float CalculateReward(Vector2Int position, BlockShape blockShape, int gridHeight)
    {
        // Default reward
        float reward = 0f;

        // Check all cells of the block
        foreach (Vector2Int cell in blockShape.cells)
        {
            Vector2Int gridPos = position + cell;
            
            // Ensure position is within grid bounds
            if (gridPos.x >= 0 && gridPos.x < gridWidth && gridPos.y >= 0 && gridPos.y < gridHeight)
            {
                // +1 for top row (y = gridHeight - 1) or bottom row (y = 0)
                if (gridPos.y == 0 || gridPos.y == gridHeight - 1)
                {
                    reward = 1.0f;
                }
                // -1 for middle rows
                else
                {
                    reward = -1.0f;
                }
            }
        }
        
        // Only log reward in non-training mode or occasionally in training
        bool isTraining = Academy.Instance.IsCommunicatorOn;
        if (!isTraining || stepCount % 10 == 0)
        {
            Debug.Log("Reward: " + reward);
        }
        
        return reward;
    }

    // For manual control during testing
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        
        // Select block (1, 2, 3 keys)
        if (Input.GetKeyDown(KeyCode.Alpha1)) discreteActions[0] = 0;
        else if (Input.GetKeyDown(KeyCode.Alpha2)) discreteActions[0] = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha3)) discreteActions[0] = 2;
        
        // Move with arrow keys
        discreteActions[1] = Mathf.FloorToInt(Input.GetAxisRaw("Horizontal") + gridWidth / 2);
        discreteActions[2] = Mathf.FloorToInt(Input.GetAxisRaw("Vertical") + gridHeight / 2);
        
        // Clamp to valid ranges
        discreteActions[1] = Mathf.Clamp(discreteActions[1], 0, gridWidth - 1);
        discreteActions[2] = Mathf.Clamp(discreteActions[2], 0, gridHeight - 1);
    }
} 