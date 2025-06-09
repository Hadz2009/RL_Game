using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Required for LINQ operations like OrderBy

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 8;
    public int gridHeight = 8;
    public float cellSize = 0.5125f; // Smaller cell size for better fit on screen
    [SerializeField] private GameObject cellPrefab;
    // [Range(0, 1)] public float gridFillThreshold = 0.4f; // Threshold to switch spawning strategy -- REMOVED, replaced by DDA
    
    [Header("Grid Position")]
    [SerializeField] private Vector2 gridOffset = new Vector2(-2.25f, -2.25f); // Centered for 4.5x4.5 grid
    [SerializeField] private bool useResponsiveLayout = true; // Enable responsive layout
    [SerializeField] [Range(0.1f, 0.3f)] private float mobileWidthAdjustment = 0.2f; // Adjust for mobile width
    
    [Header("Block Settings")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private Transform blocksContainer;
    [SerializeField] private Vector2 blockSpawnArea = new Vector2(0f, -2f); // Position for spawning blocks
    [SerializeField] public Vector3 initialBlockScale = new Vector3(0.1f, 0.1f, 0.1f); // Configurable initial scale
    
    [Header("Block Probability Controls")]
    [Tooltip("Add blocks here to control their spawn probability")]
    [SerializeField] private List<BlockProbability> customBlockProbabilities = new List<BlockProbability>();
    [Tooltip("When enabled, prevents the same block type from appearing twice")]
    [SerializeField] private bool preventDuplicateBlocks = true;
    [Tooltip("Allow 3x3 blocks to be duplicated when the board is mostly empty")]
    [SerializeField] private bool allow3x3DuplicatesWhenEmpty = true;
    [Tooltip("Maximum board fill percentage to allow 3x3 duplicates")]
    [Range(0.0f, 0.5f)]
    [SerializeField] private float emptyBoardThreshold = 0.2f; // Allow 3x3 duplicates when board is less than 20% full
    
    [SerializeField] [Tooltip("Spacing between blocks in the dock")] private float dockBlockSpacing = 1.5f; // Configurable horizontal spacing
    
    // Define a class to hold custom block probabilities
    [System.Serializable]
    public class BlockProbability
    {
        [Tooltip("The exact name of the block shape")]
        public string blockName;
        [Tooltip("1.0 = normal chance, lower values = less frequent")]
        [Range(0.1f, 1.0f)]
        public float probabilityMultiplier = 1.0f; // 1.0 = normal, lower values = less frequent
    }
    
    [Header("Game References")]
    [SerializeField] private GameManager gameManager; // Ensure this is assigned in Inspector
    private DifficultyManager difficultyManager; // Reference to the DifficultyManager
    
    [Header("Manual Play Settings")] // Added for manual play
    [SerializeField] private LayerMask blockClickLayerMask; 
    [SerializeField] private LayerMask gridCellClickLayerMask;
    private BlockController selectedManualBlock = null;
    private bool humanMadeAPlacementThisTurn = false;

    public bool HumanMadeAPlacementThisTurn // Public accessor for Agent
    {
        get { return humanMadeAPlacementThisTurn; }
        set { humanMadeAPlacementThisTurn = value; }
    }
    
    // Grid representation: 0 = empty, 1 = filled
    private int[,] grid;
    private GameObject[,] gridCells;
    private Color[,] gridColors; // Add color tracking for each cell
    
    // Current blocks
    public List<BlockShape> availableBlocks = new List<BlockShape>();
    public List<BlockController> currentBlocks = new List<BlockController>();
    
    [Header("Scoring")]
    public int pointsPerLine = 100;
    public int comboMultiplier = 10;
    
    [Header("DDA Block Categorization")]
    // Define block shape names in Inspector. Add names from your BlockShape assets.
     private List<string> easyShapeNames = new List<string> {
        
        "CreateHorizontalDuo", 
        "CreateVerticalDuo", 
        "CreateTriBlock", 
        "CreateVerticalTriple", 
        "CreateCorner2x2",
        "CreateCorner2x2Inverse"
    }; 
     private List<string> mediumShapeNames = new List<string> {
        "CreateSquare",
        "CreateVerticalQuadruple",
        "CreateHorizontalQuadruple",
        "CreateLShape",
        "CreateJShape",
        "Create2x3Rectangle",
        "Create3x2Rectangle"
    
        
    }; 
     private List<string> hardShapeNames = new List<string> {
        "Create3x3",
        "Create5x1",
        "Create1x5",
        
        "CreateReverseTShape",
        "CreateTShape",
        "CreateSShape",
        "CreateZShape",
        "CreateRightTShape",
        "CreateLeftTShape",
        "CreateNShape",
        "CreateZShapeInverse",
        //"CreateCorner3x3",
        //"CreateCorner3x3Inverse",
        
        
        
    };
    
    // Additional categorization by shape type for ensuring variety
    [Header("Shape Type Categories")]
    [SerializeField] private List<string> singleLineShapes = new List<string> {
        "CreateHorizontalDuo", "CreateVerticalDuo", "CreateTriBlock", "CreateVerticalTriple", 
        "CreateHorizontalQuadruple", "CreateVerticalQuadruple", "Create5x1", "Create1x5"
    };
    
    [SerializeField] private List<string> squareRectangleShapes = new List<string> {
        "CreateSquare", "Create3x3", "Create2x3Rectangle", "Create3x2Rectangle"
    };
    
    [SerializeField] private List<string> lShapes = new List<string> {
        "CreateLShape", "CreateJShape", "CreateTShape", "CreateReverseTShape"
    };
    
    [SerializeField] private List<string> tShapes = new List<string> {
        "CreateTShape", "CreateReverseTShape", "CreateRightTShape", "CreateLeftTShape"
    };
    
    [SerializeField] private List<string> szShapes = new List<string> {
        "CreateSShape", "CreateZShape", "CreateZShapeInverse", "CreateNShape"
    };
    
    [SerializeField] private List<string> cornerShapes = new List<string> {
        "CreateCorner2x2", "CreateCorner2x2Inverse"
        //"CreateCorner3x3",
        //"CreateCorner3x3Inverse"
    };
    
    // Board filling thresholds for early/late game strategies
    [Header("Board Filling Strategies")]
    [Range(0, 1)] public float earlyGameThreshold = 0.3f; // When board is less than 30% full: give big blocks to fill it quickly
    [Range(0, 1)] public float lateGameThreshold = 0.6f; // When board is more than 60% full: prioritize line-clearing blocks
    
    // Fun factor parameters
    [Header("Game Balance Settings")]
    [Range(0, 1)] public float clearingPotentialWeight = 0.8f; // How strongly to prioritize blocks that can clear lines (higher = more line-clearing blocks)
    [Range(0, 1)] public float varietyWeight = 0.4f; // How much to prioritize different types of blocks (higher = more variety)
    [SerializeField] private int consecutiveBlocksLimit = 3; // Avoid giving too many of the same category in a row
    
    // Time-based difficulty ramping
    [Header("Time-Based Difficulty")]
    [Range(0, 1)] public float funGameDuration = 0.85f; // First 85% of game is fun, line-clearing focused
    [Range(0, 1)] public float hardGameThreshold = 0.90f; // At 90%, game gets harder with fewer line-clearing blocks
    [Range(0, 1)] public float brutalGameThreshold = 0.95f; // At 95%, only hard blocks (was endGameForceThreshold)
    
    // Track recently given block categories
    private List<string> recentBlockCategories = new List<string>();
    
    // DDA Metrics Tracking
    private float timeSinceLastClear = 0f;
    private bool blockPlacedSinceLastMetricUpdate = false;
    
    // Tracks the names of the previously offered set of 3 blocks to prevent exact repetition
    private HashSet<string> lastOfferedBlockSetNames = new HashSet<string>();

    // --- ML-Agent Integration ---
    public int lastLinesCleared = 0; // Track lines cleared by the last placed block
    // --- End ML-Agent Integration ---

    // --- Public Accessors for Agent ---
    public int Width => gridWidth;
    public int Height => gridHeight;
    // --- End Public Accessors ---
    private int activeClearOperations = 0;
    public bool isClearingLines
    {
        get
        {
            return activeClearOperations > 0;
        }
    }

    public bool isPlaced = false;
    
    private Transform gridContainerTransform; // Added field to store grid container's transform
    
    private void Awake()
    {
        grid = new int[gridWidth, gridHeight];
        gridCells = new GameObject[gridWidth, gridHeight];
        gridColors = new Color[gridWidth, gridHeight]; // Initialize the colors array
        
        // REMOVED Responsive Layout Check
        // if (useResponsiveLayout)
        // {
        //     AdjustForScreenAspect();
        // }
    }
    
    void Start()
    {
        // Get the DifficultyManager instance
        difficultyManager = FindObjectOfType<DifficultyManager>(); // Changed from DifficultyManager.Instance
        if (difficultyManager == null)
        {
            Debug.LogError("DifficultyManager instance not found in this scene! Make sure a DifficultyManager exists and is active."); // Updated error message
            enabled = false;
            return;
        }

        ////Debug.log("GridManager Start called");
        CreateGrid();
        
        StartCoroutine(WaitForBlocksAndSpawn());
    }
    
    void Update()
    {
        // Increment time since last clear if no block has been placed this frame (or since last check)
        // Only increment if the game is actually playing (e.g., not paused, not game over)
        if (gameManager != null && gameManager.IsGameActive() && !blockPlacedSinceLastMetricUpdate) // Assuming GameManager has IsGameActive()
        {
            timeSinceLastClear += Time.deltaTime;
        }
        // Reset the flag after checking
        blockPlacedSinceLastMetricUpdate = false;

        // Handle manual input if in manual play mode and game is active
        if (gameManager != null && gameManager.isManualPlayMode && gameManager.IsGameActive())
        {
            HandleManualInput();
        }
    }
    
    private void HandleManualInput()
    {
        if (Input.GetMouseButtonDown(0) && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Try to select a block from the dock
            // Ensure blocks in the dock have colliders and are on the 'blockClickLayerMask' layer.
            if (selectedManualBlock == null) // Only try to select a new block if one isn't already selected
            {
                if (Physics.Raycast(ray, out hit, 100f, blockClickLayerMask))
                {
                    BlockController clickedBlock = hit.collider.GetComponent<BlockController>();
                    if (clickedBlock != null && currentBlocks.Contains(clickedBlock))
                    {
                        selectedManualBlock = clickedBlock;
                        // Optional: Add visual feedback for selected block (e.g., highlight)
                        //Debug.Log($"[Manual Play] Selected block: {selectedManualBlock.shape.shapeName}");
                        return; // Processed block selection click, wait for placement click
                    }
                }
            }

            // Try to place a selected block onto the grid or deselect
            if (selectedManualBlock != null)
            {
                // First, check if clicking on the selected block again to deselect
                if (Physics.Raycast(ray, out hit, 100f, blockClickLayerMask))
                {
                    BlockController clickedBlock = hit.collider.GetComponent<BlockController>();
                    if (clickedBlock == selectedManualBlock)
                    {
                        //Debug.Log($"[Manual Play] Deselected block: {selectedManualBlock.shape.shapeName}");
                        selectedManualBlock = null;
                        // Optional: Remove visual feedback for deselection
                        return;
                    }
                }

                // If not deselecting, try to place on grid
                // Ensure grid cells (or a grid plane) have colliders and are on the 'gridCellClickLayerMask' layer.
                if (Physics.Raycast(ray, out hit, 100f, gridCellClickLayerMask))
                {
                    Vector2Int gridPos = WorldToGridPosition(hit.point);

                    if (CanPlaceBlock(selectedManualBlock.shape, gridPos))
                    {
                        //Debug.Log($"[Manual Play] Attempting to place {selectedManualBlock.shape.shapeName} at {gridPos}");
                        if (TryPlaceBlock(selectedManualBlock.shape, gridPos)) // TryPlaceBlock already handles scoring, line clearing etc.
                        {
                            //Debug.Log($"[Manual Play] Placed {selectedManualBlock.shape.shapeName} at {gridPos}.");
                            BlockController placedBlock = selectedManualBlock; // Keep a reference before nulling
                            
                            RemoveBlock(placedBlock); // Removes from currentBlocks list
                            if (placedBlock.gameObject != null) Destroy(placedBlock.gameObject); // Destroy the visual

                            selectedManualBlock = null; // Clear selection
                            HumanMadeAPlacementThisTurn = true; // Signal to Agent that a move was made this turn
                        }
                        else
                        {
                            //Debug.LogWarning($"[Manual Play] Could not place {selectedManualBlock.shape.shapeName} at {gridPos} (TryPlaceBlock failed). Possible race condition or invalid state.");
                        }
                    }
                    else
                    {
                        //Debug.LogWarning($"[Manual Play] Invalid placement location {gridPos} for {selectedManualBlock.shape.shapeName} (CanPlaceBlock failed). Click elsewhere or deselect block.");
                    }
                }
                // If player clicks outside the grid or selectable blocks while a block is selected, nothing happens (block remains selected)
                // To deselect, they must click the selected block again.
            }
        }
    }
    
    private IEnumerator WaitForBlocksAndSpawn()
    {
        ////Debug.log("Waiting for blocks to be available...");
        while (availableBlocks.Count == 0)
        {
            yield return null;
        }
        
        ////Debug.log($"Blocks available! Count: {availableBlocks.Count}");
        
        // Randomize the available blocks list to ensure varied starts
        ShuffleBlocksList();
        
        ////Debug.log("Spawning initial blocks...");
        SpawnBlocks(3); // Start with 3 blocks
    }
    
    void CreateGrid()
    {
        // Create parent container for grid cells
        GameObject gridContainer = new GameObject("GridContainer");
        gridContainer.transform.SetParent(transform);
        
        // Position the grid container locally using gridOffset
        gridContainer.transform.localPosition = new Vector3(gridOffset.x, gridOffset.y, 0);
        this.gridContainerTransform = gridContainer.transform; // Store the transform
        
        // Calculate grid dimensions for centering
        float totalWidth = gridWidth * cellSize;
        float totalHeight = gridHeight * cellSize;
        
        // Calculate starting position to center the grid
        float startX = -(totalWidth / 2f) + (cellSize / 2f);
        float startY = -(totalHeight / 2f) + (cellSize / 2f);
        
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // Position with offset from grid container and centered
                Vector3 cellLocalPosition = new Vector3(
                    startX + (x * cellSize),
                    startY + (y * cellSize),
                    0
                );
                GameObject cell = Instantiate(cellPrefab); // Instantiate first
                cell.transform.SetParent(gridContainer.transform); // Then parent
                cell.transform.localPosition = cellLocalPosition; // Then set local position
                cell.transform.localRotation = Quaternion.identity; // Ensure default rotation
                cell.name = $"Cell [{x}, {y}]";
                gridCells[x, y] = cell;
                
                // Initialize grid
                grid[x, y] = 0;
            }
        }
    }
    
    // New method to handle screen aspect ratio adjustments
    
    
    public void SpawnBlocks(int count = 1)
    {
        // SIMPLE MODE: BlockProvider is DISABLED - always spawn blocks normally
        // All BlockProvider integration code has been removed for simplicity
        
        // --- Prevent Set Repetition --- 
        // Before spawning a new set, record the names of the current set (if any)
        if (currentBlocks.Count > 0)
        {   
            lastOfferedBlockSetNames.Clear();
            foreach (var block in currentBlocks)
            {
                if (block != null && block.shape != null)
                {
                     lastOfferedBlockSetNames.Add(block.shape.shapeName);
                }
            }
             ////Debug.log($"Recorded previous block set: [{string.Join(", ", lastOfferedBlockSetNames)}]");
        }
        
        for (int i = 0; i < count; i++)
        {
            if (currentBlocks.Count < 3)
            {
                // Removed game over condition check
                ////Debug.log($"Spawning block {i + 1}");
                SpawnBlock();
            }
        }
    }
    
    private void SpawnBlock()
    {
        if (availableBlocks.Count == 0)
        {
            //Debug.LogError("No block shapes available!");
            return;
        }
        
        // Calculate metrics and update Difficulty Manager *before* selecting the next block
        GridMetrics currentMetrics = GetGridStateMetrics();
        if (difficultyManager != null)
        {
            difficultyManager.UpdateStruggleScore(currentMetrics);
        }
        else
        {
             //Debug.LogError("Difficulty Manager is null during SpawnBlock!");
             // Handle error or fallback to non-DDA logic if necessary
        }

        // Keep track of shapes that have been tried to prevent infinite loops
        HashSet<string> triedShapes = new HashSet<string>();
        BlockShape blockShape = null;
        int attempts = 0;
        
        // --- Prevent Set Repetition Logic --- 
        int maxAttempts = 10; // Increased attempts to avoid repeating the last set
        HashSet<string> triedExclusions = new HashSet<string>(); // Track shapes excluded in attempts

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Get candidate shape, potentially excluding some
            BlockShape candidateShape = GetNextBlockShape(currentBlocks, triedExclusions.Count > 0 ? triedExclusions : null);
            
            if (candidateShape == null)
            {
                //Debug.LogError($"GetNextBlockShape returned null on attempt {attempt + 1}. Cannot spawn block.");
                return; // Critical error, stop spawning
            }

            // Check for set repetition *only* when spawning the third block
            if (currentBlocks.Count == 2)
            {
                // Form the potential new set
                HashSet<string> potentialNewSet = new HashSet<string>(currentBlocks.Select(b => b.shape.shapeName))
                {
                    candidateShape.shapeName
                };

                // Compare with the last offered set
                if (potentialNewSet.SetEquals(lastOfferedBlockSetNames))
                {
                     // Repetition detected!
                     if (attempt < maxAttempts - 1)
                     {
                         // If not the last attempt, exclude this shape and try again
                         //Debug.LogWarning($"Attempt {attempt + 1}/{maxAttempts}: Potential set repetition with \'{candidateShape.shapeName}\'. Excluding and retrying. Last set: [{string.Join(", ", lastOfferedBlockSetNames)}]. Potential: [{string.Join(", ", potentialNewSet)}]");
                         triedExclusions.Add(candidateShape.shapeName);
                         continue; // Go to the next iteration of the for loop
                     }
                     else
                     {
                         // Last attempt, allow the repetition
                         //Debug.LogWarning($"Attempt {attempt + 1}: Could not avoid set repetition with '{candidateShape.shapeName}'. Allowing repetition.");
                         blockShape = candidateShape; // Accept the shape
                         break; // Exit the loop
                     }
                }
                else
                {
                    // Not a repetition, accept the shape
                    blockShape = candidateShape;
                    break; // Exit the loop
                }
            }
            else
            {
                // Not the third block, no need to check for set repetition yet
                blockShape = candidateShape;
                break; // Exit the loop
            }
        }
        // --- End Prevent Set Repetition Logic ---
        
        // If blockShape is still null after attempts (shouldn't happen unless GetNextBlockShape failed critically)
        if (blockShape == null)
        {
            //Debug.LogError("Failed to determine a block shape after multiple attempts. Spawning process halted.");
            return;
        }

        ////Debug.log($"Spawning block with shape: {blockShape.shapeName}");
        
        // Calculate LOCAL spawn position (spread blocks horizontally)
        float spacing = dockBlockSpacing; // Use inspector-configurable dock spacing
        float xPosition = blockSpawnArea.x + (currentBlocks.Count) * spacing; 
        Vector3 localSpawnPosition = new Vector3(xPosition, blockSpawnArea.y, 0); // This is relative to blocksContainer
        
        // Instantiate and set up block
        if (blockPrefab == null)
        {
            //Debug.LogError("Block Prefab not assigned!");
            return;
        }
        
        GameObject blockObj = Instantiate(blockPrefab); // Instantiate without specific world position/rotation yet

        if (blocksContainer != null) 
        {
            blockObj.transform.SetParent(blocksContainer);
            blockObj.transform.localPosition = localSpawnPosition;
            blockObj.transform.localRotation = Quaternion.identity; // Ensure consistent local rotation
        }
        else
        {
            // Fallback: if blocksContainer isn't assigned, parent to GridManager's transform
            // and use localSpawnPosition relative to GridManager. This might not be ideal for layout.
            //Debug.LogWarning("blocksContainer is not assigned in GridManager. Spawning block relative to GridManager's root. Consider assigning blocksContainer as a child of GridManager.");
            blockObj.transform.SetParent(transform); // Parent to the GridManager itself
            blockObj.transform.localPosition = localSpawnPosition;
            blockObj.transform.localRotation = Quaternion.identity;
        }

        blockObj.transform.localScale = initialBlockScale; 
        
        // if (blocksContainer != null) { blockObj.transform.SetParent(blocksContainer); } // Old parenting logic removed
        
        BlockController controller = blockObj.GetComponent<BlockController>();
        if (controller == null)
        {
            //Debug.LogError("BlockController component not found on block prefab!");
            Destroy(blockObj);
            return;
        }
        
        controller.Initialize(blockShape, this);
        currentBlocks.Add(controller);
        ////Debug.log($"Block spawned successfully. Total blocks: {currentBlocks.Count}");
    }
    /// <summary>
    ///--- End Spawn Block ---
    /// </summary>
    /// <param name="blocksInCurrentSpawnBatch"></param>
    /// <param name="excludedShapeNames"></param>
    /// <returns></returns>
    //************************************************************************************************************************************************************  
    private BlockShape GetNextBlockShape(List<BlockController> blocksInCurrentSpawnBatch, HashSet<string> excludedShapeNames = null)
    {
        // --- Determine if this is the very first set of blocks --- 
        bool isInitialSpawn = true;
        for (int x = 0; x < gridWidth; x++) {
            for (int y = 0; y < gridHeight; y++) {
                if (grid[x, y] != 0) {
                    isInitialSpawn = false; 
                    break;
                }
            }
            if (!isInitialSpawn) break;
        }
        if (isInitialSpawn && blocksInCurrentSpawnBatch.Count >= 3) { // Should not happen, but safety check
             isInitialSpawn = false;
        }
        // ---------------------------------------------------------

        // Calculate current board density
        float currentBoardDensity = CalculateBoardDensity();
        // ////Debug.log($"Board Density: {currentBoardDensity:P1}"); // Reduced logging noise

        // Get time pressure - used for end game
        float timePressureBias = difficultyManager.GetTimePressureBias(); // Value from 0 (start) to 1 (end game)

        // --- Initial Candidate Filtering (For non-Brutal modes mainly) ---
        // Filter based on blocks currently being offered in the dock for this spawn cycle
        var existingShapes = blocksInCurrentSpawnBatch.Select(b => b.shape).ToHashSet();
        var existingShapeNames = blocksInCurrentSpawnBatch.Select(b => b.shape.shapeName).ToHashSet();
        HashSet<string> offeredShapeTypeCategories = new HashSet<string>();
        foreach (var block in blocksInCurrentSpawnBatch)
        {
            if (block.shape != null) { offeredShapeTypeCategories.Add(GetShapeTypeCategory(block.shape)); }
        }
        // ////Debug.log($"Offered Categories: {string.Join(", ", offeredShapeTypeCategories)}");

        List<BlockShape> baseCandidateShapes = availableBlocks;
        bool exclusionsAppliedToBase = false; // Track if exclusions were applied
        // Apply temporary exclusions FIRST if provided (for set repetition avoidance)
        if (excludedShapeNames != null && excludedShapeNames.Count > 0)
        {
            baseCandidateShapes = availableBlocks
                .Where(b => !excludedShapeNames.Contains(b.shapeName))
                .ToList();
            ////Debug.log($"Applied temporary exclusions: {string.Join(", ", excludedShapeNames)}. Base candidates remaining: {baseCandidateShapes.Count}");
            if (baseCandidateShapes.Count == 0) {
                 //Debug.LogWarning("Temporary exclusions removed all candidates! Reverting to full available list.");
                 baseCandidateShapes = availableBlocks.ToList(); // Fallback if exclusions removed everything
            }
            exclusionsAppliedToBase = true; // Mark that exclusions have been processed for the base list
        }

        // Attempt strict filtering first (prevent same name AND same category type based on current batch)
        List<BlockShape> candidateShapesStrict = baseCandidateShapes
            .Where(b => !existingShapes.Contains(b) &&
                   (!preventDuplicateBlocks || !existingShapeNames.Contains(b.shapeName) ||
                    (b.shapeName == "3x3" && allow3x3DuplicatesWhenEmpty && currentBoardDensity < emptyBoardThreshold)) &&
                   !offeredShapeTypeCategories.Contains(GetShapeTypeCategory(b)))
            .ToList();

        List<BlockShape> candidateShapes; // This list is used for Fun/Hard modes
        if (candidateShapesStrict.Count > 0)
        {
            candidateShapes = candidateShapesStrict;
            // ////Debug.log($"Using strict filtering: {candidateShapes.Count} candidates remaining.");
        }
        else
        {
            // Fallback: Allow category duplicates if strict filtering yields nothing
            // //Debug.LogWarning("Strict filtering yielded no candidates. Falling back to only prevent exact duplicates (if enabled).");
            candidateShapes = baseCandidateShapes
            .Where(b => !existingShapes.Contains(b) && 
                   (!preventDuplicateBlocks || !existingShapeNames.Contains(b.shapeName) || 
                    (b.shapeName == "3x3" && allow3x3DuplicatesWhenEmpty && currentBoardDensity < emptyBoardThreshold)))
            .ToList();
        
             if (candidateShapes.Count == 0 && baseCandidateShapes.Count > 0)
             {
                 // //Debug.LogWarning("Fallback filtering also yielded no candidates. Allowing full duplicates from base list.");
                 candidateShapes = baseCandidateShapes.Where(b => !existingShapes.Contains(b)).ToList();
                 if(candidateShapes.Count == 0) {
                     //Debug.LogWarning("Even allowing duplicates yielded no candidates based on existingShapes filter. Using raw baseCandidateShapes.");
                     candidateShapes = baseCandidateShapes.ToList(); // Absolute fallback (already filtered by exclusions if necessary)
                 }
             }
        }
        // --- End Initial Filtering ---

        // Categorize ALL available blocks for potential use in Brutal mode
        HashSet<string> easyNames = new HashSet<string>(easyShapeNames);
        HashSet<string> mediumNames = new HashSet<string>(mediumShapeNames);
        HashSet<string> hardNames = new HashSet<string>(hardShapeNames);
        var allHardShapes = availableBlocks.Where(s => hardNames.Contains(s.shapeName)).ToList();
        var allMediumShapes = availableBlocks.Where(s => mediumNames.Contains(s.shapeName)).ToList();
        var allEasyShapes = availableBlocks.Where(s => easyNames.Contains(s.shapeName)).ToList();

        // Categorize the filtered `candidateShapes` for Fun/Hard modes
        var easyShapes = candidateShapes.Where(s => easyNames.Contains(s.shapeName)).ToList();
        var mediumShapes = candidateShapes.Where(s => mediumNames.Contains(s.shapeName)).ToList();
        var hardShapes = candidateShapes.Where(s => hardNames.Contains(s.shapeName)).ToList();

        // Additional shape type categorization (using filtered candidates)
        HashSet<string> lineNames = new HashSet<string>(singleLineShapes);
        HashSet<string> squareRectNames = new HashSet<string>(squareRectangleShapes);
        HashSet<string> lNames = new HashSet<string>(lShapes);
        HashSet<string> tNames = new HashSet<string>(tShapes);
        HashSet<string> szNames = new HashSet<string>(szShapes);
        HashSet<string> cornerNames = new HashSet<string>(cornerShapes);

        var lineShapes = candidateShapes.Where(s => lineNames.Contains(s.shapeName)).ToList();
        var squareRectShapes = candidateShapes.Where(s => squareRectNames.Contains(s.shapeName)).ToList();
        var lJShapes = candidateShapes.Where(s => lNames.Contains(s.shapeName)).ToList();
        var tBlockShapes = candidateShapes.Where(s => tNames.Contains(s.shapeName)).ToList();
        var szBlockShapes = candidateShapes.Where(s => szNames.Contains(s.shapeName)).ToList();
        var cornerBlockShapes = candidateShapes.Where(s => cornerNames.Contains(s.shapeName)).ToList();
        
        // --- END GAME BRUTAL OVERRIDE --- 
        if (timePressureBias >= brutalGameThreshold && allHardShapes.Count > 0)
        {
            ////Debug.log("BRUTAL MODE ----- ");
            
            // 1. Start with ALL defined hard shapes that are available
            List<BlockShape> currentHardPool = allHardShapes.ToList();
            ////Debug.log($"Initial hard pool size: {currentHardPool.Count}");

            // 2. Filter out shapes already in the dock (respecting preventDuplicateBlocks)
            if (preventDuplicateBlocks && blocksInCurrentSpawnBatch.Count > 0)
            {
                HashSet<string> brutalExistingShapeNames = blocksInCurrentSpawnBatch.Select(b => b.shape.shapeName).ToHashSet();
                currentHardPool = currentHardPool
                    .Where(s => !brutalExistingShapeNames.Contains(s.shapeName) ||
                           (s.shapeName == "3x3" && allow3x3DuplicatesWhenEmpty && currentBoardDensity < emptyBoardThreshold))
                    .ToList();
                ////Debug.log($"Hard pool size after duplicate filter: {currentHardPool.Count}");
            }

            // 3. Apply temporary exclusions (for set repetition)
            if (excludedShapeNames != null && excludedShapeNames.Count > 0)
            {
                // Only apply if they weren't already applied to the source list (availableBlocks)
                if (!exclusionsAppliedToBase) 
                { 
                    currentHardPool = currentHardPool
                        .Where(b => !excludedShapeNames.Contains(b.shapeName))
                        .ToList();
                    ////Debug.log($"Brutal Mode: Applied temp exclusions to hard pool: {string.Join(", ", excludedShapeNames)}. Count: {currentHardPool.Count}");
                }
                 // ////Debug.log($"Hard pool size after temp exclusions: {currentHardPool.Count}"); // Reduced logging
            }

            // --- NEW Brutal Selection Logic: Random from Filtered Hard Pool --- 
            if (currentHardPool.Count > 0)
            {
                // 4. Select a block from the filtered hard pool using WEIGHTED random based on custom probabilities
                Dictionary<BlockShape, float> weightedHardPool = new Dictionary<BlockShape, float>();
                float totalWeight = 0f;
                foreach (var shape in currentHardPool)
                {
                    float weight = 1.0f;
                    foreach (var blockProb in customBlockProbabilities)
                    {
                        if (blockProb.blockName == shape.shapeName)
                        {
                            weight = Mathf.Max(0.01f, blockProb.probabilityMultiplier); // Use custom prob, ensure weight > 0
                            break;
                        }
                    }
                    weightedHardPool[shape] = weight;
                    totalWeight += weight;
                }

                BlockShape chosenHardShape = null;
                if (totalWeight > 0 && weightedHardPool.Count > 0)
                {
                    float randomPoint = Random.value * totalWeight;
                    float accumulatedWeight = 0f;
                    foreach (var kvp in weightedHardPool)
                    {
                        accumulatedWeight += kvp.Value;
                        if (randomPoint <= accumulatedWeight)
                        {
                            chosenHardShape = kvp.Key;
                            break;
                        }
                    }
                    // Fallback if rounding issues occur
                    if (chosenHardShape == null) chosenHardShape = weightedHardPool.Keys.Last(); 
                }
                else 
                {   // If total weight is zero or pool empty after weighting, pick randomly from original filtered pool
                    //Debug.LogWarning("Brutal Mode: Total weight in hard pool is zero or pool empty. Picking random hard block.");
                    if (currentHardPool.Count > 0) { // Check again, pool might be different now
                         chosenHardShape = currentHardPool[Random.Range(0, currentHardPool.Count)];
                    } else {
                        // This case means the pool was empty *before* weighting - should trigger fallback below
                         //Debug.LogError("Brutal Mode Critical: currentHardPool became empty unexpectedly.");
                    }
                }
                
                if (chosenHardShape != null) 
                {
                    ////Debug.log($"Brutal mode: Weighted random selected hard shape: {chosenHardShape.shapeName}");
                    // 5. Prioritize returning this hard block, even if unplaceable
                    UpdateRecentCategories(chosenHardShape, easyNames, mediumNames, hardNames);
                    return chosenHardShape; 
                } else {
                     //Debug.LogWarning("Brutal Mode: Failed to select a shape via weighted random. Proceeding to fallbacks.");
                     // Force triggering the fallback logic below if chosenHardShape is somehow null
                }
            }
            // --- Fallback: Filtered Hard Pool is Empty or Selection Failed ---
            //Debug.LogWarning("Brutal mode: Filtered hard pool is empty OR weighted selection failed. Proceeding to fallbacks.");
            
            // 7. Extreme Fallback: Try finding the least helpful placeable block among ALL available shapes.
             var allAvailableFiltered = availableBlocks.ToList(); // Start with all available
             // Apply exclusions if needed (if not applied to base list earlier)
             if (excludedShapeNames != null && excludedShapeNames.Count > 0 && !exclusionsAppliedToBase) {
                 allAvailableFiltered = allAvailableFiltered.Where(b => !excludedShapeNames.Contains(b.shapeName)).ToList();
             }
             // Now filter based on current dock items
             allAvailableFiltered = allAvailableFiltered
                .Where(s => !blocksInCurrentSpawnBatch.Select(b => b.shape.shapeName).Contains(s.shapeName) ||
                       (s.shapeName == "3x3" && allow3x3DuplicatesWhenEmpty && currentBoardDensity < emptyBoardThreshold))
                .ToList();
             
             if (allAvailableFiltered.Count == 0) allAvailableFiltered = availableBlocks.ToList(); // Ultimate fallback

             var sortedAllByClearing = allAvailableFiltered.OrderBy(s => EvaluateLineClearingPotential(s)).ToList();
             BlockShape leastHelpfulPlaceable = FindSimplestPlaceableBlock(sortedAllByClearing);

             if (leastHelpfulPlaceable != null)
             {
                //Debug.LogWarning($"Brutal fallback 1 (CRITICAL): Offering least helpful placeable block from *all* types: {leastHelpfulPlaceable.shapeName}");
                UpdateRecentCategories(leastHelpfulPlaceable, easyNames, mediumNames, hardNames);
                return leastHelpfulPlaceable;
        }
        else
        {
                 // 8. Ultimate Fallback: Truly nothing placeable left anywhere. Give the first available block.
                 //Debug.LogError("CRITICAL (Brutal): No placeable shapes found AT ALL! Returning first available block.");
                 BlockShape finalResortBlock = availableBlocks.FirstOrDefault();
                  if (finalResortBlock != null) UpdateRecentCategories(finalResortBlock, easyNames, mediumNames, hardNames);
                 return finalResortBlock;
             }
             // --- End Fallbacks --- 
        }
         // --- End Brutal Mode --- 

        // --- Apply exclusions to Fun/Hard candidate list if not done earlier ---
        if (excludedShapeNames != null && excludedShapeNames.Count > 0 && !exclusionsAppliedToBase)
        {
            candidateShapes = candidateShapes
                .Where(b => !excludedShapeNames.Contains(b.shapeName))
                .ToList();
            ////Debug.log($"Fun/Hard Mode: Applied temp exclusions: {string.Join(", ", excludedShapeNames)}. Candidates remaining: {candidateShapes.Count}");
        }

        // --- Fun / Hard Mode Logic (Uses `candidateShapes` which were pre-filtered) ---
        if (candidateShapes.Count == 0) { 
            //Debug.LogError("No candidate shapes available for selection (Fun/Hard Mode)! Check filtering."); 
            // Attempt to return *any* available block as an emergency fallback
            BlockShape emergencyBlock = baseCandidateShapes.FirstOrDefault() ?? availableBlocks.FirstOrDefault(); // Use base list first
            if (emergencyBlock != null) UpdateRecentCategories(emergencyBlock, easyNames, mediumNames, hardNames);
            return emergencyBlock;
        }

        // --- Override for Initial Spawn Randomness ---
        if (isInitialSpawn)
        {
             ////Debug.log("Initial Spawn: Selecting random block.");
             // Filter available blocks to exclude those already chosen for the initial set
             var initialCandidates = availableBlocks
                 .Where(b => !blocksInCurrentSpawnBatch.Select(blk => blk.shape.shapeName).Contains(b.shapeName))
                 .ToList();
             
             // Also apply temporary exclusions if any
             if (excludedShapeNames != null && excludedShapeNames.Count > 0) {
                initialCandidates = initialCandidates.Where(b => !excludedShapeNames.Contains(b.shapeName)).ToList();
             }

             if (initialCandidates.Count > 0)
             {
                 BlockShape randomInitialBlock = initialCandidates[Random.Range(0, initialCandidates.Count)];
                 ////Debug.log($"Initial Spawn: Chose '{randomInitialBlock.shapeName}'");
                 // No need to update recent categories here as it bypasses normal logic
                 return randomInitialBlock;
             }
             else
             {
                 //Debug.LogWarning("Initial Spawn: No unique candidates left! Falling back to normal logic.");
                 // Fall through to normal logic if filtering left nothing (should be rare)
             }
        }
        // --- End Initial Spawn Override ---
        
        // --- FUN MAIN GAMEPLAY (FIRST 85-90% OF THE GAME) ---
        // Evaluate all blocks for their fun potential (clearing lines, filling board strategically)
        if (timePressureBias < hardGameThreshold)
        {
            // Prioritize shapes from categories not currently offered
            List<BlockShape> shapesFromMissingCategories = new List<BlockShape>();
            
            if (!offeredShapeTypeCategories.Contains("Line") && lineShapes.Count > 0)
                shapesFromMissingCategories.AddRange(lineShapes);
                
            if (!offeredShapeTypeCategories.Contains("SquareRect") && squareRectShapes.Count > 0)
                shapesFromMissingCategories.AddRange(squareRectShapes);
                
            if (!offeredShapeTypeCategories.Contains("LJ") && lJShapes.Count > 0)
                shapesFromMissingCategories.AddRange(lJShapes);
                
            if (!offeredShapeTypeCategories.Contains("T") && tBlockShapes.Count > 0)
                shapesFromMissingCategories.AddRange(tBlockShapes);
                
            if (!offeredShapeTypeCategories.Contains("SZ") && szBlockShapes.Count > 0)
                shapesFromMissingCategories.AddRange(szBlockShapes);
                
            if (!offeredShapeTypeCategories.Contains("Corner") && cornerBlockShapes.Count > 0)
                shapesFromMissingCategories.AddRange(cornerBlockShapes);
            
            // If we have shapes from missing categories, prioritize those
            if (shapesFromMissingCategories.Count > 0)
            {
                candidateShapes = shapesFromMissingCategories;
                ////Debug.log($"Prioritizing {shapesFromMissingCategories.Count} shapes from missing categories for variety");
            }

            // --- ANALYZE ALL BLOCKS FOR THEIR POTENTIAL ---
            Dictionary<BlockShape, float> blockScores = new Dictionary<BlockShape, float>();
            
            // Dynamic clearing weight adjustment - increase clearing weight in middle game
            float dynamicClearingWeight = clearingPotentialWeight;
            if (timePressureBias > funGameDuration && timePressureBias < hardGameThreshold)
            {
                // Gradually reduce clearing weight as we approach hard game threshold
                float transitionProgress = (timePressureBias - funGameDuration) / (hardGameThreshold - funGameDuration);
                dynamicClearingWeight = Mathf.Lerp(clearingPotentialWeight, 0.3f, transitionProgress);
                ////Debug.log($"Transition phase: Reducing clearing weight to {dynamicClearingWeight:F2}");
            }
            
            // Evaluate each shape on multiple criteria 
            foreach (BlockShape shape in candidateShapes)
            {
                // 1. Line clearing potential - use a more thorough evaluation
                int clearingPotential = EvaluateLineClearingPotential(shape);
                
                // 2. Size/complexity (prefer bigger blocks in early game, more tactical in mid-game)
                int complexity = shape.GetComplexity();
                
                // 3. Category bonus/penalty based on recent history (encourage variety)
                float categoryBonus = CalculateCategoryBonus(shape, easyNames, mediumNames, hardNames);
                
                // 4. Board state specific score
                float boardSpecificScore = 0f;
                
                // Early game: Prefer bigger blocks to fill the board when empty
                if (currentBoardDensity < earlyGameThreshold)
                {
                    // Give preference to bigger/more complex blocks to fill the board quickly
                    boardSpecificScore = Mathf.Min(1.0f, complexity / 10f);
                }
                // Late game: Heavily prefer clearing blocks if board getting full
                else if (currentBoardDensity > lateGameThreshold)
                {
                    boardSpecificScore = clearingPotential > 0 ? 0.8f : 0f; // Strong bonus if can clear lines
                }
                // Mid game: Balance between clearing and building
                else
                {
                    // Balance between complexity and clearing with bias toward clearing
                    boardSpecificScore = (clearingPotential * 0.8f) + (Mathf.Min(1.0f, complexity / 10f) * 0.2f);
                }
                
                // NEW: Apply custom probability multiplier if defined
                float customProbabilityMultiplier = 1.0f;
                foreach (var blockProb in customBlockProbabilities)
                {
                    if (blockProb.blockName == shape.shapeName)
                    {
                        customProbabilityMultiplier = blockProb.probabilityMultiplier;
                        ////Debug.log($"Applied custom probability {customProbabilityMultiplier} to {shape.shapeName}");
                        break;
                    }
                }
                
                // Calculate total block score with dynamic weights
                float totalScore = 
                    (clearingPotential * dynamicClearingWeight) + 
                    (boardSpecificScore * 0.4f) + 
                    (categoryBonus * varietyWeight);
                
                // Apply custom probability multiplier
                totalScore *= customProbabilityMultiplier;
                
                // --- Apply programmatic probability adjustments ---
                string shapeTypeCategory = GetShapeTypeCategory(shape);
                if (shapeTypeCategory == "L") { // L Shapes (lower probability)
                    // Extreme penalty for L shapes
                    totalScore *= 0.05f;
                    
                    // If we've recently given an L shape, make it even rarer
                    // if (blocksSinceLastLShape < L_SHAPE_COOLDOWN) {
                    //     totalScore *= 0.1f; // Additional 10x penalty during cooldown
                    //     ////Debug.log($"Applied HEAVY L-Shape penalty to {shape.shapeName} - cooldown active ({blocksSinceLastLShape}/{L_SHAPE_COOLDOWN})");
                    // } else {
                    //     ////Debug.log($"Applied L-Shape penalty to {shape.shapeName} - cooldown expired");
                    // }
                }
                else if (shapeTypeCategory == "Corner") { // Corner Shapes (lower probability)
                     // Extreme penalty for corner shapes (same as L shapes)
                     totalScore *= 0.05f;
                     
                     // If we've recently given a corner shape, make it even rarer
                     // if (blocksSinceLastCornerShape < CORNER_SHAPE_COOLDOWN) {
                     //     totalScore *= 0.1f; // Additional 10x penalty during cooldown
                     //     ////Debug.log($"Applied HEAVY Corner shape penalty to {shape.shapeName} - cooldown active ({blocksSinceLastCornerShape}/{CORNER_SHAPE_COOLDOWN})");
                     // } else {
                     //     ////Debug.log($"Applied Corner shape penalty to {shape.shapeName} - cooldown expired");
                     // }
                }
                else if (shapeTypeCategory == "T") { // T Shapes (lower probability)
                    totalScore *= 0.2f;
                    // ////Debug.log($"Applied T-Shape penalty to {shape.shapeName}. New score: {totalScore:F2}");
                }
                else if (shapeTypeCategory == "SquareRect") { // Square/Rect Shapes (higher probability)
                     totalScore *= 2.0f;
                     // ////Debug.log($"Applied Square/Rect bonus to {shape.shapeName}. New score: {totalScore:F2}");
                }
                // --------------------------------------------------
                
                blockScores[shape] = totalScore;
            }
            
            // Select from top scoring blocks with some randomness to keep it interesting
            List<BlockShape> topBlocks = blockScores.OrderByDescending(kvp => kvp.Value)
                                                  .Take(3) // Take top 3 candidates
                                                  .Select(kvp => kvp.Key)
                                                  .ToList();
            
            if (topBlocks.Count > 0)
            {
                // Slightly randomize selection from top blocks
                float rand = Random.value;
                BlockShape selectedShape;
                
                if (rand < 0.7f && topBlocks.Count >= 1)
                {
                    selectedShape = topBlocks[0]; // Top block (70% chance)
                }
                else if (rand < 0.9f && topBlocks.Count >= 2)
                {
                    selectedShape = topBlocks[1]; // Second best (20% chance)
                }
                else if (topBlocks.Count >= 3)
                {
                    selectedShape = topBlocks[2]; // Third best (10% chance)
                }
                else
                {
                    selectedShape = topBlocks[0]; // Default to best if not enough blocks
                }
                
                // Update recent categories
                UpdateRecentCategories(selectedShape, easyNames, mediumNames, hardNames);
                
                ////Debug.log($"Selected block '{selectedShape.shapeName}' based on fun gameplay factors. Score: {blockScores[selectedShape]:F2}");
                return selectedShape;
            }
        }
        
        // --- HARD END GAME (90-95% of time) ---
        // Drift into increasingly difficult play
        if (timePressureBias >= hardGameThreshold && timePressureBias < brutalGameThreshold)
        {
            ////Debug.log("HARD END GAME: Forcing more difficult blocks.");
            // Assign fixed probabilities for the harder end game
            float hardProb = 0.7f;  // High chance for hard blocks
            float mediumProb = 0.25f; // Some medium blocks
            float easyProb = 0.05f;   // Minimal chance for easy blocks
            
            // As we get closer to brutal threshold, increase hard probability
            float progressTowardBrutal = (timePressureBias - hardGameThreshold) / (brutalGameThreshold - hardGameThreshold);
            hardProb = Mathf.Lerp(hardProb, 1.0f, progressTowardBrutal);
            mediumProb = Mathf.Lerp(mediumProb, 0.0f, progressTowardBrutal);
            easyProb = Mathf.Lerp(easyProb, 0.0f, progressTowardBrutal);
            
            // Select category based on probability
            List<BlockShape> categoryPool = null;
            float randomValue = Random.value;
            
            // Determine potential pools first (considering shapes available in candidateShapes)
            var availableEasy = candidateShapes.Where(s => easyNames.Contains(s.shapeName)).ToList();
            var availableMedium = candidateShapes.Where(s => mediumNames.Contains(s.shapeName)).ToList();
            var availableHard = candidateShapes.Where(s => hardNames.Contains(s.shapeName)).ToList();

            if (randomValue < easyProb && availableEasy.Count > 0)
            {
                categoryPool = availableEasy;
                ////Debug.log("Hard mode: Selected Easy category.");
            }
            else if (randomValue < easyProb + mediumProb && availableMedium.Count > 0)
            {
                categoryPool = availableMedium;
                 ////Debug.log("Hard mode: Selected Medium category.");
            }
            else if (availableHard.Count > 0)
            {
                categoryPool = availableHard;
                ////Debug.log("Hard mode: Selected Hard category.");
            }
            else
            {
                // Fallback if chosen category is empty but others aren't
                ////Debug.log("Hard mode: Chosen category empty, falling back...");
                if (availableHard.Count > 0) categoryPool = availableHard;
                else if (availableMedium.Count > 0) categoryPool = availableMedium;
                else if (availableEasy.Count > 0) categoryPool = availableEasy;
                else categoryPool = candidateShapes; // Absolute fallback
            }
            
            // If even the fallback results in no shapes, use all candidates
            if (categoryPool == null || categoryPool.Count == 0)
            {
                //Debug.LogWarning("Hard mode: No shapes in selected category or fallbacks, using all candidates.");
                categoryPool = candidateShapes;
                if (categoryPool.Count == 0) { 
                     //Debug.LogError("CRITICAL (Hard): No candidate shapes available at all in Hard mode!"); 
                     return null; 
                }
            }
            
            // Filter out shapes that would create duplicates *within the current offering*
            if (preventDuplicateBlocks && blocksInCurrentSpawnBatch.Count > 0)
            {
                HashSet<string> hardModeExistingShapeNames = blocksInCurrentSpawnBatch.Select(b => b.shape.shapeName).ToHashSet();
                
                // Remove shapes that would create duplicates
                List<BlockShape> filteredPool = categoryPool
                    .Where(s => !hardModeExistingShapeNames.Contains(s.shapeName) || 
                           (s.shapeName == "3x3" && allow3x3DuplicatesWhenEmpty && currentBoardDensity < emptyBoardThreshold))
                    .ToList();
                    
                // If filtering left us with no shapes, revert to original pool (allow duplicates if necessary)
                if (filteredPool.Count == 0 && categoryPool.Count > 0)
                {
                    ////Debug.log("Hard mode: No unique shapes available after filtering category pool, allowing duplicates from this pool.");
                    // Keep categoryPool as is
                }
                else
                {
                    categoryPool = filteredPool;
                }
            }

             // If the pool is empty after filtering, we have a problem
                if (categoryPool.Count == 0)
                {
                 //Debug.LogError("CRITICAL (Hard): Category pool became empty after duplicate filtering! Selecting random candidate.");
                 if (candidateShapes.Count == 0) return null; // Should not happen based on earlier checks
                 categoryPool = candidateShapes; // Use unfiltered candidates as last resort
                 
                 // Re-filter this candidate list one last time
                 if (preventDuplicateBlocks && blocksInCurrentSpawnBatch.Count > 0)
                 {
                     HashSet<string> finalCheckNames = blocksInCurrentSpawnBatch.Select(b => b.shape.shapeName).ToHashSet();
                     categoryPool = categoryPool
                         .Where(s => !finalCheckNames.Contains(s.shapeName) || 
                                (s.shapeName == "3x3" && allow3x3DuplicatesWhenEmpty && currentBoardDensity < emptyBoardThreshold))
                         .ToList();
                     if (categoryPool.Count == 0) categoryPool = candidateShapes; // Give up on filtering
                 }
             }
            
            // NEW: Apply custom probabilities to the final pool
            Dictionary<BlockShape, float> weightedPool = new Dictionary<BlockShape, float>();
            foreach (var shape in categoryPool)
            {
                float weight = 1.0f;
                // Find if this shape has a custom probability
                foreach (var blockProb in customBlockProbabilities)
                {
                    if (blockProb.blockName == shape.shapeName)
                    {
                        weight = blockProb.probabilityMultiplier;
                        break;
                    }
                }
                weightedPool[shape] = weight;
            }
            
            // Select based on weighted probabilities
            if (weightedPool.Count > 0)
            {
                float totalWeight = weightedPool.Values.Sum();
                if (totalWeight <= 0) { // Handle case where all weights are zero
                    //Debug.LogWarning("Hard Mode: Total weight in pool is zero, selecting randomly.");
                     BlockShape randomFallbackShape = categoryPool[Random.Range(0, categoryPool.Count)];
                     UpdateRecentCategories(randomFallbackShape, easyNames, mediumNames, hardNames);
                     return randomFallbackShape;
                }

                float randomPoint = Random.value * totalWeight;
                float accumulatedWeight = 0f;
                
                foreach (var kvp in weightedPool)
                {
                    accumulatedWeight += kvp.Value;
                    if (randomPoint <= accumulatedWeight)
                    {
                        BlockShape selectedEndGameShape = kvp.Key;
                        UpdateRecentCategories(selectedEndGameShape, easyNames, mediumNames, hardNames);
                        return selectedEndGameShape;
                    }
                }
                // Fallback in case of rounding errors
                BlockShape selectedFallbackShape = weightedPool.Keys.Last();
                UpdateRecentCategories(selectedFallbackShape, easyNames, mediumNames, hardNames);
                return selectedFallbackShape;
            }
        }
        
        // --- FALLBACK / NORMAL DDA LOGIC ---
        // This should only be reached if the fun gameplay selection somehow failed

        // --- Fallback to normal DDA Probability Adjustment ---
        // This path should ideally not be reached often, especially in Brutal mode

        // Apply exclusions if not done earlier
        if (excludedShapeNames != null && excludedShapeNames.Count > 0 && !exclusionsAppliedToBase)
        {
            easyShapes = easyShapes.Where(s => !excludedShapeNames.Contains(s.shapeName)).ToList();
            mediumShapes = mediumShapes.Where(s => !excludedShapeNames.Contains(s.shapeName)).ToList();
            hardShapes = hardShapes.Where(s => !excludedShapeNames.Contains(s.shapeName)).ToList();
            candidateShapes = candidateShapes.Where(s => !excludedShapeNames.Contains(s.shapeName)).ToList(); // Filter the base candidates too
            ////Debug.log($"Fallback DDA: Applied temp exclusions: {string.Join(", ", excludedShapeNames)}.");
        }

        float adjustmentFactor = difficultyManager.GetTier1AdjustmentFactor();
        float netBias = adjustmentFactor - (timePressureBias * difficultyManager.maxAdjustmentFactor * 1.5f);

        // Calculate base probabilities
        float baseEasyProb = 0.33f;
        float baseMediumProb = 0.34f;
        float baseHardProb = 0.33f;

        // Adjust based on net bias
        float adjustedEasyProb = Mathf.Clamp01(baseEasyProb + netBias);
        float adjustedHardProb = Mathf.Clamp01(baseHardProb - netBias);
        float adjustedMediumProb = 1.0f - adjustedEasyProb - adjustedHardProb;

        // Ensure medium prob isn't negative
        if (adjustedMediumProb < 0)
        {
            adjustedMediumProb = 0;
            float sum = adjustedEasyProb + adjustedHardProb;
            if (sum > 0)
            {
                adjustedEasyProb /= sum;
                adjustedHardProb /= sum;
            }
            else
            {
                adjustedEasyProb = 0.5f;
                adjustedHardProb = 0.5f;
            }
        }

        // Filter out shapes that would create duplicates
        HashSet<string> fallbackExistingShapeNames = new HashSet<string>();
        if (preventDuplicateBlocks && blocksInCurrentSpawnBatch.Count > 0)
        {
            foreach (var block in blocksInCurrentSpawnBatch)
            {
                if (block.shape != null)
                {
                    fallbackExistingShapeNames.Add(block.shape.shapeName);
                }
            }
            
            // Filter categories based on duplicates
            if (easyShapes.Count > 0)
            {
                easyShapes = easyShapes.Where(s => !fallbackExistingShapeNames.Contains(s.shapeName)).ToList();
            }
            
            if (mediumShapes.Count > 0)
            {
                mediumShapes = mediumShapes.Where(s => !fallbackExistingShapeNames.Contains(s.shapeName)).ToList();
            }
            
            if (hardShapes.Count > 0)
            {
                hardShapes = hardShapes.Where(s => !fallbackExistingShapeNames.Contains(s.shapeName)).ToList();
            }
            
            // Special case for 3x3 when board is mostly empty
            if (fallbackExistingShapeNames.Contains("3x3") && allow3x3DuplicatesWhenEmpty && CalculateBoardDensity() < emptyBoardThreshold)
            {
                ////Debug.log("Allowing 3x3 duplicate for empty board in fallback logic");
                var shape3x3 = availableBlocks.FirstOrDefault(s => s.shapeName == "3x3");
                if (shape3x3 != null)
                {
                    // Add to appropriate category if it exists
                    if (hardNames.Contains("3x3")) hardShapes.Add(shape3x3);
                }
            }
        }

        // Select category based on adjusted probabilities
        List<BlockShape> fallbackPool = null;
        float randValue = Random.value;

        if (randValue < adjustedEasyProb && easyShapes.Count > 0)
        {
            fallbackPool = easyShapes;
        }
        else if (randValue < adjustedEasyProb + adjustedMediumProb && mediumShapes.Count > 0)
        {
            fallbackPool = mediumShapes;
        }
        else if (hardShapes.Count > 0)
        {
            fallbackPool = hardShapes;
        }
        else
        {
            // If all filtered pools are empty, use all candidate shapes but still filter duplicates
            fallbackPool = candidateShapes;
            if (preventDuplicateBlocks && blocksInCurrentSpawnBatch.Count > 0)
            {
                fallbackPool = fallbackPool
                    .Where(s => !fallbackExistingShapeNames.Contains(s.shapeName) || 
                            (s.shapeName == "3x3" && allow3x3DuplicatesWhenEmpty && CalculateBoardDensity() < emptyBoardThreshold))
                    .ToList();
            }
        }

        // If we ended up with no shapes after all the filtering, allow duplicates as last resort
        if (fallbackPool == null || fallbackPool.Count == 0)
        {
            ////Debug.log("Fallback: No unique shapes available after filtering, allowing duplicates");
            fallbackPool = candidateShapes;
        }

        // Select a random block from the chosen category
        BlockShape fallbackShape = fallbackPool[Random.Range(0, fallbackPool.Count)];
        UpdateRecentCategories(fallbackShape, easyNames, mediumNames, hardNames);

        return fallbackShape;
    }
    
    // Helper method to evaluate how well a block can clear lines on the current board
    private int EvaluateLineClearingPotential(BlockShape shape)
    {
        int maxClearCount = 0;
        int positionsChecked = 0;
        int validPlacements = 0;
        
        // More thorough check - scan entire board
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                positionsChecked++;
                
                if (CanPlaceBlock(shape, pos))
                {
                    validPlacements++;
                    int clearCount = SimulatePlacementAndCheckClears(shape, pos);
                    maxClearCount = Mathf.Max(maxClearCount, clearCount);
                    
                    // Early exit if we found a great position that clears multiple lines
                    if (clearCount >= 3) 
                    {
                        return clearCount + 2; // Bonus for exceptional clearing potential
                    }
                    else if (clearCount >= 2)
                    {
                        return clearCount + 1; // Bonus for good clearing potential
                    }
                }
            }
        }
        
        // Bonus factor for shapes that can be placed in many positions
        float placementFlexibilityBonus = 0;
        if (positionsChecked > 0)
        {
            float placementRatio = (float)validPlacements / positionsChecked;
            placementFlexibilityBonus = placementRatio * 0.5f; // Up to 0.5 bonus for very flexible pieces
        }
        
        // Return clearing potential with flexibility bonus
        return maxClearCount + Mathf.RoundToInt(placementFlexibilityBonus);
    }
    
    /// <summary>
    /// --- End Evaluate Line Clearing Potential ---
    /// </summary>
    /// <param name="shape"></param>
    /// <returns></returns>
    //************************************************************************************************************************************************************
    // Calculate bonus/penalty based on recent block categories 
    private float CalculateCategoryBonus(BlockShape shape, HashSet<string> easyNames, HashSet<string> mediumNames, HashSet<string> hardNames)
    {
        string category = GetShapeCategory(shape, easyNames, mediumNames, hardNames);
        
        // Check if we've given too many blocks of this category recently
        int sameCount = 0;
        for (int i = 0; i < Mathf.Min(consecutiveBlocksLimit, recentBlockCategories.Count); i++)
        {
            if (recentBlockCategories[i] == category)
            {
                sameCount++;
            }
        }
        
        // Calculate penalty - more penalty for more repeats
        float penalty = sameCount * 0.2f; // 0.2 penalty per repeat
        
        return Mathf.Clamp01(1.0f - penalty); // 1.0 = no penalty, lower = more penalty
    }
    
    // Get the category of a shape
    private string GetShapeCategory(BlockShape shape, HashSet<string> easyNames, HashSet<string> mediumNames, HashSet<string> hardNames)
    {
        if (easyNames.Contains(shape.shapeName)) return "Easy";
        if (mediumNames.Contains(shape.shapeName)) return "Medium";
        if (hardNames.Contains(shape.shapeName)) return "Hard";
        return "Unknown";
    }
    
    // Update the list of recent block categories
    private void UpdateRecentCategories(BlockShape shape, HashSet<string> easyNames, HashSet<string> mediumNames, HashSet<string> hardNames)
    {
        string category = GetShapeCategory(shape, easyNames, mediumNames, hardNames);
        
        // Add to front
        recentBlockCategories.Insert(0, category);
        
        // Trim list if needed
        if (recentBlockCategories.Count > consecutiveBlocksLimit)
        {
            recentBlockCategories.RemoveAt(recentBlockCategories.Count - 1);
        }
    }

    // NEW Helper method to get the Shape Type Category (Line, Square, L, T, SZ, Corner)
    private string GetShapeTypeCategory(BlockShape shape)
    {
        if (singleLineShapes.Contains(shape.shapeName)) return "Line";
        if (squareRectangleShapes.Contains(shape.shapeName)) return "SquareRect";
        if (lShapes.Contains(shape.shapeName)) return "L";
        if (tShapes.Contains(shape.shapeName)) return "T";
        if (szShapes.Contains(shape.shapeName)) return "SZ";
        if (cornerShapes.Contains(shape.shapeName)) return "Corner";
        return "Unknown"; // Fallback
    }

    // Helper to check if a shape can be placed anywhere on the current grid
    public bool IsShapePlaceableAnywhere(BlockShape shape)
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2Int currentPos = new Vector2Int(x, y);
                if (CanPlaceBlock(shape, currentPos))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ** KEPT/REUSED ** Helper: Find simplest placeable block from candidates
    private BlockShape FindSimplestPlaceableBlock(List<BlockShape> candidates)
    {
        BlockShape bestShape = null;
        int minComplexity = int.MaxValue;

        foreach (BlockShape shape in candidates.OrderBy(s => s.GetComplexity())) // Check simplest first
        {
            if (IsShapePlaceableAnywhere(shape))
            {
                 int complexity = shape.GetComplexity();
                 if (complexity < minComplexity) // Should catch the first placeable due to OrderBy
                 {
                      minComplexity = complexity;
                      bestShape = shape;
                      return bestShape; // Found the simplest placeable
                 }
            }
        }
        // If loop finishes, no block in candidates is placeable
         if (bestShape == null && candidates.Count > 0) {
             //Debug.LogWarning($"FindSimplestPlaceableBlock: Could not find any placeable block among {candidates.Count} candidates.");
         }
        return bestShape;
    }

    // ** RENAMED ** Was GetGridFillPercentage
    public float CalculateBoardDensity()
    {
        int filledCount = 0;
        int totalCells = gridWidth * gridHeight;
        if (totalCells == 0) return 0f;
        for (int x = 0; x < gridWidth; x++) { for (int y = 0; y < gridHeight; y++) { if (grid[x, y] == 1) filledCount++; } }
        return (float)filledCount / totalCells;
    }

    // SimulatePlacementAndCheckClears unchanged
    public int SimulatePlacementAndCheckClears(BlockShape shape, Vector2Int position)
    {
        // Create a temporary copy of the grid
        int[,] tempGrid = (int[,])grid.Clone();

        // Simulate placing the block
        foreach (Vector2Int cell in shape.cells)
        {
            Vector2Int gridPos = position + cell;
            // Basic bounds check (redundant if CanPlaceBlock was called first, but safe)
            if (gridPos.x >= 0 && gridPos.x < gridWidth && gridPos.y >= 0 && gridPos.y < gridHeight)
            {
                 tempGrid[gridPos.x, gridPos.y] = 1;
            }
            else
            {
                 // This shouldn't happen if CanPlaceBlock was true
                 // //Debug.LogError("Simulating placement outside bounds!");
                 return 0; // Invalid placement simulation
            }
        }

        // Simulate checking lines on the temporary grid
        int linesCleared = 0;
        // Check rows
        for (int y = 0; y < gridHeight; y++)
        {
            bool rowComplete = true;
            for (int x = 0; x < gridWidth; x++)
            {
                if (tempGrid[x, y] == 0) { rowComplete = false; break; }
            }
            if (rowComplete) { linesCleared++; }
        }
        // Check columns
        for (int x = 0; x < gridWidth; x++)
        {
            bool columnComplete = true;
            for (int y = 0; y < gridHeight; y++)
            {
                if (tempGrid[x, y] == 0) { columnComplete = false; break; }
            }
            if (columnComplete) { linesCleared++; }
        }

        // Return the count of lines that *would* be cleared
        return linesCleared;
    }

    // ** KEPT ** Was used for IsGameOver
    private List<BlockShape> FindPlaceableBlocks()
    {
        // This checks *all* available blocks, ignoring candidates/filtering
        List<BlockShape> placeable = new List<BlockShape>();
        foreach (BlockShape shape in availableBlocks) 
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (CanPlaceBlock(shape, new Vector2Int(x, y)))
                    {
                        placeable.Add(shape);
                        goto NextShape;
                    }
                }
            }
            NextShape:;
        }
        // ////Debug.log($"FindPlaceableBlocks (Full Scan): Found {placeable.Count} placeable shapes.");
        return placeable;
    }
    
    public bool TryPlaceBlock(BlockShape shape, Vector2Int position)
    {
        if (!CanPlaceBlock(shape, position)) { return false; }
        
        // Place the block
        foreach (Vector2Int cell in shape.cells)
        {
            Vector2Int gridPos = position + cell;
            grid[gridPos.x, gridPos.y] = 1;
            gridColors[gridPos.x, gridPos.y] = shape.blockColor; // Store the block's color
            
            // Update visuals
            UpdateCellVisual(gridPos.x, gridPos.y, true);
        }
        
        blockPlacedSinceLastMetricUpdate = true; // Flag that placement occurred

        // Check and clear completed lines
        int linesCleared = CheckAndClearLines();
        
        // Award points
        if (linesCleared > 0)
        {
            int score = linesCleared * pointsPerLine;
            int comboBonus = (difficultyManager != null ? difficultyManager.GetConsecutiveSuccesses() : 0) * comboMultiplier;
            score += comboBonus;
            
            if (gameManager != null) { gameManager.AddScore(score); }
            ////Debug.log($"Lines Cleared: {linesCleared}. Score Added: {score} (Combo Bonus: {comboBonus})");

            // --- DDA Reporting ---
            if (difficultyManager != null) { difficultyManager.RecordSuccess(); }
            timeSinceLastClear = 0f; // Reset timer on success
        }
        else
        {
            // --- DDA Reporting ---
            if (difficultyManager != null) { difficultyManager.RecordFailure(); }
            ////Debug.log("Block placed, no lines cleared. Failure recorded.");
            // timeSinceLastClear continues incrementing in Update()
        }
        
        // --- ML-Agent Integration ---
        lastLinesCleared = linesCleared; // Store lines cleared for agent reward
        // --- End ML-Agent Integration ---
        
        return true;
    }
    
    public bool CanPlaceBlock(BlockShape shape, Vector2Int position)
    {
        foreach (Vector2Int cell in shape.cells)
        {
            Vector2Int gridPos = position + cell;
            
            // Check if out of bounds
            if (gridPos.x < 0 || gridPos.x >= gridWidth || gridPos.y < 0 || gridPos.y >= gridHeight)
            {
                return false;
            }
            
            // Check if cell is already occupied
            if (grid[gridPos.x, gridPos.y] == 1)
            {
                return false;
            }
            
        }
        return true;
    }
    
    private void UpdateCellVisual(int x, int y, bool filled)
    {
        // Update the visual appearance of the cell based on whether it's filled
        if (gridCells[x, y] != null)
        {
            // Simplify to directly update SpriteRenderer color
            SpriteRenderer renderer = gridCells[x, y].GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                // Use the stored grid color if filled, otherwise a default empty color (e.g., dark grey)
                renderer.color = filled ? gridColors[x, y] : new Color(0.2f, 0.2f, 0.22f); // Use dark grey for empty
            }
        }
    }
    
    private int CheckAndClearLines()
    {
        int linesCleared = 0;
        
        // Create lists to store row and column indices that need to be cleared
        List<int> rowsToClear = new List<int>();
        List<int> columnsToClear = new List<int>();
        
        // Check rows
        for (int y = 0; y < gridHeight; y++)
        {
            bool rowComplete = true;
            for (int x = 0; x < gridWidth; x++)
            {
                if (grid[x, y] == 0)
                {
                    rowComplete = false;
                    break;
                }
            }
            
            if (rowComplete)
            {
                rowsToClear.Add(y);
                linesCleared++;
            }
        }
        
        // Check columns
        for (int x = 0; x < gridWidth; x++)
        {
            bool columnComplete = true;
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == 0)
                {
                    columnComplete = false;
                    break;
                }
            }
            
            if (columnComplete)
            {
                columnsToClear.Add(x);
                linesCleared++;
            }
        }
        
        // Clear all identified rows and columns
        foreach (int row in rowsToClear)
        {
            ClearRow(row);
            
        }
        
        foreach (int column in columnsToClear)
        {
            ClearColumn(column);
        }
        
        return linesCleared;
    }

    private void ClearRow(int row)
    {
        // Only start the visual animation - actual clearing is now done in the animation
        // StartCoroutine(FlashRow(row)); // Old Rainbow
        activeClearOperations++;
        if (gameManager.IsAgentTrainingMode && gameManager != null)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                grid[x, row] = 0;
                UpdateCellVisual(x, row, false);
            }
            
            FinishClearOperation();
        }
        else
        {
            StartCoroutine(SimpleFlashRow(row)); // New Simple Flash
        }
    }

    private void ClearColumn(int column)
    {
        // Only start the visual animation - actual clearing is now done in the animation
        // StartCoroutine(FlashColumn(column)); // Old Rainbow
        activeClearOperations++;
        if (gameManager.IsAgentTrainingMode && gameManager != null)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[column, y] = 0;
                UpdateCellVisual(column, y, false);
            }
            
            FinishClearOperation();
        }
        else
        {
            StartCoroutine(SimpleFlashColumn(column)); // New Simple Flash
        }
    }

    private void FinishClearOperation()
    {
        activeClearOperations--;
        if (activeClearOperations < 0)
        {
            activeClearOperations = 0; // Safety check, should not happen
            Debug.LogWarning("GridManager: activeClearOperations dropped below zero!");
        }
        // isClearingLines will automatically become false when activeClearOperations is 0
    }

    private IEnumerator SimpleFlashRow(int row)
    {
        
        Color flashColor = Color.yellow; // Or Color.white
        Color defaultColor = new Color(0.2f, 0.2f, 0.22f); // Use the dark background color
        int flashes = 2;
        float flashDuration = 0.04f; // Duration of each on/off phase

        // Store original colors (might not be necessary if always flashing from default)
        Color[] originalColors = new Color[gridWidth];
        for (int x = 0; x < gridWidth; x++)
        {
            originalColors[x] = gridCells[x, row]?.GetComponent<SpriteRenderer>()?.color ?? defaultColor;
        }

        for (int i = 0; i < flashes; i++)
        {
            // Flash ON
            for (int x = 0; x < gridWidth; x++)
            {
                UpdateCellWithColor(x, row, flashColor);
            }
            yield return new WaitForSeconds(flashDuration);

            // Flash OFF
            for (int x = 0; x < gridWidth; x++)
            {
                UpdateCellWithColor(x, row, defaultColor);
            }
            yield return new WaitForSeconds(flashDuration);
        }

        // After flashing, actually clear the row
        for (int x = 0; x < gridWidth; x++)
        {
            grid[x, row] = 0; // Clear the cell in the grid data structure
            UpdateCellVisual(x, row, false); // Update the visual to show empty
        }
        
        // Check if this was the last animation (both rows and columns)
        FinishClearOperation();
    }

    private IEnumerator SimpleFlashColumn(int column)
    {
        
        Color flashColor = Color.yellow; // Or Color.white
        Color defaultColor = new Color(0.2f, 0.2f, 0.22f); // Use the dark background color
        int flashes = 2;
        float flashDuration = 0.04f; // Duration of each on/off phase

        // Store original colors
        Color[] originalColors = new Color[gridHeight];
        for (int y = 0; y < gridHeight; y++)
        {
            originalColors[y] = gridCells[column, y]?.GetComponent<SpriteRenderer>()?.color ?? defaultColor;
        }

        for (int i = 0; i < flashes; i++)
        {
            // Flash ON
            for (int y = 0; y < gridHeight; y++)
            {
                UpdateCellWithColor(column, y, flashColor);
            }
            yield return new WaitForSeconds(flashDuration);

            // Flash OFF
            for (int y = 0; y < gridHeight; y++)
            {
                UpdateCellWithColor(column, y, defaultColor);
            }
            yield return new WaitForSeconds(flashDuration);
        }

        // After flashing, actually clear the column
        for (int y = 0; y < gridHeight; y++)
        {
            grid[column, y] = 0; // Clear the cell in the grid data structure
            UpdateCellVisual(column, y, false); // Update the visual to show empty
        }
        
        // Check if this was the last animation (both rows and columns)
        FinishClearOperation();
    }
    
    // Helper coroutine to check if all line clearing animations are done
    private IEnumerator CheckIfAllCleared()
    {
        // Wait a small delay to ensure all coroutines are registered
        yield return new WaitForSeconds(0.1f);
        
        // Count active coroutines with the words "Flash" in their name
        int flashingCoroutines = 0;
        foreach (MonoBehaviour mono in this.GetComponents<MonoBehaviour>())
        {
            System.Reflection.FieldInfo coroutineField = mono.GetType().GetField("m_Coroutines", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
            if (coroutineField != null)
            {
                IEnumerator[] coroutines = coroutineField.GetValue(mono) as IEnumerator[];
                if (coroutines != null)
                {
                    foreach (IEnumerator coroutine in coroutines)
                    {
                        if (coroutine != null && coroutine.ToString().Contains("Flash"))
                        {
                            flashingCoroutines++;
                        }
                    }
                }
            }
        }
        
        // If this is the only flashing coroutine (or none are left), 
        // we're done with clearing lines
        if (flashingCoroutines <= 1)
        {
            activeClearOperations = 0;
        }
    }

    private void UpdateCellWithColor(int x, int y, Color color)
    {
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && gridCells[x, y] != null) 
        {
            SpriteRenderer renderer = gridCells[x, y].GetComponent<SpriteRenderer>();
            if (renderer != null) 
            {
                renderer.color = color;
            }
        }
    }
    
    public void RemoveBlock(BlockController block)
    {
        if (block != null && block.gameObject != null)
        {
            // Destroy the GameObject
            Destroy(block.gameObject);
        }
        // Remove from list
        currentBlocks.Remove(block);
    }
    
    // Helper method to convert world position to grid position
    public Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        if (gridContainerTransform == null)
        {
            Debug.LogError("GridContainer transform not set in WorldToGridPosition! Grid might not be initialized properly.");
            return Vector2Int.zero;
        }

        // Calculate starting position to center the grid (local to gridContainer)
        float totalWidth = gridWidth * cellSize;
        float totalHeight = gridHeight * cellSize;
        float startX = -(totalWidth / 2f) + (cellSize / 2f);
        float startY = -(totalHeight / 2f) + (cellSize / 2f);
        
        // Convert world position to local position within the gridContainer
        Vector3 localPosInGridContainer = gridContainerTransform.InverseTransformPoint(worldPosition);

        // Calculate position relative to the center of cell (0,0) in the gridContainer's local space
        float relativeX = localPosInGridContainer.x - startX;
        float relativeY = localPosInGridContainer.y - startY;

        // Calculate grid coordinates
        int x = Mathf.RoundToInt(relativeX / cellSize);
        int y = Mathf.RoundToInt(relativeY / cellSize);
        
        return new Vector2Int(x, y);
    }

    // New method: Convert grid position to world position
    public Vector3 GetWorldPositionFromGrid(Vector2Int gridPosition)
    {
        if (gridContainerTransform == null)
        {
            Debug.LogError("GridContainer transform not set in GetWorldPositionFromGrid! Grid might not be initialized properly.");
            return Vector3.zero;
        }

        // Calculate starting position to center the grid (local to gridContainer)
        float totalWidth = gridWidth * cellSize;
        float totalHeight = gridHeight * cellSize;
        float startX = -(totalWidth / 2f) + (cellSize / 2f);
        float startY = -(totalHeight / 2f) + (cellSize / 2f);
        
        // Calculate local position of the cell's center within the gridContainer
        Vector3 cellLocalPositionInGridContainer = new Vector3(
            startX + (gridPosition.x * cellSize),
            startY + (gridPosition.y * cellSize),
            0
        );
            
        // Convert local position to world position
        return gridContainerTransform.TransformPoint(cellLocalPositionInGridContainer);
    }

    // --- DDA Metric Calculation Methods ---

    private GridMetrics GetGridStateMetrics()
    {
        GridMetrics metrics = new GridMetrics();

        metrics.BoardDensity = CalculateBoardDensity();
        metrics.AvailablePlacements = CalculateAvailablePlacements(availableBlocks); // Check against all potential shapes
        metrics.MaxPossiblePlacements = CalculateMaxPossiblePlacements(availableBlocks); // Estimate max for normalization
        metrics.TimeSinceClear = timeSinceLastClear;
        metrics.IsolatedHoles = CalculateIsolatedHoles();
        metrics.BoardBumpiness = CalculateBoardBumpiness();
        metrics.IsAnyFuturePieceUnplaceable = CheckIfAnyPieceUnplaceable(availableBlocks); // Lookahead check

        return metrics;
    }

    // Calculates total valid placements for a given list of shapes
    private int CalculateAvailablePlacements(List<BlockShape> shapesToConsider)
    {
        int totalPlacements = 0;
        HashSet<Vector2Int> uniquePlacementPositions = new HashSet<Vector2Int>();

        foreach (BlockShape shape in shapesToConsider)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (CanPlaceBlock(shape, pos))
                    {
                        // Count unique positions OR total valid moves?
                        // Let's count total valid moves for now.
                        totalPlacements++;
                    }
                }
            }
        }
        return totalPlacements;
    }

    // Estimate max placements (simple version: assumes empty grid)
    private int CalculateMaxPossiblePlacements(List<BlockShape> shapesToConsider)
    {
         // Crude estimate: average shape size * number of shapes * grid size
         // Better: Simulate on empty grid (could be cached)
         int maxPlacements = 0;
         int[,] emptyGrid = new int[gridWidth, gridHeight]; // Temp empty grid
         foreach (BlockShape shape in shapesToConsider)
         {
             for (int x = 0; x < gridWidth; x++)
             {
                 for (int y = 0; y < gridHeight; y++)
                 {
                    // Need a CanPlaceBlock that takes a grid parameter
                    // Or just check bounds for empty grid simulation
                     bool fits = true;
                     foreach (Vector2Int cell in shape.cells)
                     {
                         Vector2Int gridPos = new Vector2Int(x, y) + cell;
                         if (gridPos.x < 0 || gridPos.x >= gridWidth || gridPos.y < 0 || gridPos.y >= gridHeight)
                         {
                             fits = false; break;
                         }
                     }
                     if (fits) maxPlacements++;
                 }
             }
         }
         return maxPlacements > 0 ? maxPlacements : 1; // Avoid division by zero
    }

    // Counts empty cells surrounded by filled cells
    private int CalculateIsolatedHoles()
    {
        int holeCount = 0;
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == 0) // If cell is empty
                {
                    bool surrounded = true;
                    // Check 4 neighbours (or 8 if diagonal matters)
                    int[] dx = { 0, 0, 1, -1 };
                    int[] dy = { 1, -1, 0, 0 };
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = x + dx[i];
                        int ny = y + dy[i];
                        // If neighbour is within bounds AND is empty, it's not a hole
                        if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight && grid[nx, ny] == 0)
                        {
                            surrounded = false;
                            break;
                        }
                        // If neighbour is out of bounds, consider it 'filled' for hole detection
                    }
                    if (surrounded) { holeCount++; }
                }
            }
        }
        return holeCount;
    }

    // Calculates sum of height differences between adjacent columns
    private float CalculateBoardBumpiness()
    {
        float totalBumpiness = 0;
        int[] columnHeights = new int[gridWidth];

        // Calculate height of each column (highest filled cell)
        for (int x = 0; x < gridWidth; x++)
        {
            int height = 0;
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == 1)
                {
                    height = y + 1; // Use 1-based height
                }
            }
            columnHeights[x] = height;
        }

        // Sum absolute differences between adjacent column heights
        for (int x = 0; x < gridWidth - 1; x++)
        {
            totalBumpiness += Mathf.Abs(columnHeights[x] - columnHeights[x + 1]);
        }

        return totalBumpiness;
    }

    // Checks if *any* of the potential shapes cannot be placed anywhere
    private bool CheckIfAnyPieceUnplaceable(List<BlockShape> shapesToConsider)
    {
        foreach (BlockShape shape in shapesToConsider)
        {
            if (!IsShapePlaceableAnywhere(shape))
            {
                 //Debug.LogWarning($"Lookahead: Shape '{shape.shapeName}' is unplaceable on current board.");
                 return true; // Found at least one unplaceable shape
            }
        }
        return false; // All considered shapes are placeable
    }

    // Method to check which lines would be completed if cells are placed
    public List<List<Vector2Int>> GetPotentialLineCompletions(List<Vector2Int> cellsToPlace)
    {
        List<List<Vector2Int>> completedLines = new List<List<Vector2Int>>();
        
        // Create a temporary copy of the grid
        int[,] tempGrid = (int[,])grid.Clone();
        
        // Simulate placing the cells
        foreach (Vector2Int cell in cellsToPlace)
        {
            if (cell.x >= 0 && cell.x < gridWidth && cell.y >= 0 && cell.y < gridHeight)
            {
                tempGrid[cell.x, cell.y] = 1;
            }
        }
        
        // Check rows for completion
        for (int y = 0; y < gridHeight; y++)
        {
            bool rowComplete = true;
            for (int x = 0; x < gridWidth; x++)
            {
                if (tempGrid[x, y] == 0)
                {
                    rowComplete = false;
                    break;
                }
            }
            
            if (rowComplete)
            {
                // This row would be completed, add all cells in this row
                List<Vector2Int> completedRow = new List<Vector2Int>();
                for (int x = 0; x < gridWidth; x++)
                {
                    completedRow.Add(new Vector2Int(x, y));
                }
                completedLines.Add(completedRow);
            }
        }
        
        // Check columns for completion
        for (int x = 0; x < gridWidth; x++)
        {
            bool columnComplete = true;
            for (int y = 0; y < gridHeight; y++)
            {
                if (tempGrid[x, y] == 0)
                {
                    columnComplete = false;
                    break;
                }
            }
            
            if (columnComplete)
            {
                // This column would be completed, add all cells in this column
                List<Vector2Int> completedColumn = new List<Vector2Int>();
                for (int y = 0; y < gridHeight; y++)
                {
                    completedColumn.Add(new Vector2Int(x, y));
                }
                completedLines.Add(completedColumn);
            }
        }
        
        return completedLines;
    }

    // Helper method to shuffle the blocks list for more variety in game starts
    private void ShuffleBlocksList()
    {
        // Fisher-Yates shuffle algorithm
        for (int i = availableBlocks.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            // Swap blocks[i] and blocks[j]
            BlockShape temp = availableBlocks[i];
            availableBlocks[i] = availableBlocks[j];
            availableBlocks[j] = temp;
        }
        ////Debug.log("Shuffled available blocks for more randomized game starts");
    }

    // --- ML-Agent Public Methods ---

    /// <summary>
    /// Checks if a cell at the given coordinates is occupied.
    /// </summary>
    public bool IsCellOccupied(int x, int y)
    {
        // Check bounds first
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
        {
            // Consider out-of-bounds as occupied for placement checks,
            // but maybe return false for observation? Let's return true for safety.
            return true;
        }
        return grid[x, y] == 1;
    }

    /// <summary>
    /// Resets the grid to an empty state, clears current blocks, and spawns initial blocks.
    /// Used by the Agent to start a new episode.
    /// </summary>
    public void ClearGrid()
    {
        ////Debug.log("Clearing Grid for new episode...");
        // Clear grid data
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y] = 0;
                gridColors[x, y] = Color.white; // Reset color
                UpdateCellVisual(x, y, false); // Update visual
            }
        }

        // Clear existing block controllers (destroy game objects)
        foreach (var blockController in currentBlocks)
        {
            if (blockController != null)
            {
                Destroy(blockController.gameObject);
            }
        }
        currentBlocks.Clear();
        lastOfferedBlockSetNames.Clear(); // Clear offered set history
        recentBlockCategories.Clear(); // Clear DDA history

        // Reset metrics
        timeSinceLastClear = 0f;
        lastLinesCleared = 0;
        blockPlacedSinceLastMetricUpdate = false;
        activeClearOperations = 0;
        // Reset any related game state if necessary (e.g., score in GameManager)
       


        // Spawn new initial blocks
        // We might not want to spawn blocks immediately after clearing if the agent
        // is responsible for the first spawn action. Let's comment this out for now.
        // SpawnBlocks(3);
        ////Debug.log("Grid Cleared.");
    }

     /// <summary>
    /// Spawns a specific block prefab provided by the Agent, bypassing DDA selection.
    /// Places it in the next available dock slot.
    /// </summary>
    /// <param name="blockPrefabToSpawn">The specific block prefab chosen by the Agent.</param>
    public void SpawnSpecificBlock(GameObject blockPrefabToSpawn)
    {
        if (blockPrefabToSpawn == null)
        {
            //Debug.LogError("Agent provided a null block prefab to spawn!", this);
            return;
        }

        // Try to get BlockShape component (assuming prefab has it or its child does)
        BlockShape blockShape = blockPrefabToSpawn.GetComponentInChildren<BlockShape>();
        if (blockShape == null)
        {
             // Maybe the prefab *is* the shape? Check root.
             blockShape = blockPrefabToSpawn.GetComponent<BlockShape>();
             if (blockShape == null)
             {
                //Debug.LogError($"Provided block prefab '{blockPrefabToSpawn.name}' does not contain a BlockShape component!", this);
                return;
             }
        }
         ////Debug.log($"Agent requested spawning block with shape: {blockShape.shapeName}");

        // If we have 3 blocks already, maybe replace one? Or just don't spawn?
        // For now, let's just not spawn if the dock is full. Agent needs to handle this.
        if (currentBlocks.Count >= 3)
        {
             //Debug.LogWarning("Agent tried to spawn a block, but dock is full (3 blocks). Ignoring request.", this);
             // Optionally provide negative reward here?
             return;
        }


        // Calculate LOCAL spawn position (spread blocks horizontally) - MATCH normal SpawnBlock logic
        float spacing = dockBlockSpacing;
        float xPosition = blockSpawnArea.x + (currentBlocks.Count) * spacing;
        Vector3 localSpawnPosition = new Vector3(xPosition, blockSpawnArea.y, 0); // This is relative to blocksContainer

        // Instantiate without specific world position/rotation yet - MATCH normal SpawnBlock logic
        GameObject blockObj = Instantiate(blockPrefabToSpawn);

        if (blocksContainer != null) 
        {
            blockObj.transform.SetParent(blocksContainer);
            blockObj.transform.localPosition = localSpawnPosition;
            blockObj.transform.localRotation = Quaternion.identity; // Ensure consistent local rotation
        }
        else
        {
            // Fallback: if blocksContainer isn't assigned, parent to GridManager's transform
            //Debug.LogWarning("blocksContainer is not assigned in GridManager. Spawning block relative to GridManager's root. Consider assigning blocksContainer as a child of GridManager.");
            blockObj.transform.SetParent(transform); // Parent to the GridManager itself
            blockObj.transform.localPosition = localSpawnPosition;
            blockObj.transform.localRotation = Quaternion.identity;
        }

        blockObj.transform.localScale = initialBlockScale;

        BlockController controller = blockObj.GetComponent<BlockController>();
        if (controller == null)
        {
            //Debug.LogError($"BlockController component not found on the provided agent block prefab '{blockPrefabToSpawn.name}'!", this);
            Destroy(blockObj);
            return;
        }

        // Initialize the controller with the shape derived from the prefab
        controller.Initialize(blockShape, this);
        currentBlocks.Add(controller);

        // We might need to add the shape to availableBlocks if it's not there?
        // This assumes the agent only picks from shapes already in availableBlocks.
        if (!availableBlocks.Any(b => b.shapeName == blockShape.shapeName))
        {
             //Debug.LogWarning($"Agent spawned shape '{blockShape.shapeName}' which was not in the initial availableBlocks list.", this);
             // Add it dynamically? Might mess up DDA logic if we switch back.
             // availableBlocks.Add(blockShape);
        }


        ////Debug.log($"Specific block '{blockShape.shapeName}' spawned by Agent request. Total blocks: {currentBlocks.Count}");
    }

    /// <summary>
    /// Checks if the game is over. Game over occurs if there are blocks in the dock,
    /// but none of them can be placed anywhere on the grid.
    /// </summary>
    /// <returns>True if game over, false otherwise.</returns>
    public bool CheckGameOver()
    {
        // If lines are currently being cleared, it's not game over yet
        if (isClearingLines)
        {
            return false;
        }
        
        // If there are no blocks left to place, it's not game over yet (wait for spawn)
        if (currentBlocks.Count == 0)
        {
            return false;
        }

        // Check if *any* of the currently offered blocks can be placed
        foreach (var blockController in currentBlocks)
        {
            if (blockController != null && blockController.shape != null)
            {
                if (IsShapePlaceableAnywhere(blockController.shape))
                {
                    // Found at least one placeable block, game is not over
                    return false;
                }
            }
        }

        // If we looped through all current blocks and none were placeable, it's game over
        return true;
    }

     /// <summary>
    /// Gets the number of lines cleared by the most recent successful block placement.
    /// </summary>
    /// <returns>Number of lines cleared.</returns>
    public int GetLastLinesCleared()
    {
        return lastLinesCleared;
    }

    /// <summary>
    /// Overloaded version that accepts a BlockShape directly instead of a prefab.
    /// Uses the default blockPrefab and applies the shape to it.
    /// </summary>
    /// <param name="shape">The BlockShape to use for this block.</param>
    public void SpawnSpecificBlock(BlockShape shape)
    {
        Debug.Log($"SpawnSpecificBlock called with shape: {(shape?.shapeName ?? "NULL")}");
        
        if (shape == null)
        {
            Debug.LogError("Agent provided a null block shape to spawn!", this);
            return;
        }

        Debug.Log($"SpawnSpecificBlock: Current blocks count before spawn: {currentBlocks.Count}");
        
        // If we have 3 blocks already, don't spawn
        if (currentBlocks.Count >= 3)
        {
            Debug.LogWarning("Agent tried to spawn a block, but dock is full (3 blocks). Ignoring request.", this);
            return;
        }

        // Make sure we have a block prefab
        if (blockPrefab == null)
        {
            Debug.LogError("BlockPrefab is not assigned in GridManager! Cannot spawn block.", this);
            return;
        }

        ////Debug.log($"Agent requested spawning block with shape: {shape.shapeName}");

        // Calculate LOCAL spawn position (spread blocks horizontally) - MATCH normal SpawnBlock logic
        float spacing = dockBlockSpacing;
        float xPosition = blockSpawnArea.x + (currentBlocks.Count) * spacing;
        Vector3 localSpawnPosition = new Vector3(xPosition, blockSpawnArea.y, 0); // This is relative to blocksContainer

        // Instantiate without specific world position/rotation yet - MATCH normal SpawnBlock logic
        GameObject blockObj = Instantiate(blockPrefab);

        if (blocksContainer != null) 
        {
            blockObj.transform.SetParent(blocksContainer);
            blockObj.transform.localPosition = localSpawnPosition;
            blockObj.transform.localRotation = Quaternion.identity; // Ensure consistent local rotation
        }
        else
        {
            // Fallback: if blocksContainer isn't assigned, parent to GridManager's transform
            //Debug.LogWarning("blocksContainer is not assigned in GridManager. Spawning block relative to GridManager's root. Consider assigning blocksContainer as a child of GridManager.");
            blockObj.transform.SetParent(transform); // Parent to the GridManager itself
            blockObj.transform.localPosition = localSpawnPosition;
            blockObj.transform.localRotation = Quaternion.identity;
        }

        blockObj.transform.localScale = initialBlockScale;

        BlockController controller = blockObj.GetComponent<BlockController>();
        if (controller == null)
        {
            Debug.LogError("BlockController component not found on blockPrefab!", this);
            Destroy(blockObj);
            return;
        }

        Debug.Log($"SpawnSpecificBlock: Found BlockController, initializing with shape '{shape.shapeName}'");

        // Initialize with the provided shape
        controller.Initialize(shape, this);
        
        Debug.Log($"SpawnSpecificBlock: About to add controller to currentBlocks. Count before: {currentBlocks.Count}");
        currentBlocks.Add(controller);
        Debug.Log($"SpawnSpecificBlock: Successfully spawned '{shape.shapeName}'. Total blocks: {currentBlocks.Count}");
    }

    /// <summary>
    /// An additional overload that takes both a prefab and a shape. 
    /// Useful when you want to use a specific prefab with a specific shape.
    /// </summary>
    public void SpawnSpecificBlock(GameObject prefab, BlockShape shape)
    {
        // Use default prefab if none provided
        if (prefab == null)
        {
            if (blockPrefab == null)
            {
                //Debug.LogError("No prefab provided and default blockPrefab is null. Cannot spawn block!", this);
                return;
            }
            SpawnSpecificBlock(shape);
            return;
        }

        // If shape is null, try to get it from the prefab
        if (shape == null)
        {
            shape = prefab.GetComponentInChildren<BlockShape>();
            if (shape == null)
            {
                shape = prefab.GetComponent<BlockShape>();
                if (shape == null)
                {
                    //Debug.LogError("Neither a BlockShape was provided nor could one be found on the prefab!", this);
                    return;
                }
            }
            SpawnSpecificBlock(prefab); // Use the original method with just the prefab
            return;
        }

        // Both prefab and shape provided - use the prefab but with the specified shape
        if (currentBlocks.Count >= 3)
        {
            //Debug.LogWarning("Agent tried to spawn a block, but dock is full (3 blocks). Ignoring request.", this);
            return;
        }

        ////Debug.log($"Agent requested spawning block with prefab and shape: {shape.shapeName}");

        // Calculate LOCAL spawn position (spread blocks horizontally) - MATCH normal SpawnBlock logic
        float spacing = dockBlockSpacing;
        float xPosition = blockSpawnArea.x + (currentBlocks.Count) * spacing;
        Vector3 localSpawnPosition = new Vector3(xPosition, blockSpawnArea.y, 0); // This is relative to blocksContainer

        // Instantiate without specific world position/rotation yet - MATCH normal SpawnBlock logic
        GameObject blockObj = Instantiate(prefab);

        if (blocksContainer != null) 
        {
            blockObj.transform.SetParent(blocksContainer);
            blockObj.transform.localPosition = localSpawnPosition;
            blockObj.transform.localRotation = Quaternion.identity; // Ensure consistent local rotation
        }
        else
        {
            // Fallback: if blocksContainer isn't assigned, parent to GridManager's transform
            //Debug.LogWarning("blocksContainer is not assigned in GridManager. Spawning block relative to GridManager's root. Consider assigning blocksContainer as a child of GridManager.");
            blockObj.transform.SetParent(transform); // Parent to the GridManager itself
            blockObj.transform.localPosition = localSpawnPosition;
            blockObj.transform.localRotation = Quaternion.identity;
        }

        blockObj.transform.localScale = initialBlockScale;

        BlockController controller = blockObj.GetComponent<BlockController>();
        if (controller == null)
        {
            //Debug.LogError("BlockController component not found on provided prefab!", this);
            Destroy(blockObj);
            return;
        }

        // Initialize with the provided shape
        controller.Initialize(shape, this);
        currentBlocks.Add(controller);

        Debug.Log($"SpawnSpecificBlock (prefab): Successfully spawned '{shape.shapeName}'. Total blocks: {currentBlocks.Count}");
    }

    // --- End ML-Agent Public Methods ---

    // --- Grid State & Heuristic Calculation Helpers ---

    /// <summary>
    /// Returns a copy of the current grid state.
    /// </summary>
    public int[,] GetGridState() {
        // Return a clone to prevent external modification of the internal grid state
        return (int[,])grid.Clone();
    }

    /// <summary>
    /// Helper: Calculates the sum of the heights of the highest filled cell in each column for a GIVEN grid state.
    /// </summary>
    public int CalculateAggregateHeight(int[,] gridState) {
        if (gridState == null) return 0;
        int totalHeight = 0;
        int width = gridState.GetLength(0);
        int height = gridState.GetLength(1);
        for (int x = 0; x < width; x++) {
            for (int y = height - 1; y >= 0; y--) { // Scan down from the top
                if (gridState[x, y] == 1) {
                    totalHeight += (y + 1); // Add height of this column (1-based)
                    break; // Move to the next column
                }
            }
        }
        return totalHeight;
    }

    /// <summary>
    /// Helper: Calculates the sum of absolute differences between adjacent column heights for a GIVEN grid state.
    /// Measures the unevenness of the board surface.
    /// </summary>
    public int CalculateBumpiness(int[,] gridState) {
        if (gridState == null) return 0;
        int bumpiness = 0;
        int width = gridState.GetLength(0);
        int height = gridState.GetLength(1);
        int[] colHeights = new int[width];

        // Calculate height of each column (highest filled cell + 1)
         for (int x = 0; x < width; x++) {
             int colHeight = 0;
             for (int y = height - 1; y >= 0; y--) { // Scan down
                 if (gridState[x, y] == 1) {
                     colHeight = y + 1; // 1-based height
                     break;
                 }
             }
             colHeights[x] = colHeight;
         }

        // Sum absolute differences between adjacent column heights
        for (int x = 0; x < width - 1; x++) {
            bumpiness += Mathf.Abs(colHeights[x] - colHeights[x + 1]);
        }
        return bumpiness;
    }

    /// <summary>
    /// Helper: Counts the number of holes for a GIVEN grid state.
    /// A hole is an empty cell where all four directions (up, down, left, right)
    /// are blocked either by a filled cell or by the grid boundary.
    /// </summary>
    // In GridManager.cs

// In GridManager.cs

public int CalculateHoles(int[,] gridState)
{
    if (gridState == null) return 0;
    int holes = 0;
    int width = gridState.GetLength(0);  // Number of columns
    int height = gridState.GetLength(1); // Number of rows

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (gridState[x, y] == 0) // If the current cell at (x,y) is empty
            {
                // Check if this empty cell is "blocked" from all four directions.
                // A direction is considered "blocked" if it's either a grid boundary
                // OR if the adjacent cell in that direction is occupied by a block (== 1).

                // Check North: Is it the top boundary OR is the cell above occupied?
                bool northBlocked = (y + 1 >= height) || (gridState[x, y + 1] == 1);

                // Check South: Is it the bottom boundary OR is the cell below occupied?
                bool southBlocked = (y - 1 < 0)    || (gridState[x, y - 1] == 1);

                // Check East: Is it the right boundary OR is the cell to the east occupied?
                bool eastBlocked  = (x + 1 >= width)  || (gridState[x + 1, y] == 1);

                // Check West: Is it the left boundary OR is the cell to the west occupied?
                bool westBlocked  = (x - 1 < 0)    || (gridState[x - 1, y] == 1);

                // If all four directions are blocked (either by boundary or by another block)
                if (northBlocked && southBlocked && eastBlocked && westBlocked)
                {
                    holes++;
                }
            }
        }
    }
    return holes;
}

    // In GridManager.cs
/*
    private BlockShape GetNextBlockShape(List<BlockController> blocksInCurrentSpawnBatch, HashSet<string> excludedShapeNames = null)
    {
    // --- OPTION 1: PlayerAgentIsTrainingMode (from previous responses, kept for reference if needed) ---
    // if (gameManager != null && gameManager.playerAgentIsTrainingMode) { /* existing random logic  }

    // --- OPTION 2: Your new request: PURE RANDOM WITH EQUAL WEIGHTS ---
    if (gameManager != null && gameManager.useRandomBlockSpawningEqualWeights)
    {
        //Debug.Log("<color=lightblue>[GetNextBlockShape]</color> Using PURE RANDOM (Equal Weights) block selection.");

        if (availableBlocks == null || availableBlocks.Count == 0)
        {
//            //Debug.LogError("<color=red>[GetNextBlockShape]</color> (Random Mode) No availableBlocks defined in GridManager! Cannot select random block.");
            return null; // Or a default tiny block
        }

        List<BlockShape> candidatePool = new List<BlockShape>(availableBlocks);

        // Filter out shapes already present in the CURRENT dock offering (blocksInCurrentSpawnBatch) to avoid direct duplicates IN THIS SET.
        // This doesn't prevent the same shape appearing in the next set.
        if (preventDuplicateBlocks && blocksInCurrentSpawnBatch.Count > 0) {
            HashSet<string> currentBatchNames = new HashSet<string>(blocksInCurrentSpawnBatch.Select(bc => bc.shape.shapeName));
            candidatePool.RemoveAll(shape => currentBatchNames.Contains(shape.shapeName) &&
                                       // Special allowance for 3x3 if board is empty, even if preventDuplicateBlocks is true
                                       ! (shape.shapeName == "3x3" && allow3x3DuplicatesWhenEmpty && CalculateBoardDensity() < emptyBoardThreshold) );
        }

        // Also apply temporary exclusions if any (e.g., from set repetition logic if that's active upstream)
        if (excludedShapeNames != null && excludedShapeNames.Count > 0) {
            candidatePool.RemoveAll(shape => excludedShapeNames.Contains(shape.shapeName));
        }

        if (candidatePool.Count > 0)
        {
            BlockShape chosenBlock = candidatePool[Random.Range(0, candidatePool.Count)];
//            //Debug.Log($"<color=lightblue>[GetNextBlockShape]</color> (Random Mode) Chose: {chosenBlock.shapeName} from {candidatePool.Count} candidates.");
            return chosenBlock;
        }
        else if (availableBlocks.Count > 0) // Fallback: if filtering removed all, pick any from original list
        {
//            //Debug.LogWarning("<color=yellow>[GetNextBlockShape]</color> (Random Mode) Filtering removed all candidates. Picking from original availableBlocks list (may include duplicates in set).");
            BlockShape chosenBlock = availableBlocks[Random.Range(0, availableBlocks.Count)];
//            //Debug.Log($"<color=lightblue>[GetNextBlockShape]</color> (Random Mode Fallback) Chose: {chosenBlock.shapeName}.");
            return chosenBlock;
        }
        else // Should not happen if initial check passed
        {
//            //Debug.LogError("<color=red>[GetNextBlockShape]</color> (Random Mode) availableBlocks became empty unexpectedly after initial check!");
            return null;
        }
    }

    // --- FALLBACK TO EXISTING DDA LOGIC ---
//    //Debug.Log("<color=gray>[GetNextBlockShape]</color> Using DDA/Fun Factor block selection.");
    // ... (rest of your existing GetNextBlockShape DDA logic) ...
    // bool isInitialSpawn = ...
    // float currentBoardDensity = ...
    // --- End Grid State & Heuristic Calculation Helpers ---

    // FIXME: Add actual DDA logic or a proper fallback here.
    // For now, returning null to resolve CS0161. This might cause issues if DDA is expected.
//    //Debug.LogWarning("[GetNextBlockShape] DDA logic is not implemented. Returning null as a fallback.");
    return null; 
    } 
    */
}

