using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.MLAgents;
public class BlockController : MonoBehaviour
{
    public BlockShape shape;
    private GridManager gridManager;

    private PlayerAgent agent;
    

    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private float cellScale = 0.5125f;
    [SerializeField] private float cellSpacing = 0.1f; // Add spacing between cells
    [SerializeField] private float clickUpwardOffset = 0.7f; // Increased for more pronounced effect
    [SerializeField] private float exponentialFactor = 8.0f; // Increased from 6.0 to 8.0 for an even more dramatic curve
    
    private List<GameObject> blockCells = new List<GameObject>();
    private Vector3 startPosition; // Original position in the dock
    private Vector3 dockScale; // Store the small scale used in dock
    private Vector3 dragOffset; // Offset between mouse click and block pivot
    
    private bool isDragging = false;
    private bool hasAppliedJump = false; // Track if we've applied the jump
    private Camera mainCamera;
    private Vector3 initialMousePos; // Store the initial mouse position when dragging starts
    
    
    // Grid hover indication
    private Vector2Int currentGridPosition = new Vector2Int(-1, -1);
    private List<GameObject> hoverIndicators = new List<GameObject>();
    private Color validPlacementColor = new Color(0.2f, 1f, 0.2f, 0.7f); // Slightly transparent green
    private Color invalidPlacementColor = new Color(1f, 0.2f, 0.2f, 0.7f); // Slightly transparent red
    private List<GameObject> lineCompletionIndicators = new List<GameObject>(); // For highlighting potential line completions
    private bool willCompleteLine = false; // Track if the current placement will complete a line
    
    public void Initialize(BlockShape blockShape, GridManager manager)
    {
        shape = blockShape;
        gridManager = manager;
        mainCamera = Camera.main;
        
        // Keep track of the spawn position (dock position)
        startPosition = transform.position;
        // Store the initial scale set by GridManager
        dockScale = transform.localScale; 
        
        // Simple mobile device check for Samsung Z Fold and similar ultrawide devices
        bool isUltrawide = (float)Screen.width / Screen.height > 2.0f;
        if (isUltrawide)
        {
            // For ultrawide, slightly reduce cell scale to fit better
            cellScale = 0.45f;
            cellSpacing = 0.09f;
        }
        
        CreateVisual();
       // CreateHoverIndicators();
        // Don't UpdatePosition here, let the spawner handle initial pos
    }

   
    
    void CreateVisual()
    {
        // Clear any existing cells
        foreach (var cell in blockCells)
        {
            Destroy(cell);
        }
        blockCells.Clear();
        
        // Ensure parent object has a BoxCollider2D
        BoxCollider2D parentCollider = GetComponent<BoxCollider2D>();
        if (parentCollider == null)
        {
            parentCollider = gameObject.AddComponent<BoxCollider2D>();
            parentCollider.isTrigger = true; // Make it a trigger so it doesn't physically collide
        }

        // Keep track of min/max bounds to size the parent collider
        Vector2 minBounds = Vector2.positiveInfinity;
        Vector2 maxBounds = Vector2.negativeInfinity;

        // Create new cells based on the shape
        foreach (Vector2Int cellPos in shape.cells)
        {
            GameObject cell = Instantiate(cellPrefab, transform);
            Vector3 localCellPos = new Vector3(
                cellPos.x * (cellScale + cellSpacing),
                cellPos.y * (cellScale + cellSpacing),
                0);
            cell.transform.localPosition = localCellPos;

            // Update bounds based on cell position and scale
            minBounds.x = Mathf.Min(minBounds.x, localCellPos.x - cellScale / 2f);
            minBounds.y = Mathf.Min(minBounds.y, localCellPos.y - cellScale / 2f);
            maxBounds.x = Mathf.Max(maxBounds.x, localCellPos.x + cellScale / 2f);
            maxBounds.y = Mathf.Max(maxBounds.y, localCellPos.y + cellScale / 2f);
            
            // Scale the cell
            cell.transform.localScale = new Vector3(1f, 1f, 1f) * cellScale;
            
            blockCells.Add(cell);
            
            // Set cell color using the shape's color
            SpriteRenderer renderer = cell.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = shape.blockColor;
            }
        }

        // Add buffer space around the collider to make it easier to tap
        float colliderBuffer = 1.0f; // Increased from 0.5f to 1.0f for an even larger tap area
        Vector2 colliderSize = maxBounds - minBounds;
        colliderSize += new Vector2(colliderBuffer, colliderBuffer);
        Vector2 colliderOffset = minBounds + (colliderSize / 2f);

        parentCollider.size = colliderSize;
        parentCollider.offset = colliderOffset;
    }
    
    // Create hover indicators for grid placement preview
    // TODO: Remove this
    /*
    private void CreateHoverIndicators()
    {
        // Clean up any existing indicators
        foreach (var indicator in hoverIndicators)
        {
            Destroy(indicator);
        }
        hoverIndicators.Clear();
        
        // Create new indicators for each cell in the shape
        foreach (Vector2Int cellPos in shape.cells)
        {
            GameObject indicator = new GameObject($"Indicator_{cellPos}");
            indicator.transform.SetParent(null); // Make sure they're in world space
            
            SpriteRenderer renderer = indicator.AddComponent<SpriteRenderer>();
            
            // Use the same sprite as the cell prefab
            if (cellPrefab != null && cellPrefab.GetComponent<SpriteRenderer>() != null)
            {
                renderer.sprite = cellPrefab.GetComponent<SpriteRenderer>().sprite;
            }
            else
            {
                // Fallback to a default sprite if needed
                renderer.sprite = Resources.Load<Sprite>("DefaultSquare");
            }
            
            // Set initial scale - same size as actual blocks
            indicator.transform.localScale = new Vector3(cellScale, cellScale, 1f);
            
            // Start with valid placement color
            renderer.color = new Color(0.0f, 1f, 0.0f, 0.8f); // Bright green with 80% opacity
            
            // Set sorting order to be behind the actual blocks but definitely visible
            renderer.sortingOrder = 0; // Try a different sorting order
            
            // Hide initially
            indicator.SetActive(false);
            
            hoverIndicators.Add(indicator);
        }
    }
    */
    // Get a static rainbow color based on position
    private Color GetStaticRainbowColor(Vector2Int position)
    {
        // Generate a fixed color based on position in the grid
        // This creates a nice pattern without animation
        float hue = ((position.x * 0.1f) + (position.y * 0.17f)) % 1.0f;
        
        // Convert HSV to RGB for a nice rainbow distribution
        Color color = Color.HSVToRGB(hue, 0.9f, 1.0f);
        color.a = 0.8f; // 80% opacity
        return color;
    }
    
    // Update hover indicators based on current grid position
    private void UpdateHoverIndicators(Vector2Int gridPos)
    {
        currentGridPosition = gridPos;
        
        // Check if this is a valid placement
        bool canPlace = gridManager.CanPlaceBlock(shape, gridPos);
        Color indicatorColor = canPlace ? validPlacementColor : invalidPlacementColor;
        
        // If placement is valid, check for potential line completions
        willCompleteLine = false;
        if (canPlace)
        {
            // Check for line completions
            List<Vector2Int> cellsToPlace = new List<Vector2Int>();
            foreach (Vector2Int cell in shape.cells)
            {
                Vector2Int pos = gridPos + cell;
                if (pos.x >= 0 && pos.x < gridManager.gridWidth && 
                    pos.y >= 0 && pos.y < gridManager.gridHeight)
                {
                    cellsToPlace.Add(pos);
                }
            }
            
            // Get potential line completions from GridManager
            List<List<Vector2Int>> linesToComplete = gridManager.GetPotentialLineCompletions(cellsToPlace);
            
            // If there are lines to complete, set the flag and highlight them
            if (linesToComplete.Count > 0)
            {
                willCompleteLine = true;
                HighlightCompletedLines(linesToComplete);
            }
            else
            {
                HideLineCompletionIndicators();
            }
        }
        else
        {
            // Hide line completion indicators if placement is invalid
            HideLineCompletionIndicators();
        }
        
        // Make sure all block cells have full opacity
        foreach (GameObject blockCell in blockCells)
        {
            SpriteRenderer renderer = blockCell.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                Color color = renderer.color;
                color.a = 1.0f; // Full opacity
                renderer.color = color;
                
                // Make sure the block cells are on top
                renderer.sortingOrder = 1;
            }
        }
        
        // Update each indicator position and color
        for (int i = 0; i < shape.cells.Count && i < hoverIndicators.Count; i++)
        {
            Vector2Int cellGridPos = gridPos + shape.cells[i];
            
            // Convert grid position to world position
            GameObject indicator = hoverIndicators[i];
            
            // If the cell would be outside the grid, hide the indicator
            if (cellGridPos.x < 0 || cellGridPos.x >= gridManager.gridWidth ||
                cellGridPos.y < 0 || cellGridPos.y >= gridManager.gridHeight)
            {
                indicator.SetActive(false);
                continue;
            }
            
            // Position the indicator at the grid cell
            Vector3 gridCellPosition = gridManager.GetWorldPositionFromGrid(cellGridPos);
            indicator.transform.position = gridCellPosition;
            
            // Set color and show - use rainbow for line completions
            SpriteRenderer renderer = indicator.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                if (willCompleteLine)
                {
                    // Use rainbow colors when completing a line
                    float colorPos = ((float)cellGridPos.x / gridManager.gridWidth + 
                                    (float)cellGridPos.y / gridManager.gridHeight) * 0.5f + 
                                    (Time.time * 0.3f);
                    renderer.color = GetRainbowColor(colorPos % 1);
                }
                else
                {
                    renderer.color = indicatorColor;
                }
                
                // Ensure indicator is visible with proper opacity
                Color color = renderer.color;
                color.a = 0.7f; // 70% opacity
                renderer.color = color;
                
                // Ensure the indicator is behind the block cells
                renderer.sortingOrder = -1;
            }
            
            indicator.SetActive(true);
        }
    }
    
    // Hide all hover indicators
    private void HideHoverIndicators()
    {
        foreach (var indicator in hoverIndicators)
        {
            if (indicator != null)
            {
                indicator.SetActive(false);
            }
        }
        
        // Also hide line completion indicators
        HideLineCompletionIndicators();
        
        currentGridPosition = new Vector2Int(-1, -1); // Reset current position
    }
    
    // Check for potential line completions and highlight them
    private void CheckForLineCompletions(Vector2Int gridPos)
    {
        // Clean up existing line completion indicators
        HideLineCompletionIndicators();
        
        // Simulate placement and check for line completions
        List<Vector2Int> cellsToPlace = new List<Vector2Int>();
        foreach (Vector2Int cell in shape.cells)
        {
            Vector2Int pos = gridPos + cell;
            if (pos.x >= 0 && pos.x < gridManager.gridWidth && 
                pos.y >= 0 && pos.y < gridManager.gridHeight)
            {
                cellsToPlace.Add(pos);
            }
        }
        
        // Get potential line completions from GridManager
        List<List<Vector2Int>> linesToComplete = gridManager.GetPotentialLineCompletions(cellsToPlace);
        
        // If there are lines to complete, highlight them
        if (linesToComplete.Count > 0)
        {
            HighlightCompletedLines(linesToComplete);
        }
    }
    
    // Hide line completion indicators
    private void HideLineCompletionIndicators()
    {
        foreach (var indicator in lineCompletionIndicators)
        {
            Destroy(indicator);
        }
        lineCompletionIndicators.Clear();
    }
    
    // Highlight lines that would be completed if the block is placed
    private void HighlightCompletedLines(List<List<Vector2Int>> linesToComplete)
    {
        // Clear previous indicators
        HideLineCompletionIndicators();
        
        foreach (var line in linesToComplete)
        {
            foreach (var cell in line)
            {
                // Skip cells that are part of the current block (already have hover indicators)
                bool isPartOfPlacement = false;
                foreach (Vector2Int placementCell in shape.cells)
                {
                    if (cell == currentGridPosition + placementCell)
                    {
                        isPartOfPlacement = true;
                        break;
                    }
                }
                
                if (!isPartOfPlacement)
                {
                    // Create indicator for this cell
                    GameObject indicator = new GameObject($"LineCompletion_{cell}");
                    SpriteRenderer renderer = indicator.AddComponent<SpriteRenderer>();
                    
                    // Use the same sprite as the cell prefab
                    if (cellPrefab != null && cellPrefab.GetComponent<SpriteRenderer>() != null)
                    {
                        renderer.sprite = cellPrefab.GetComponent<SpriteRenderer>().sprite;
                    }
                    
                    // Set initial scale and position - same size as actual blocks
                    indicator.transform.localScale = new Vector3(cellScale, cellScale, 1f);
                    indicator.transform.position = gridManager.GetWorldPositionFromGrid(cell);
                    
                    // Set static rainbow effect color
                    renderer.color = GetStaticRainbowColor(cell);
                    
                    // Make sure the indicator is behind existing cells but visible
                    renderer.sortingOrder = 3;
                    
                    lineCompletionIndicators.Add(indicator);
                }
            }
        }
    }
    
    // Get a rainbow color for line completion indicators - improved colors
    private Color GetRainbowColor(float position)
    {
        // Enhanced rainbow effect with more vibrant colors
        float frequency = 2.0f * Mathf.PI;
        
        // More vibrant and saturated rainbow
        float r = Mathf.Clamp01(Mathf.Sin(frequency * position) * 0.75f + 0.25f);
        float g = Mathf.Clamp01(Mathf.Sin(frequency * (position + 0.33f)) * 0.75f + 0.25f);
        float b = Mathf.Clamp01(Mathf.Sin(frequency * (position + 0.67f)) * 0.75f + 0.25f);
        
        // Boost the saturation by increasing the highest channel
        float max = Mathf.Max(r, Mathf.Max(g, b));
        if (max < 0.8f)
        {
            float boost = 0.8f / (max + 0.0001f);
            r *= boost;
            g *= boost;
            b *= boost;
        }
        
        return new Color(r, g, b, 0.8f); // Increased opacity to 80%
    }
    
    void OnMouseDown()
    {
        if (isDragging) return; // Prevent re-triggering if already dragging

        // Store the starting position and mouse position
        Vector3 initialPosition = transform.position;
        initialMousePos = GetMouseWorldPos();
        
        // Store a simple offset for X position tracking
        dragOffset = new Vector3(initialPosition.x - initialMousePos.x, 0, 0);
        
        // Move block up for visual feedback
        transform.position = initialPosition + Vector3.up * clickUpwardOffset;
        
        // Scale up
        transform.localScale = Vector3.one;
        
        // Mark as dragging
        isDragging = true;
        
        // Initialize hover indicators based on new position
        Vector2Int gridPosition = gridManager.WorldToGridPosition(transform.position);
        //ForceShowHoverIndicators(gridPosition);
    }
    
    // New method to force indicators to be visible
    private void ForceShowHoverIndicators(Vector2Int gridPos)
    {
        // Check if this is a valid placement
        bool canPlace = gridManager.CanPlaceBlock(shape, gridPos);
        
        currentGridPosition = gridPos;
        
        // Make sure all block cells have full opacity and high z-order
        foreach (GameObject blockCell in blockCells)
        {
            SpriteRenderer renderer = blockCell.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, 1.0f);
                renderer.sortingOrder = 10; // Very high to ensure on top
            }
        }
        
        // Only check for line completions if the placement is valid
        willCompleteLine = false;
        HashSet<Vector2Int> cellsInCompletedLines = new HashSet<Vector2Int>();
        
        if (canPlace)
        {
            // Check for potential line completions
            List<Vector2Int> cellsToPlace = new List<Vector2Int>();
            foreach (Vector2Int cell in shape.cells)
            {
                Vector2Int pos = gridPos + cell;
                if (pos.x >= 0 && pos.x < gridManager.gridWidth && 
                    pos.y >= 0 && pos.y < gridManager.gridHeight)
                {
                    cellsToPlace.Add(pos);
                }
            }
            
            // Get potential line completions from GridManager
            List<List<Vector2Int>> linesToComplete = gridManager.GetPotentialLineCompletions(cellsToPlace);
            willCompleteLine = linesToComplete.Count > 0;
            
            // Create a set of all cells that are part of a completed line
            if (willCompleteLine)
            {
                foreach (var line in linesToComplete)
                {
                    foreach (var cell in line)
                    {
                        cellsInCompletedLines.Add(cell);
                    }
                }
                
                // If there are lines to complete, highlight them with static colors
                HighlightCompletedLines(linesToComplete);
            }
            else
            {
                HideLineCompletionIndicators();
            }
        }
        else
        {
            // If placement is invalid, ensure no line completion indicators are shown
            HideLineCompletionIndicators();
        }
        
        // Force all indicators to be visible and correctly positioned
        for (int i = 0; i < shape.cells.Count && i < hoverIndicators.Count; i++)
        {
            Vector2Int cellGridPos = gridPos + shape.cells[i];
            GameObject indicator = hoverIndicators[i];
            
            // If outside grid, still show it but with transparency
            bool isInGrid = cellGridPos.x >= 0 && cellGridPos.x < gridManager.gridWidth &&
                            cellGridPos.y >= 0 && cellGridPos.y < gridManager.gridHeight;
                            
            // Position indicator at grid cell position
            Vector3 gridCellPosition;
            if (isInGrid)
            {
                gridCellPosition = gridManager.GetWorldPositionFromGrid(cellGridPos);
            }
            else
            {
                // Estimate position for cells outside grid
                Vector3 basePos = gridManager.GetWorldPositionFromGrid(new Vector2Int(
                    Mathf.Clamp(cellGridPos.x, 0, gridManager.gridWidth-1),
                    Mathf.Clamp(cellGridPos.y, 0, gridManager.gridHeight-1)
                ));
                
                // Adjust for cells outside grid
                gridCellPosition = basePos + new Vector3(
                    (cellGridPos.x < 0 ? cellGridPos.x : cellGridPos.x >= gridManager.gridWidth ? cellGridPos.x - gridManager.gridWidth + 1 : 0) * gridManager.cellSize,
                    (cellGridPos.y < 0 ? cellGridPos.y : cellGridPos.y >= gridManager.gridHeight ? cellGridPos.y - gridManager.gridHeight + 1 : 0) * gridManager.cellSize,
                    0
                );
            }
            
            indicator.transform.position = gridCellPosition;
            
            // Set indicator color based on placement validity and make sure Z position is correct
            SpriteRenderer renderer = indicator.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                // Check if this specific cell is part of a completed line
                bool cellCompletesLine = willCompleteLine && isInGrid && canPlace && cellsInCompletedLines.Contains(cellGridPos);
                
                if (cellCompletesLine)
                {
                    // Use static rainbow colors only for cells that are part of line completions
                    renderer.color = GetStaticRainbowColor(cellGridPos);
                }
                else
                {
                    // Use green/red for valid/invalid placements
                    Color indicatorColor = canPlace ? 
                        new Color(0.0f, 1f, 0.0f, 0.8f) :  // Bright green
                        new Color(1f, 0.0f, 0.0f, 0.8f);   // Bright red
                    
                    renderer.color = isInGrid ? indicatorColor : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }
                
                renderer.sortingOrder = 5; // Lower than blocks but still visible
            }
            
            // Force the indicator to be active
            indicator.SetActive(true);
        }
    }
    
    void OnMouseDrag()
    {
        if (!isDragging) return;

        // Get current mouse position
        Vector3 mouseWorldPos = GetMouseWorldPos();

        // --- X POSITION --- 
        // Calculate block's X based on mouse + X offset only
        float targetBlockX = mouseWorldPos.x + dragOffset.x;

        // --- Y POSITION --- 
        // Get grid boundaries in world coordinates
        float gridBottom = gridManager.GetWorldPositionFromGrid(new Vector2Int(0, 0)).y;
        float gridTop = gridManager.GetWorldPositionFromGrid(new Vector2Int(0, gridManager.gridHeight - 1)).y;
        float gridHeight = gridTop - gridBottom;

        // Always keep the block at least clickUpwardOffset above the current finger position
        float minY = mouseWorldPos.y + clickUpwardOffset;
        
        // Calculate finger progress relative to the grid (0 to 1)
        float fingerRelativeY = (mouseWorldPos.y - gridBottom) / gridHeight;
        fingerRelativeY = Mathf.Clamp01(fingerRelativeY); // Clamp for exponential calculation
        
        // Apply exponential curve
        float k = exponentialFactor;
        float exponentialValue = (Mathf.Exp(k * fingerRelativeY) - 1) / (Mathf.Exp(k) - 1);
        
        if (float.IsNaN(exponentialValue) || float.IsInfinity(exponentialValue)) {
            exponentialValue = fingerRelativeY; // Fallback
        }
        
        // Calculate additional exponential lift based on finger position
        float additionalLift = exponentialValue * gridHeight;
        
        // Ensure final Y is at least minimum height (minY) plus any additional exponential lift
        float targetBlockY = minY + additionalLift;
        
        // Set position directly - no interpolation
        transform.position = new Vector3(targetBlockX, targetBlockY, 0f);

        // Update indicators
        Vector2Int gridPosition = gridManager.WorldToGridPosition(transform.position);
       // ForceShowHoverIndicators(gridPosition);
    }
    
    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;
        
        // Hide hover indicators
       // HideHoverIndicators();
        
        // Try to place the block
        Vector2Int gridPosition = gridManager.WorldToGridPosition(transform.position);
        bool placed = gridManager.TryPlaceBlock(shape, gridPosition);
        
        if (placed)
        {
            gridManager.RemoveBlock(this);
            
            // Check if the dock is now empty after placing this block
            if (gridManager.currentBlocks.Count == 0)
            {
                gridManager.SpawnBlocks(3); // Spawn a new batch of 3
            }

            Destroy(gameObject);
            //gridManager.isPlaced = true;
           // agent.RequestDecision();
            Debug.Log("Step:");
        }
        else
        {   
            // Return to start position (original dock position) and small scale if not placed
            // Since clickUpwardOffset is removed, this is now consistent.
            transform.position = startPosition; 
            transform.localScale = dockScale; // Shrink back down to the initial dock scale
        }
    }
    
    private void OnDestroy()
    {
        // Clean up hover indicators when the block is destroyed
        foreach (var indicator in hoverIndicators)
        {
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }
        
        // Also clean up line completion indicators
        foreach (var indicator in lineCompletionIndicators)
        {
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }
    }

    Vector3 GetMouseWorldPos()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = mainCamera.nearClipPlane + 10; // Distance from camera
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        return worldPos;
    }
}   