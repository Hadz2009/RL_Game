// PlayerAgent.cs (WITH EXTRA DEBUG LOGS)

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using System.Linq;

public class PlayerAgent : Agent
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private BlockController blockController;
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private DifficultyManager difficultyManager;
    [Header("Reward Settings")]
    [SerializeField] private float lineClearMultiplier = 0.0f; // Set to zero to ignore line clearing rewards
    [SerializeField] private float gameOverPenalty = -1.0f;
    [SerializeField] private float invalidMovePenalty = -0.05f;
    [SerializeField] private float survivalRewardPerStep = 0.0f; // Zero out
    [SerializeField] private float adjacencyReward = 0.0f; // Zero out
    [SerializeField] private float holePenalty = 0.0f; // Zero out
    [SerializeField] private float compactnessReward = 0.0f; // Zero out
    [SerializeField] private float misalignedPlacementPenalty = 0.0f; // Zero out
    [SerializeField] private float topBottomEdgeReward = 2.0f; // Increased from 1.0f for extreme focus
    [SerializeField] private float boundaryNotFivePenalty = 0.0f; // Zero out as less relevant
    [SerializeField] private float centerPlacementPenalty = -2.0f; // Increased from -1.0f for extreme focus

    [Header("Advanced Planning")]
    // [SerializeField] private bool enableLookAhead = true; // Toggle for look-ahead planning
    // [SerializeField] private int lookAheadDepth = 2; // How many future blocks to consider
    // [SerializeField] private float bumpinessPenalty = -0.05f; // Penalty for uneven surfaces
    // [SerializeField] private int maxSimulationsPerBlock = 25; // Limit simulations to avoid performance issues

    [Header("Timing Settings")] // Added for clarity
    [SerializeField] private float moveDelaySeconds = 0.5f; // Time to wait after a successful move

    [Header("Guided Training Settings")]
    // [SerializeField] private bool enableGuidedTraining = true; // Enable planning guidance during training
    // [SerializeField] private float guidanceRewardScale = 0.1f; // How much to reward following the planner's advice
    // [SerializeField] private int maxGuidanceSimulations = 10; // Maximum simulations for guidance (keep low for performance)

    private int gridWidth;
    private int gridHeight;
    private List<BlockShape> agentInternalShapeVocabulary;
    private Dictionary<string, int> agentShapeNameToIdMap = new Dictionary<string, int>();
    private const int MAX_AGENT_SHAPE_ID = 50;

    private float nextActionTime = 0f; // Time when the next action is allowed

    // DEBUG: Counter for heuristic actions
    private int heuristicActionCounter = 0;

    [SerializeField] private bool ManualPlayMode = false;
    private int stepCounter = 0;
    private int maxStepCount = 50;
    // Valid moves cache for action masking
    private Dictionary<int, List<Vector2Int>> validPlacementsByBlockIndex = new Dictionary<int, List<Vector2Int>>();

    public override void Initialize()
    {
        
        if (gridManager == null) { Debug.LogError("GridManager not assigned!", this); enabled = false; return; }
        if (gameManager == null) { Debug.LogError("GameManager not assigned!", this); enabled = false; return; }
        gridWidth = gridManager.gridWidth;
        gridHeight = gridManager.gridHeight;
        PopulateAgentInternalShapeMap();
        
    }

    private void AddClampedReward(float rawReward)
    {
        float clampedReward = Mathf.Clamp(rawReward, -1.0f, 1.0f);
        AddReward(clampedReward);
    }

    private void Update()
    {
        // Only check for game over if lines are not being cleared
        if(gridManager.CheckGameOver())
        {
            // Check if agent *actually* has no moves left with its current blocks,
            // even if the general game over condition is met.
            bool agentHasAnyValidMove = false;
            if (validPlacementsByBlockIndex != null) 
            {
                foreach (var placementsList in validPlacementsByBlockIndex.Values)
                {
                    if (placementsList != null && placementsList.Count > 0)
                    {
                        agentHasAnyValidMove = true;
                        Debug.Log($"<color=green>[PlayerAgent.Update]</color> Agent has valid moves. Continuing episode.");
                        break;
                    }
                }
            }

            if (!agentHasAnyValidMove) // Only truly end if game over AND no possible moves remain
            {
                AddClampedReward(gameOverPenalty);
                difficultyManager.gameTimer = 0;
                Debug.Log($"<color=red>[PlayerAgent.Update]</color> Game Over. No valid moves. Ending episode.");
                EndEpisode();
            }
        }

        if (stepCounter >= 50)
        {
            Debug.Log("Episode End at StepCount: " + stepCounter);
            EndEpisode();
        }
    }

    private void PopulateAgentInternalShapeMap()
    {
        agentInternalShapeVocabulary = BlockShape.GetStandardShapes()
                                      .Where(s => s != null && !string.IsNullOrEmpty(s.shapeName))
                                      .OrderBy(s => s.shapeName)
                                      .Distinct(new BlockShapeNameComparer())
                                      .ToList();
        agentShapeNameToIdMap.Clear();
        for (int i = 0; i < agentInternalShapeVocabulary.Count; i++)
        {
            if (!agentShapeNameToIdMap.ContainsKey(agentInternalShapeVocabulary[i].shapeName))
            {
                agentShapeNameToIdMap.Add(agentInternalShapeVocabulary[i].shapeName, i + 1);
            }
        }
    }

    private class BlockShapeNameComparer : IEqualityComparer<BlockShape>
    {
        public bool Equals(BlockShape x, BlockShape y) { if (ReferenceEquals(x, y)) return true; if (x is null || y is null) return false; return x.shapeName == y.shapeName; }
        public int GetHashCode(BlockShape obj) { return obj?.shapeName?.GetHashCode() ?? 0; }
    }

    public override void OnEpisodeBegin()
    {
        heuristicActionCounter = 0; // Reset for heuristic mode debugging
        nextActionTime = Time.time; // Allow the first action immediately
        validPlacementsByBlockIndex.Clear(); // Clear valid moves cache

        if (gridManager.availableBlocks == null || gridManager.availableBlocks.Count == 0)
        {
            gridManager.availableBlocks = BlockShape.GetStandardShapes();
        }
        
        gameManager.ResetScore();
        gridManager.ClearGrid();
        gridManager.SpawnBlocks(3);
        stepCounter = 0;
        // Precompute valid placements for each block
        PrecomputeValidPlacements();
    }

    // New method to precompute all valid placements for each block
    private void PrecomputeValidPlacements()
    {
        validPlacementsByBlockIndex.Clear();
        
        for (int blockIdx = 0; blockIdx < gridManager.currentBlocks.Count; blockIdx++)
        {
            BlockController controller = gridManager.currentBlocks[blockIdx];
            if (controller == null || controller.shape == null) continue;
            
            List<Vector2Int> validPlacements = new List<Vector2Int>();
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (gridManager.CanPlaceBlock(controller.shape, pos))
                    {
                        validPlacements.Add(pos);
                    }
                }
            }
            
            validPlacementsByBlockIndex[blockIdx] = validPlacements;
        }
    }

    // Calculate adjacency score (how many sides of the placement touch existing blocks)
    private int CalculateAdjacencyScore(BlockShape shape, Vector2Int position)
    {
        if (shape == null || shape.cells == null || shape.cells.Count == 0) return 0;

        // MODIFICATION START: If block touches any boundary, adjacency score is 0.
        bool isTouchingBoundary = false;
        // Iterate over each cell of the shape to check its world position
        foreach (Vector2Int cellOffsetInShape in shape.cells)
        {
            Vector2Int worldCellPos = position + cellOffsetInShape;

            // Check if this cell is at any of the grid boundaries
            // Assumes grid coordinates are [0, gridWidth-1] and [0, gridHeight-1]
            if (worldCellPos.x == 0 || worldCellPos.x == gridWidth - 1 ||
                worldCellPos.y == 0 || worldCellPos.y == gridHeight - 1)
            {
                isTouchingBoundary = true;
                break; // Found a cell touching a boundary, no need to check further
            }
        }

        if (isTouchingBoundary)
        {
            return 0; // Adjacency score is 0 if any part of the block touches a grid boundary.
        }
        // MODIFICATION END

        int alignedSides = 0;
        int[,] currentGrid = gridManager.GetGridState();

        // Determine the bounding box of the shape
        int minX = shape.cells.Min(cell => cell.x);
        int maxX = shape.cells.Max(cell => cell.x);
        int minY = shape.cells.Min(cell => cell.y);
        int maxY = shape.cells.Max(cell => cell.y);

        // Check alignment for each of the four potential sides of the shape's bounding box

        // Check Top Edge (y = maxY)
        bool topEdgeAligned = true;
        if (position.y + maxY < gridHeight -1) // Only check if not at the very top of the grid, allowing alignment with top boundary
        {
            for (int x_offset = minX; x_offset <= maxX; x_offset++)
            {
                // Check if a cell of the shape exists at this x_offset along its top edge
                if (shape.cells.Any(cell => cell.x == x_offset && cell.y == maxY))
                {
                    Vector2Int worldPos = position + new Vector2Int(x_offset, maxY + 1); // Cell directly above
                    if (worldPos.x < 0 || worldPos.x >= gridWidth || worldPos.y < 0 || worldPos.y >= gridHeight || currentGrid[worldPos.y, worldPos.x] == 0)
                    {
                        topEdgeAligned = false;
                        break;
                    }
                }
            }
        } else { // If at the top edge of the grid, consider it aligned
             topEdgeAligned = false;
        }
        if (topEdgeAligned && shape.cells.Any(c => c.y == maxY)) alignedSides++;


        // Check Bottom Edge (y = minY)
        bool bottomEdgeAligned = true;
        if (position.y + minY > 0) // Only check if not at the very bottom of the grid, allowing alignment with bottom boundary
        {
            for (int x_offset = minX; x_offset <= maxX; x_offset++)
            {
                if (shape.cells.Any(cell => cell.x == x_offset && cell.y == minY))
                {
                    Vector2Int worldPos = position + new Vector2Int(x_offset, minY - 1); // Cell directly below
                    if (worldPos.x < 0 || worldPos.x >= gridWidth || worldPos.y < 0 || worldPos.y >= gridHeight || currentGrid[worldPos.y, worldPos.x] == 0)
                    {
                        bottomEdgeAligned = false;
                        break;
                    }
                }
            }
        } else { // If at the bottom edge of the grid, consider it aligned
            bottomEdgeAligned = false;
        }
        if (bottomEdgeAligned && shape.cells.Any(c => c.y == minY)) alignedSides++;


        // Check Left Edge (x = minX)
        bool leftEdgeAligned = true;
        if (position.x + minX > 0) // Only check if not at the very left of the grid
        {
            for (int y_offset = minY; y_offset <= maxY; y_offset++)
            {
                if (shape.cells.Any(cell => cell.y == y_offset && cell.x == minX))
                {
                    Vector2Int worldPos = position + new Vector2Int(minX - 1, y_offset); // Cell directly to the left
                    if (worldPos.x < 0 || worldPos.x >= gridWidth || worldPos.y < 0 || worldPos.y >= gridHeight || currentGrid[worldPos.y, worldPos.x] == 0)
                    {
                        leftEdgeAligned = false;
                        break;
                    }
                }
            }
        } else { // If at the left edge of the grid, consider it aligned
            leftEdgeAligned = false;
        }
        if (leftEdgeAligned && shape.cells.Any(c => c.x == minX)) alignedSides++;


        // Check Right Edge (x = maxX)
        bool rightEdgeAligned = true;
        if (position.x + maxX < gridWidth - 1) // Only check if not at the very right of the grid
        {
            for (int y_offset = minY; y_offset <= maxY; y_offset++)
            {
                if (shape.cells.Any(cell => cell.y == y_offset && cell.x == maxX))
                {
                    Vector2Int worldPos = position + new Vector2Int(maxX + 1, y_offset); // Cell directly to the right
                    if (worldPos.x < 0 || worldPos.x >= gridWidth || worldPos.y < 0 || worldPos.y >= gridHeight || currentGrid[worldPos.y, worldPos.x] == 0)
                    {
                        rightEdgeAligned = false;
                        break;
                    }
                }
            }
        } else { // If at the right edge of the grid, consider it aligned
            rightEdgeAligned = false;
        }
        if (rightEdgeAligned && shape.cells.Any(c => c.x == maxX)) alignedSides++;

        return alignedSides;
    }
    
    // Calculate how many holes would be created by this placement
    private int CalculateNewHoles(BlockShape shape, Vector2Int position)
    {
        if (shape == null) return 0;
        
        int[,] currentGrid = gridManager.GetGridState(); // Correctly [gridWidth, gridHeight] -> [col, row]
        
        // Initialize simulatedGrid with correct dimensions: [gridWidth, gridHeight]
        int[,] simulatedGrid = new int[gridWidth, gridHeight]; 
        
        // Copy currentGrid to simulatedGrid correctly
        for (int x = 0; x < gridWidth; x++) // Iterate columns
        {
            for (int y = 0; y < gridHeight; y++) // Iterate rows
            {
                simulatedGrid[x, y] = currentGrid[x, y]; // Access both as [col, row]
            }
        }
        
        // Add shape to simulatedGrid correctly
        foreach (Vector2Int offset in shape.cells)
        {
            Vector2Int cellPos = position + offset; // cellPos.x is col, cellPos.y is row
            if (cellPos.x >= 0 && cellPos.x < gridWidth && cellPos.y >= 0 && cellPos.y < gridHeight)
            {
                simulatedGrid[cellPos.x, cellPos.y] = 1; // Access as [col, row]
            }
        }
        
        int currentHolesCount = gridManager.CalculateHoles(currentGrid); // Expects [col,row] - OK
        int newHolesCount = gridManager.CalculateHoles(simulatedGrid);   // Expects [col,row] - Now OK
        
        return newHolesCount - currentHolesCount;
    }
    
    // New method to count how many cells of the placed shape touch existing blocks
    private int CountTouchingCells(BlockShape shape, Vector2Int position)
    {
        if (shape == null || shape.cells == null) return 0;

        // Assuming gridManager.GetGridState() returns grid[row, col] or grid[y,x]
        // and that gridWidth/gridHeight are correctly set.
        int[,] currentGrid = gridManager.GetGridState(); 
        int touchingShapeCellsCount = 0;

        foreach (Vector2Int shapeCellOffset in shape.cells)
        {
            Vector2Int worldCellPos = position + shapeCellOffset;
            bool thisShapeCellIsTouching = false;

            // Check 4 orthogonal neighbors of this worldCellPos
            Vector2Int[] neighbors = new Vector2Int[]
            {
                new Vector2Int(worldCellPos.x + 1, worldCellPos.y), // Right
                new Vector2Int(worldCellPos.x - 1, worldCellPos.y), // Left
                new Vector2Int(worldCellPos.x, worldCellPos.y + 1), // Up
                new Vector2Int(worldCellPos.x, worldCellPos.y - 1)  // Down
            };

            foreach (Vector2Int neighborPos in neighbors)
            {
                // Check bounds and if neighbor is occupied in the *current* grid (before placement)
                // Assuming grid indices: currentGrid[col_x, row_y] if GetGridState() is [width, height]
                if (neighborPos.y >= 0 && neighborPos.y < gridHeight &&
                    neighborPos.x >= 0 && neighborPos.x < gridWidth &&
                    currentGrid[neighborPos.x, neighborPos.y] == 1) // Corrected Indexing: [x, y]
                {
                    thisShapeCellIsTouching = true;
                    break; // This cell of the new shape touches an existing block, no need to check other neighbors for this cell
                }
            }

            if (thisShapeCellIsTouching)
            {
                touchingShapeCellsCount++;
            }
        }
        return touchingShapeCellsCount;
    }

    // Add new method to count cells touching boundary
    private int CountCellsTouchingBoundary(BlockShape shape, Vector2Int position)
    {
        if (shape == null || shape.cells == null || shape.cells.Count == 0) return 0;

        int touchingCount = 0;
        // Check each cell of the shape
        foreach (Vector2Int cellOffsetInShape in shape.cells)
        {
            Vector2Int worldCellPos = position + cellOffsetInShape;

            // Check if this cell is at any of the grid boundaries
            if (worldCellPos.x == 0 || worldCellPos.x == gridWidth - 1 ||
                worldCellPos.y == 0 || worldCellPos.y == gridHeight - 1)
            {
                touchingCount++;
            }
        }

        return touchingCount;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Debug.Log("<color=cyan>[PlayerAgent.CollectObservations]</color> CALLED.");
        if (gridManager == null) { /* Add default obs */ return; }

        // Grid State
        for (int y = 0; y < gridHeight; y++) { 
                for (int x = 0; x < gridWidth; x++) {
                sensor.AddObservation(gridManager.IsCellOccupied(x, y) ? 1.0f : 0.0f); 
            }
        }

        // Dock Blocks
        System.Text.StringBuilder sb = new System.Text.StringBuilder("Observed Dock IDs: ");
        for (int i = 0; i < 3; i++)
        {
            int observedShapeId = 0;
            if (i < gridManager.currentBlocks.Count && gridManager.currentBlocks[i] != null && gridManager.currentBlocks[i].shape != null)
            {
                BlockShape shapeInDock = gridManager.currentBlocks[i].shape;
                observedShapeId = GetAgentShapeId(shapeInDock);
                if (observedShapeId == 0 && agentShapeNameToIdMap.Count > 0)
                    sb.Append($"Slot {i} ('{shapeInDock.shapeName}')=UNMAPPED, ");
                else
                    sb.Append($"Slot {i} ('{shapeInDock.shapeName}')={observedShapeId}, ");
            } else {
                 sb.Append($"Slot {i}=EMPTY/NULL, ");
            }
            sensor.AddObservation((float)observedShapeId / MAX_AGENT_SHAPE_ID);
        }
        // Only log observations periodically or if something looks off, can be very spammy
        // if (Time.frameCount % 60 == 0) Debug.Log($"<color=gray>[PlayerAgent.CollectObservations]</color> {sb.ToString()}");


        // Heuristics
        int[,] currentGridState = gridManager.GetGridState();
        float density = (currentGridState != null && (gridWidth * gridHeight > 0)) ? (float)currentGridState.Cast<int>().Count(cell => cell == 1) / (gridWidth * gridHeight) : 0f; sensor.AddObservation(density);
        float aggHeight = (currentGridState != null && (gridHeight * gridWidth > 0)) ? (float)gridManager.CalculateAggregateHeight(currentGridState) / (gridHeight * gridWidth) : 0f; sensor.AddObservation(aggHeight);
        float bump = (currentGridState != null && (gridHeight > 0 && gridWidth > 0)) ? (float)gridManager.CalculateBumpiness(currentGridState) / (gridHeight * (gridWidth > 1 ? gridWidth - 1 : 1)) : 0f; sensor.AddObservation(bump);
        float holes = (currentGridState != null && (gridWidth * gridHeight > 0)) ? (float)gridManager.CalculateHoles(currentGridState) / (gridWidth * gridHeight) : 0f; sensor.AddObservation(holes);
    }

    private int GetAgentShapeId(BlockShape shape)
    {
        if (agentShapeNameToIdMap == null || agentShapeNameToIdMap.Count == 0) return 0;
        if (shape != null && !string.IsNullOrEmpty(shape.shapeName) && agentShapeNameToIdMap.TryGetValue(shape.shapeName, out int id)) return id;
        return 0;
    }

    void OnEpisodeEnd()
    {
      //  float bonus = (stepCounter / 50f) * 5.0f; // up to +5
      //  AddReward(bonus);
        

        
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        heuristicActionCounter++; // Increment for heuristic debug
       
        
            if (Time.time < nextActionTime)
            {
                return; // Skip action processing if in delay
            }

            int blockChoiceAction = actions.DiscreteActions[0];
            int targetX = actions.DiscreteActions[1];
            int targetY = actions.DiscreteActions[2];

            // --- CRITICAL: Validate blockChoiceAction --- 
            if (blockChoiceAction < 0 || blockChoiceAction >= gridManager.currentBlocks.Count)
            {
                // Should never happen with proper action masking
                return; 
            }
            // --- End Validation ---
            
            BlockController selectedController = gridManager.currentBlocks[blockChoiceAction];
            if (selectedController == null)
            {
                return;
            }
            if (selectedController.gameObject == null) {
                return;
            }


            BlockShape selectedShape = selectedController.shape;
            if (selectedShape == null)
            {
                return;
            }
        
            Vector2Int placementPosition = new Vector2Int(targetX, targetY);
            if (!gridManager.CanPlaceBlock(selectedShape, placementPosition))
            {
                // Should never happen with proper action masking
                return;
            }
            
            // Simple look-ahead guidance for training (if enabled)
            /*
            if (enableGuidedTraining && enableLookAhead)
            {
                // Evaluate the chosen action
                float chosenActionScore = 0;
                
                // Get current grid state for simulation
                int[,] currentGrid = gridManager.GetGridState();
                
                // Score the chosen action using simplified simulation
                chosenActionScore = EvaluateActionForTraining(selectedShape, placementPosition, blockChoiceAction, currentGrid);
                
                // Find what the planner would consider the best action
                float bestPlannerScore = float.MinValue;
                int bestBlockIndex = 0;
                Vector2Int bestPlacement = new Vector2Int(0, 0);
                bool foundBetter = false;
                
                // Only search through a small subset of possibilities for performance
                int simulationsRun = 0;
                
                // Look through each block
                foreach (var kvp in validPlacementsByBlockIndex)
                {
                    int blockIndex = kvp.Key;
                    List<Vector2Int> validPlacements = kvp.Value;
                    
                    if (validPlacements.Count > 0)
                    {
                        BlockShape shape = gridManager.currentBlocks[blockIndex].shape;
                        
                        // Take a smaller sample of possible placements to evaluate
                        int evaluationsPerBlock = Mathf.Min(3, validPlacements.Count);
                        int step = validPlacements.Count / Mathf.Max(1, evaluationsPerBlock);
                        
                        for (int i = 0; i < validPlacements.Count && simulationsRun < maxGuidanceSimulations; i += step)
                        {
                            Vector2Int placement = validPlacements[i];
                            float score = EvaluateActionForTraining(shape, placement, blockIndex, currentGrid);
                            
                            if (score > bestPlannerScore)
                            {
                                bestPlannerScore = score;
                                bestBlockIndex = blockIndex;
                                bestPlacement = placement;
                                foundBetter = true;
                            }
                            
                            simulationsRun++;
                        }
                    }
                }
                
                // If the planner found a significantly better action, add a guidance reward
                // based on how close the agent's choice was to the optimal choice
                if (foundBetter && bestPlannerScore > chosenActionScore * 1.5f)
                {
                    // Give a negative guidance reward (we want to discourage bad moves)
                    float guidanceReward = -guidanceRewardScale;
                    AddClampedReward(guidanceReward);
                }
                else if (foundBetter && bestPlannerScore > chosenActionScore * 1.1f)
                {
                    // Small negative reward for choices that are suboptimal but not terrible
                    float guidanceReward = -guidanceRewardScale * 0.5f;
                    AddClampedReward(guidanceReward);
                }
                else
                {
                    // Reward the agent for making a good choice (close to optimal)
                    float guidanceReward = guidanceRewardScale * 0.7f;
                    AddClampedReward(guidanceReward);
                }
            }
            */
        
            // Calculate placement metrics before placing
            int adjacencyScore = CalculateAdjacencyScore(selectedShape, placementPosition);
            int newHoles = CalculateNewHoles(selectedShape, placementPosition);
            int touchingCellCount = CountTouchingCells(selectedShape, placementPosition);
            int boundaryTouchingCells = CountCellsTouchingBoundary(selectedShape, placementPosition);
            
            // Get board state before placement for comparison
            int[,] beforeGrid = gridManager.GetGridState();
            if(!ManualPlayMode)
            {
                bool placedSuccessfully = gridManager.TryPlaceBlock(selectedShape, placementPosition);
            
                if (placedSuccessfully)
                {
                    int linesCleared = gridManager.GetLastLinesCleared();
                    float reward = (linesCleared * 2) * lineClearMultiplier;
                    
                    // Add reward for successful placement
                    //AddClampedReward(survivalRewardPerStep);
                    
                    // Add additional rewards for good placement
                    // Only apply adjacency-related rewards/penalties if no lines were cleared
                    if (linesCleared == 0)
                    {
                        if (adjacencyScore > 0)
                        {
                           // AddClampedReward(adjacencyScore * adjacencyReward); // Reward aligned sides
                        }
                        else
                        {
                          //  AddClampedReward(misalignedPlacementPenalty); // Penalize if no sides are aligned
                        }                
                        // New simplified compactness reward based on touching cells
                        if (touchingCellCount == 0) // If NO cells are touching an existing block
                        {
                          //  AddClampedReward(-compactnessReward * 2); // Penalize for placing in isolation
                            //Debug.Log($"- Compactness reward: {-compactnessReward}");
                        }
                        else if (touchingCellCount >= 2) // If 2 OR MORE cells are touching
                        {
                          //  AddClampedReward(compactnessReward); 
                            //Debug.Log($"- Compactness reward: {compactnessReward}");
                        }
                        // If touchingCellCount is 1, no specific reward/penalty from this system.
                    }
                    //AddClampedReward(newHoles * holePenalty); // Penalize new holes
                    
                    // Specific rewards for 5x1 block placement on 5x5 grid
                    if (boundaryTouchingCells == 5) // Indicates top or bottom edge for 5x1 block
                    {
                        
                        AddReward(topBottomEdgeReward);
                        Debug.Log($"- Top/Bottom Edge reward: {topBottomEdgeReward}");
                    }
                    else
                    {
                        AddReward(centerPlacementPenalty); // Minimal step penalty
                    }
                    
                    AddReward(-0.01f); // Penalize new holes

                    // Get board state after placement for advanced evaluation
                    int[,] afterGrid = gridManager.GetGridState();
                    
                   
                     
                    nextActionTime = Time.time + moveDelaySeconds; // Apply delay after successful move
                    
                    // VERY IMPORTANT: Verify selectedController is still valid and in the list *before* removing.
                    // If TryPlaceBlock somehow affects the list order or contents in an unexpected way, this could fail.
                    // GridManager.RemoveBlock should handle this safely if it takes the instance.
                    bool removedFromList = gridManager.currentBlocks.Contains(selectedController); // Check before removing

                    gridManager.RemoveBlock(selectedController); // Removes instance from GridManager.currentBlocks list

                    
                    if (selectedController != null && selectedController.gameObject != null)
                    {
                        Destroy(selectedController.gameObject); // Destroy the visual
                    } 

                    // Always update valid placements after a block is placed and removed,
                    // as the grid has changed (either by placement or line clear).
                    if (gridManager.currentBlocks.Count == 0)
                    {
                        if (gridManager.availableBlocks == null || gridManager.availableBlocks.Count == 0) { gridManager.availableBlocks = BlockShape.GetStandardShapes(); }
                        if (gridManager.availableBlocks.Count > 0) { gridManager.SpawnBlocks(3); }
                        PrecomputeValidPlacements(); // For newly spawned blocks
                    }
                    else
                    {
                        PrecomputeValidPlacements(); // For remaining blocks on the (potentially) modified grid
                    }

                    //Debug.Log("GetCumulativeReward: " + GetCumulativeReward());
                }
            }
            else
            { // ManualPlayMode - provide reward feedback based on hypothetical placement
                    if (boundaryTouchingCells == 5) // Indicates top or bottom edge for 5x1 block
                    {
                        AddReward(topBottomEdgeReward); // Using AddReward for consistency
                        Debug.Log($"(Manual) - Top/Bottom Edge reward: {topBottomEdgeReward}");
                    }
                    else // Indicates center placement
                    {
                        AddReward(centerPlacementPenalty); // Using AddReward for consistency
                        Debug.Log($"(Manual) - Center Placement penalty: {centerPlacementPenalty} (boundary cells: {boundaryTouchingCells})");
                    }
            
            }
           

            //Debug.Log("stepCounter: " + stepCounter);
        stepCounter++;
        

        
    }

    // Simplified evaluation for training guidance
    /*
    private float EvaluateActionForTraining(BlockShape shape, Vector2Int position, int blockIndex, int[,] currentGrid)
    {
        // Create a copy of the current grid
        int[,] simulatedGrid = new int[gridHeight, gridWidth];
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                simulatedGrid[y, x] = currentGrid[y, x];
            }
        }
        
        // Place the block
        foreach (Vector2Int offset in shape.cells)
        {
            Vector2Int cellPos = position + offset;
            if (cellPos.x >= 0 && cellPos.x < gridWidth && cellPos.y >= 0 && cellPos.y < gridHeight)
            {
                simulatedGrid[cellPos.y, cellPos.x] = 1;
            }
        }
        
        // Calculate adjacency and holes
        int adjacency = CalculateAdjacencyScore(shape, position);
        int holes = CalculateNewHoles(shape, position);
        float compactness = CalculateCompactness(shape, position);
        
        // Check for completed lines
        int completedLines = 0;
        for (int y = 0; y < gridHeight; y++)
        {
            bool isComplete = true;
            for (int x = 0; x < gridWidth; x++)
            {
                if (simulatedGrid[y, x] == 0)
                {
                    isComplete = false;
                    break;
                }
            }
            if (isComplete)
            {
                completedLines++;
            }
        }
        
        // Simple score calculation
        float score = 0;
        score += adjacency * adjacencyReward * 3;
        score += holes * holePenalty * 2;
        score += compactness * compactnessReward;
        score += completedLines * completedLines * lineClearMultiplier;
        
        return score;
    }
    */

    // New method: Evaluate board state with advanced metrics
    private float EvaluateBoardState(int[,] gridState)
    {
        if (gridState == null) return 0f;
        
        // Calculate basic metrics
        int holes = gridManager.CalculateHoles(gridState);
        int bumpiness = gridManager.CalculateBumpiness(gridState);
        
        // Calculate combined score - simple version
        float score = 0;
        score += holes * holePenalty * 2; // Holes are very bad
        
        // Only penalize significant bumpiness
        if (bumpiness > gridWidth / 2) { 
            // score += bumpinessPenalty;
        }
        
        return score;
    }
    
    // New method: Simulate placing a block and evaluate resulting state
    /*
    private float SimulatePlacement(BlockShape shape, Vector2Int position, int[,] currentGrid)
    {
        // Create a copy of the current grid
        int[,] simulatedGrid = new int[gridHeight, gridWidth];
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                simulatedGrid[y, x] = currentGrid[y, x];
            }
        }
        
        // Place the block in the simulated grid
        foreach (Vector2Int offset in shape.cells)
        {
            Vector2Int cellPos = position + offset;
            if (cellPos.x >= 0 && cellPos.x < gridWidth && cellPos.y >= 0 && cellPos.y < gridHeight)
            {
                simulatedGrid[cellPos.y, cellPos.x] = 1;
            }
        }
        
        // Check for completed lines and simulate clearing them
        List<int> completedLines = new List<int>();
        for (int y = 0; y < gridHeight; y++)
        {
            bool isComplete = true;
            for (int x = 0; x < gridWidth; x++)
            {
                if (simulatedGrid[y, x] == 0)
                {
                    isComplete = false;
                    break;
                }
            }
            if (isComplete)
            {
                completedLines.Add(y);
            }
        }
        
        // Clear completed lines
        if (completedLines.Count > 0)
        {
            // Sort lines in descending order for proper shifting
            completedLines.Sort((a, b) => b.CompareTo(a));
            
            foreach (int lineY in completedLines)
            {
                // Move all lines above down
                for (int y = lineY; y > 0; y--)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        simulatedGrid[y, x] = simulatedGrid[y - 1, x];
                    }
                }
                
                // Clear top line
                for (int x = 0; x < gridWidth; x++)
                {
                    simulatedGrid[0, x] = 0;
                }
            }
        }
        
        // Calculate base immediate rewards
        float immediateReward = 0;
        immediateReward += completedLines.Count * completedLines.Count * lineClearMultiplier;
        immediateReward += CalculateAdjacencyScore(shape, position) * adjacencyReward;
        immediateReward += CalculateNewHoles(shape, position) * holePenalty;
        immediateReward += CalculateCompactness(shape, position) * compactnessReward;
        
        // Evaluate resulting board state 
        float futureStateValue = EvaluateBoardState(simulatedGrid);
        
        // Look ahead further if enabled
        if (enableLookAhead && lookAheadDepth > 1)
        {
            float bestFutureReward = float.MinValue;
            int simulationsPerformed = 0;
            
            // Simulate a few possible next blocks
            // Use shapes from available blocks if possible, otherwise use standard shapes
            List<BlockShape> nextPossibleShapes = new List<BlockShape>();
            if (gridManager.availableBlocks != null && gridManager.availableBlocks.Count > 0)
            {
                nextPossibleShapes = gridManager.availableBlocks.Take(3).ToList();
            }
            else 
            {
                nextPossibleShapes = BlockShape.GetStandardShapes().Take(3).ToList();
            }
            
            // For each possible next block, find best placement
            foreach (BlockShape nextShape in nextPossibleShapes)
            {
                for (int y = 0; y < gridHeight && simulationsPerformed < maxSimulationsPerBlock; y++)
                {
                    for (int x = 0; x < gridWidth && simulationsPerformed < maxSimulationsPerBlock; x++)
                    {
                        Vector2Int nextPos = new Vector2Int(x, y);
                        
                        // Check if placement is valid in simulated grid
                        bool isValid = true;
                        foreach (Vector2Int offset in nextShape.cells)
                        {
                            Vector2Int cellPos = nextPos + offset;
                            if (cellPos.x < 0 || cellPos.x >= gridWidth || 
                                cellPos.y < 0 || cellPos.y >= gridHeight || 
                                simulatedGrid[cellPos.y, cellPos.x] == 1)
                            {
                                isValid = false;
                                break;
                            }
                        }
                        
                        if (isValid)
                        {
                            // Use reduced depth for future simulations
                            float futureReward = SimulatePlacementWithoutRecursion(nextShape, nextPos, simulatedGrid);
                            if (futureReward > bestFutureReward)
                            {
                                bestFutureReward = futureReward;
                            }
                            simulationsPerformed++;
                        }
                    }
                }
            }
            
            // Add discounted future reward
            if (bestFutureReward > float.MinValue)
            {
                futureStateValue += bestFutureReward * 0.7f; // Discount future rewards
            }
        }
        
        return immediateReward + futureStateValue;
    }
    
    // Non-recursive version to avoid stack overflow at deeper levels
    private float SimulatePlacementWithoutRecursion(BlockShape shape, Vector2Int position, int[,] currentGrid)
    {
        // Create a copy of the current grid
        int[,] simulatedGrid = new int[gridHeight, gridWidth];
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                simulatedGrid[y, x] = currentGrid[y, x];
            }
        }
        
        // Place the block
        foreach (Vector2Int offset in shape.cells)
        {
            Vector2Int cellPos = position + offset;
            if (cellPos.x >= 0 && cellPos.x < gridWidth && cellPos.y >= 0 && cellPos.y < gridHeight)
            {
                simulatedGrid[cellPos.y, cellPos.x] = 1;
            }
        }
        
        // Basic rewards without recursion
        float reward = 0;
        reward += CalculateAdjacencyScore(shape, position) * adjacencyReward;
        reward += CalculateNewHoles(shape, position) * holePenalty;
        reward += CalculateCompactness(shape, position) * compactnessReward;
        reward += EvaluateBoardState(simulatedGrid);
        
        return reward;
    }
    */
        
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0; discreteActions[1] = 0; discreteActions[2] = 0;

        // First, make sure the valid placements cache is up to date
        PrecomputeValidPlacements();
        
        // Fall back to simpler heuristic if look-ahead fails or is disabled
        float overallBestScore = float.MinValue;
        int bestBlockAction = 0; // Default to first block
        Vector2Int bestPlacementAction = Vector2Int.zero; // Default to 0,0
        bool foundAnyValidAction = false;

        System.Random random = new System.Random(); // Keep for tie-breaking or slight score variation
        int[,] currentGrid = gridManager.GetGridState(); // Get grid once

        foreach (var kvp in validPlacementsByBlockIndex) // Iterate through each available block
        {
            int blockIndex = kvp.Key;
            List<Vector2Int> validPlacementsForThisBlock = kvp.Value;
            
            if (validPlacementsForThisBlock.Count > 0)
            {
                BlockShape shape = gridManager.currentBlocks[blockIndex].shape;
                if (shape == null) continue; // Should not happen if validPlacements exist, but good check

                Vector2Int currentBlockBestPlacement = validPlacementsForThisBlock[0];
                float currentBlockBestScore = float.MinValue;
                
                // Add some randomness to placement by shuffling valid placements
                // Consider only a subset for performance, but ensure all are checked if few.
                int placementsToEvaluate = Mathf.Min(validPlacementsForThisBlock.Count, 10); // Evaluate up to 10 random placements
                List<Vector2Int> shuffledPlacements = validPlacementsForThisBlock
                    .OrderBy(x => random.Next())
                    .Take(placementsToEvaluate) 
                    .ToList();
                if (placementsToEvaluate < validPlacementsForThisBlock.Count && placementsToEvaluate > 0) {
                     // Ensure the potentially optimal first placement is always considered if we are sampling
                    if(!shuffledPlacements.Contains(validPlacementsForThisBlock[0])) {
                        shuffledPlacements[0] = validPlacementsForThisBlock[0];
                    }
                }

                foreach (Vector2Int placement in (shuffledPlacements.Count > 0 ? shuffledPlacements : validPlacementsForThisBlock)) // Iterate through placements for *this* block
                {
                    int adjacency = CalculateAdjacencyScore(shape, placement);
                    int holes = CalculateNewHoles(shape, placement);
                    int touchingCells = CountTouchingCells(shape, placement);
                    
                    // Scoring function with slight randomness
                    float score = (adjacency > 0 ? adjacency * adjacencyReward * 3 : misalignedPlacementPenalty * 2) + 
                                 holes * holePenalty * 2 +
                                 (touchingCells == 0 ? -compactnessReward * 2 : (touchingCells >= 2 ? compactnessReward : 0f)) + // Penalize isolation heavily, reward connection
                                 placement.y * 0.02f + // Slight preference for lower placements if all else is equal          
                                 (float)random.NextDouble() * 0.01f; // Reduced randomness impact
                                  
                    if (score > currentBlockBestScore)
                    {
                        currentBlockBestScore = score;
                        currentBlockBestPlacement = placement;
                    }
                }

                // After finding the best placement for *this block*, compare its score to the overall best found so far
                if (currentBlockBestScore > overallBestScore)
                {
                    overallBestScore = currentBlockBestScore;
                    bestBlockAction = blockIndex;
                    bestPlacementAction = currentBlockBestPlacement;
                    foundAnyValidAction = true;
                }
            }
        }

        if (foundAnyValidAction)
        {
            discreteActions[0] = bestBlockAction;
            discreteActions[1] = bestPlacementAction.x;
            discreteActions[2] = bestPlacementAction.y;
        }
        else
        {
            // This case should ideally be prevented by WriteDiscreteActionMask handling 
            // situations where no actions are possible (by ending the episode).
            // As a fallback, if we reach here, default to a (likely invalid) action to avoid errors,
            // though the game should have ended.
            discreteActions[0] = 0;
            discreteActions[1] = 0;
            discreteActions[2] = 0;
            Debug.LogWarning("[PlayerAgent.Heuristic] No valid action found by heuristic, defaulting. Episode should have ended via masking.");
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // Removed: var spec = this.ActionSpec;

        // Assumed branch sizes based on prior code structure and common setup
        const int numBlockChoiceActions = 3; // For branch 0, agent typically has 3 blocks to choose from
        // gridWidth for branch 1 (X position)
        // gridHeight for branch 2 (Y position)

        if (gridManager != null && gridManager.isClearingLines)
        {
            // Lines are clearing. Agent must wait. Provide a default valid action (0,0,0)
            // and ensure no other actions are available.

            // 1. Disable all actions in all assumed branches
            // Branch 0 (Block Choice)
            for (int i = 0; i < numBlockChoiceActions; i++) actionMask.SetActionEnabled(0, i, false);
            // Branch 1 (X position)
            if (gridWidth > 0) for (int i = 0; i < gridWidth; i++) actionMask.SetActionEnabled(1, i, false);
            // Branch 2 (Y position)
            if (gridHeight > 0) for (int i = 0; i < gridHeight; i++) actionMask.SetActionEnabled(2, i, false);

            // 2. Enable action 0 for each of the three main branches
            actionMask.SetActionEnabled(0, 0, true); // Block choice 0
            if (gridWidth > 0) actionMask.SetActionEnabled(1, 0, true); // X = 0
            if (gridHeight > 0) actionMask.SetActionEnabled(2, 0, true); // Y = 0
            
            return; // Do not proceed to check for placements or end episode
        }

        // --- Lines are NOT clearing. Proceed with normal action masking ---

        // 1. Initialize all actions as disabled based on assumed branch sizes
        // Branch 0 (Block Choice)
        for (int i = 0; i < numBlockChoiceActions; i++) actionMask.SetActionEnabled(0, i, false);
        // Branch 1 (X position)
        if (gridWidth > 0) for (int i = 0; i < gridWidth; i++) actionMask.SetActionEnabled(1, i, false);
        // Branch 2 (Y position)
        if (gridHeight > 0) for (int i = 0; i < gridHeight; i++) actionMask.SetActionEnabled(2, i, false);

        bool canPlaceAnyBlock = false;

        if (gridManager.currentBlocks != null && gridManager.currentBlocks.Count > 0)
        {
            foreach (var kvp in validPlacementsByBlockIndex) 
            {
                int blockActionIndex = kvp.Key; 
                List<Vector2Int> placements = kvp.Value;

                if (placements.Count > 0)
                {
                    // Ensure blockActionIndex is valid for branch 0 (assumed size numBlockChoiceActions)
                    if (blockActionIndex >= 0 && blockActionIndex < numBlockChoiceActions) 
                    {
                        actionMask.SetActionEnabled(0, blockActionIndex, true);
                        canPlaceAnyBlock = true;

                        foreach (Vector2Int pos in placements)
                        {
                            if (pos.x >= 0 && pos.x < gridWidth) { actionMask.SetActionEnabled(1, pos.x, true); }
                            if (pos.y >= 0 && pos.y < gridHeight) { actionMask.SetActionEnabled(2, pos.y, true); }
                        }
                    }
                }
            }

            if (!canPlaceAnyBlock)
            {
                Debug.Log("[PlayerAgent.WriteDiscreteActionMask] No valid placements for any current blocks. Ending episode.");
                AddClampedReward(gameOverPenalty - 0.2f); 
                //EndEpisode();
                
                // Provide a default action (0,0,0) even when ending episode.
                actionMask.SetActionEnabled(0, 0, true);
                if (gridWidth > 0) actionMask.SetActionEnabled(1, 0, true);
                if (gridHeight > 0) actionMask.SetActionEnabled(2, 0, true);
            }
        }
        else 
        {
            Debug.Log("[PlayerAgent.WriteDiscreteActionMask] No current blocks available to choose from. Ending episode.");
            AddClampedReward(gameOverPenalty - 0.3f); 
           // EndEpisode(); 
            
            // Provide a default action (0,0,0).
            actionMask.SetActionEnabled(0, 0, true);
            if (gridWidth > 0) actionMask.SetActionEnabled(1, 0, true);
            if (gridHeight > 0) actionMask.SetActionEnabled(2, 0, true);
        }
    }
}


