// SimulatedPlayer.cs
// Automated player bot used to simulate gameplay for agent training.
// Uses a heuristic score to choose the best placement.
// Corrected syntax for helper methods.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Required for LINQ operations

public class SimulatedPlayer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the main Grid Manager")]
    [SerializeField] private GridManager gridManager;
    [Tooltip("Reference to the Game Manager to check for manual play mode")]
    [SerializeField] private GameManager gameManager; // Added GameManager reference

    [Header("Heuristic Weights (CRITICAL TUNING PARAMETERS)")]
    [Tooltip("Weight for lines cleared (applied to lines*lines)")]
    [SerializeField] private float weightLinesCleared = 10.0f; // High weight for line clears (quadratic scaling below)
    [Tooltip("Weight for aggregate height of the board (lower height is better -> negative weight)")]
    [SerializeField] private float weightAggregateHeight = -0.1f; // Penalize height
    [Tooltip("Weight for board bumpiness (flatter board is better -> negative weight)")]
    [SerializeField] private float weightBumpiness = -0.2f;    // Penalize unevenness
    [Tooltip("Weight for isolated holes (fewer holes is better -> negative weight)")]
    [SerializeField] private float weightHoles = -0.5f;        // Strongly penalize holes
    // Add more weights here if you add more heuristic factors

    [Header("Gameplay Settings")]
    [Tooltip("Delay in seconds before the player makes a move (for visual debugging)")]
    [SerializeField] private float moveDelay = 0.5f;

    // --- Internal State ---
    private bool isPlaying = false; // Flag to prevent multiple coroutines running
    [Tooltip("Flag for the Agent to know when the player's turn is complete.")]
    public bool IsTurnComplete { get; set; } = true; // Public flag for the Agent

    // Helper class to store placement info AND calculated score
    private class PlacementOption
    {
        public BlockShape Shape;         // The shape being considered
        public BlockController Controller; // The actual BlockController instance in the dock
        public Vector2Int Position;      // The grid position (bottom-left cell) for placement
        public int BlockIndex;           // The original index (0, 1, or 2) in the dock list (for reference)
        public float HeuristicScore;     // The calculated score based on multiple factors
    }

    // --- Lifecycle Methods ---
    // Awake or Start can be used if needed for initial setup, but references are serialized.

    /// <summary>
    /// Called by the Agent (or other game logic) to request the simulated player takes a turn.
    /// </summary>
    public void RequestPlayTurn()
    {
        // Check for manual play mode first
        if (gameManager != null && gameManager.isManualPlayMode)
        {
            // Debug.LogWarning("[SimulatedPlayer] In manual mode. SimulatedPlayer turn bypassed by RequestPlayTurn.");
            IsTurnComplete = true; // Signal completion immediately
            isPlaying = false;     // Ensure isPlaying is false if it was true
            return;
        }

        // Only start a turn if not already playing and there are blocks in the dock
        if (!isPlaying && gridManager != null && gridManager.currentBlocks.Count > 0)
        {
            StartCoroutine(PlayTurnCoroutine());
        }
        else if (gridManager != null && gridManager.currentBlocks.Count == 0)
        {
             // If RequestPlayTurn is called but the dock is already empty, just signal completion.
             // The Agent's logic should ideally prevent this call if dock is empty, but this is safe.
            Debug.LogWarning("[SimulatedPlayer] RequestPlayTurn called with empty dock. Signalling turn complete.");
            IsTurnComplete = true; // Ensure flag is true
        }
        else if (isPlaying) // Check if already playing specifically
        {
             Debug.LogWarning("[SimulatedPlayer] RequestPlayTurn called while already playing.");
            // IsTurnComplete remains false if already playing, which is correct.
        }
         else // gridManager is null
         {
             Debug.LogError("[SimulatedPlayer] RequestPlayTurn called but gridManager is null!");
             IsTurnComplete = true; // Cannot play, signal complete
         }
    }

    /// <summary>
    /// Coroutine that finds the best move using the heuristic and executes it.
    /// Runs as a single turn for the simulated player.
    /// </summary>
    private IEnumerator PlayTurnCoroutine()
    {
        isPlaying = true;           // Set playing flag
        IsTurnComplete = false;     // Signal to Agent that turn has started

        // Optional delay for visual debugging
        yield return new WaitForSeconds(moveDelay);

        Debug.Log("[Sim Player Turn] Finding best placement...");
        // Find the best placement based on the NEW heuristic score for the current dock state
        PlacementOption bestOption = FindBestPlacement();

        // Execute the best found move if one exists and the controller is valid
        if (bestOption != null && bestOption.Controller != null)
        {
            Debug.Log($"[Sim Player Turn] Best move found (Score: {bestOption.HeuristicScore:F3}). Attempting to place Block Index {bestOption.BlockIndex} ({bestOption.Shape.shapeName}) at {bestOption.Position}.");

            // Attempt to place the block using the GridManager
            bool placed = gridManager.TryPlaceBlock(bestOption.Shape, bestOption.Position);

            if (placed)
            {
                // Debug.Log($"[Sim Player Turn] Placement successful. Removing block...");
                // Remove the *specific* BlockController instance that was placed from GridManager's list
                gridManager.RemoveBlock(bestOption.Controller);
                // Destroy the visual GameObject associated with the placed block
                if(bestOption.Controller != null && bestOption.Controller.gameObject != null) Destroy(bestOption.Controller.gameObject);
                // Debug.Log($"[Sim Player Turn] Block removed. Remaining dock count: {gridManager.currentBlocks.Count}");
                // GridManager.TryPlaceBlock handles internal grid state update, scoring, and line clearing.
            }
            else {
                 // This should ideally not happen if CanPlaceBlock returned true when FindBestPlacement was called.
                 Debug.LogError($"[Sim Player Turn] ERROR: TryPlaceBlock failed unexpectedly for a supposedly valid move! Shape: {bestOption.Shape.shapeName}, Pos: {bestOption.Position}.");
            }
        }
        else
        {
             // If FindBestPlacement returned null or a null Controller.
             // The Agent's CheckIfAnyPlacementPossible should ideally catch this before calling RequestPlayTurn.
             bool canPlaceAny = CheckIfAnyPlacementPossible(); // Re-check for logging/debugging
             if(bestOption == null) {
                 Debug.LogError($"[Sim Player Turn] ERROR: FindBestPlacement returned NULL, but CheckIfAnyPlacementPossible returned {canPlaceAny}. Could not find any valid move or scoring failed. This likely means the game is over or bugged.");
             } else { // bestOption is not null, but bestOption.Controller is null
                 Debug.LogError($"[Sim Player Turn] ERROR: FindBestPlacement returned option but Controller was NULL. Investigate PlacementOption creation.");
             }
             // No move was made this turn. Game over condition should be handled by the Agent's loop check after this turn finishes.
        }

        isPlaying = false;         // Reset playing flag
        IsTurnComplete = true;     // Signal to Agent that turn is complete
        // Debug.Log("[Sim Player Turn] Turn Finished.");
        yield return null; // Ensure coroutine always yields
    }


    /// <summary>
    /// Finds the best possible move for the blocks currently in the dock based on a heuristic score.
    /// </summary>
    /// <returns>The PlacementOption with the highest score, or null if no valid moves exist.</returns>
    private PlacementOption FindBestPlacement()
    {
        List<PlacementOption> scoredPlacements = new List<PlacementOption>();

        // Ensure GridManager and blocks exist
        if (gridManager == null || gridManager.currentBlocks == null || gridManager.currentBlocks.Count == 0) {
             Debug.LogWarning("[FindBestPlacement] No blocks in dock to evaluate.");
             return null;
        }

        // Get the grid state *before* simulating any moves for comparison in heuristic
        // Assuming gridManager.grid is accessible (ideally via a public accessor like GetGridState())
        int[,] originalGrid = gridManager.GetGridState(); // Use a public accessor on GridManager!

        // Debug.Log($"[FindBestPlacement] Evaluating {gridManager.currentBlocks.Count} blocks currently in dock.");

        // Iterate through all blocks currently in the dock
        // Use index to ensure we access the correct BlockController from the list
        for (int blockIndex = 0; blockIndex < gridManager.currentBlocks.Count; blockIndex++)
        {
            // Bounds and null check for the current block in the list
            if (blockIndex >= gridManager.currentBlocks.Count) continue; // Safety check
            BlockController controller = gridManager.currentBlocks[blockIndex];
            if (controller == null || controller.shape == null) {
                 Debug.LogWarning($"[FindBestPlacement] Skipping null controller or shape at index {blockIndex} in the dock.");
                 continue;
            }

            BlockShape shape = controller.shape;
            // Debug.Log($"[FindBestPlacement] Evaluating Block Index {blockIndex}: {shape.shapeName}"); // Verbose logging

            // Iterate through all possible placement positions on the grid for this block
            for (int y = 0; y < gridManager.gridHeight; y++)
            {
                for (int x = 0; x < gridManager.gridWidth; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);

                    // Check if placement is valid on the *original* grid state
                    if (gridManager.CanPlaceBlock(shape, position))
                    {
                        // --- Simulate the placement and calculate heuristics ---
                        // Create a fresh copy of the original grid state for each potential move simulation
                        int[,] tempGridAfterPlacement = gridManager.GetGridState(); // Start simulation from original state

                        // Simulate ONLY placing the block on this temporary grid copy
                        bool simulationSuccessfulPlacement = SimulatePlacementOnly(tempGridAfterPlacement, shape, position);

                        if (simulationSuccessfulPlacement) // Should always be true if CanPlaceBlock was true
                        {
                             // Count how many lines are full on the grid *after* placement but *before* clearing
                             int linesClearedCount = CountFullLines(tempGridAfterPlacement);

                             // Create a SECOND temporary grid copy *after* placement but *before* clearing,
                             // specifically for calculating metrics on the post-clear state.
                             int[,] tempGridAfterClear = (int[,])tempGridAfterPlacement.Clone();

                             // Simulate clearing lines on the temp grid copy intended for post-clear metrics
                             ClearFullLinesInTempGrid(tempGridAfterClear); // Modifies tempGridAfterClear IN PLACE

                             // Calculate the heuristic score based on the number of lines cleared by *this move*
                             // and the board state *after* those lines have been cleared.
                             float score = CalculateHeuristicScore(linesClearedCount, tempGridAfterClear);

                             // Add this valid placement option and its score to the list
                             scoredPlacements.Add(new PlacementOption {
                                Shape = shape,
                                Controller = controller, // *** Store the reference to the ACTUAL BlockController instance from the dock ***
                                Position = position,
                                BlockIndex = blockIndex, // Store original index for debugging/reference
                                HeuristicScore = score
                             });
                        }
                        else {
                             // Log if simulation fails, indicates a potential mismatch between CanPlaceBlock and SimulatePlacementOnly
                             Debug.LogError($"[FindBestPlacement] SimulatePlacementOnly failed for a valid placement! Pos {position}, Shape {shape.shapeName}");
                        }
                    }
                }
            }
        }
         Debug.Log($"[FindBestPlacement] Evaluated {scoredPlacements.Count} valid placement options across all blocks in dock.");

        // If no valid placements were found for any block, return null
        if (scoredPlacements.Count == 0) {
             Debug.LogWarning("[FindBestPlacement] No valid placements found for any block in the dock.");
             return null;
        }

        // Sort the valid placements by their heuristic score (highest score first)
        // and return the option with the highest score.
        PlacementOption bestOption = scoredPlacements
            .OrderByDescending(p => p.HeuristicScore)
            .FirstOrDefault(); // Get the first item after sorting (which is the highest)

         if (bestOption != null) {
             // Debug.Log($"[FindBestPlacement] Selected best option: Score {bestOption.HeuristicScore:F3}, Block Index {bestOption.BlockIndex} ({bestOption.Shape.shapeName}) at {bestOption.Position}");
         } else {
             // This should logically not happen if scoredPlacements.Count > 0
             Debug.LogError("[FindBestPlacement] ERROR: Sorting resulted in NULL despite having valid placements!");
         }

        return bestOption;
    }

    /// <summary>
    /// Helper: Simulates ONLY placing a block on a temporary grid (marks cells as filled).
    /// Does NOT check CanPlaceBlock (assumes it was already checked).
    /// Does NOT clear lines. Modifies the grid IN PLACE.
    /// </summary>
    /// <param name="tempGrid">The grid state to simulate on (will be modified).</param>
    /// <param name="shape">The shape to place.</param>
    /// <param name="position">The grid position (bottom-left cell) to place at.</param>
    /// <returns>True if placement simulation was successful (within bounds, not previously occupied).</returns>
    private bool SimulatePlacementOnly(int[,] tempGrid, BlockShape shape, Vector2Int position)
    {
        int width = tempGrid.GetLength(0);
        int height = tempGrid.GetLength(1);

        foreach (Vector2Int cell in shape.cells)
        {
            Vector2Int gridPos = position + cell;
            // Safety checks - these should pass if CanPlaceBlock was true, but defensive coding.
            if (gridPos.x < 0 || gridPos.x >= width || gridPos.y < 0 || gridPos.y >= height || tempGrid[gridPos.x, gridPos.y] == 1)
            {
                Debug.LogError($"SimulatePlacementOnly Error: Invalid cell {gridPos} for shape {shape.shapeName} at pos {position}. Grid size {width}x{height}.");
                return false; // Simulation failed
            }
            tempGrid[gridPos.x, gridPos.y] = 1; // Mark as filled
        }
        return true; // Simulation successful
    }

     /// <summary>
    /// Helper: Counts how many full rows and columns exist in a grid state.
    /// Does NOT modify the grid.
    /// </summary>
    /// <param name="grid">The grid state to check.</param>
    /// <returns>The total count of full rows and columns.</returns>
    private int CountFullLines(int[,] grid)
    {
         int fullLines = 0;
         int width = grid.GetLength(0);
         int height = grid.GetLength(1);

         // Check Rows
         for (int y = 0; y < height; y++) {
             bool rowFull = true;
             for (int x = 0; x < width; x++) {
                 if (grid[x, y] == 0) { rowFull = false; break; }
             }
             if (rowFull) { fullLines++; }
         }

         // Check Columns
         for (int x = 0; x < width; x++) {
              bool colFull = true;
              for (int y = 0; y < height; y++) {
                  if (grid[x, y] == 0) { colFull = false; break; }
              }
              if (colFull) { fullLines++; }
         }
         return fullLines;
    }

     /// <summary>
    /// Helper: Clears all full rows and columns IN PLACE on a temporary grid.
    /// </summary>
    /// <param name="grid">The grid state to clear (will be modified).</param>
    private void ClearFullLinesInTempGrid(int[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

         List<int> rowsToClear = new List<int>();
         List<int> columnsToClear = new List<int>();

         // Identify Rows to Clear
         for (int y = 0; y < height; y++) {
             bool rowFull = true;
             for (int x = 0; x < width; x++) {
                 if (grid[x, y] == 0) { rowFull = false; break; }
             }
             if (rowFull) { rowsToClear.Add(y); }
         }

         // Identify Columns to Clear
         for (int x = 0; x < width; x++) {
              bool colFull = true;
              for (int y = 0; y < height; y++) {
                  if (grid[x, y] == 0) { colFull = false; break; }
              }
              if (colFull) { columnsToClear.Add(x); }
         }

         // Perform Clearing IN PLACE
         foreach (int row in rowsToClear) {
             ClearRowInTempGrid(grid, row); // Uses the helper below
         }
          foreach (int column in columnsToClear) {
              ClearColumnInTempGrid(grid, column); // Uses the helper below
          }
    }

     /// <summary>
     /// Helper: Clears a single row IN PLACE on a temporary grid.
     /// </summary>
     private void ClearRowInTempGrid(int[,] grid, int rowY) {
         int width = grid.GetLength(0);
         for (int x = 0; x < width; x++) {
             grid[x, rowY] = 0; // Set row to empty
         }
     }

     /// <summary>
     /// Helper: Clears a single column IN PLACE on a temporary grid.
     /// </summary>
     private void ClearColumnInTempGrid(int[,] grid, int colX) {
         int height = grid.GetLength(1);
         for (int y = 0; y < height; y++) {
             grid[colX, y] = 0; // Set column to empty
         }
     }


    /// <summary>
    /// Calculates the total heuristic score for a given grid state based on defined weights.
    /// This represents how "good" this state is for future play according to the bot's logic.
    /// </summary>
    /// <param name="linesClearedCount">The number of lines cleared by the move that led to this state.</param>
    /// <param name="postClearGrid">The grid state *after* simulated placement and clearing.</param>
    /// <returns>The calculated heuristic score.</returns>
    private float CalculateHeuristicScore(int linesClearedCount, int[,] postClearGrid)
    {
         // Ensure grid is not null before calculating metrics
         if (postClearGrid == null) return float.NegativeInfinity; // Bad state
         if (gridManager == null) // Add safety check for gridManager
         {
             Debug.LogError("[CalculateHeuristicScore] GridManager reference is null!");
             return float.NegativeInfinity;
         }

          // 1. Lines Cleared Score (based on count passed in)
          // Reward scaled quadratically to heavily favor multi-line clears
          float linesScore = (linesClearedCount * linesClearedCount) * weightLinesCleared;

          // 2. Aggregate Height (calculated on the grid *after* simulated clearing)
          // Penalize tall boards
          int aggregateHeight = gridManager.CalculateAggregateHeight(postClearGrid);
          float heightScore = aggregateHeight * weightAggregateHeight;

          // 3. Bumpiness (calculated on the grid *after* simulated clearing)
          // Penalize uneven surfaces
          int bumpiness = gridManager.CalculateBumpiness(postClearGrid);
          float bumpinessScore = bumpiness * weightBumpiness;

          // 4. Holes (calculated on the grid *after* simulated clearing)
          // Strongly penalize isolated holes which are hard to fill
          int holes = gridManager.CalculateHoles(postClearGrid);
          float holesScore = holes * weightHoles;

          // Combine all score components
          float totalScore = linesScore + heightScore + bumpinessScore + holesScore;

           // Debug.Log($"Score Breakdown: Lines={linesScore:F2} (cleared={linesClearedCount}), Height={heightScore:F2} (agg={aggregateHeight}), Bumpiness={bumpinessScore:F2} (val={bumpiness}), Holes={holesScore:F2} (num={holes}) -> Total={totalScore:F2}");

          return totalScore;
    }


    /// <summary>
    /// Checks if at least one block currently in the dock can be placed anywhere on the grid.
    /// Used by the Agent to determine if the game is over for the current set.
    /// </summary>
    /// <returns>True if at least one placeable move exists for any dock block, false otherwise.</returns>
    public bool CheckIfAnyPlacementPossible()
    {
         // Ensure gridManager is not null before accessing currentBlocks
         if (gridManager == null || gridManager.currentBlocks == null) return false;

         // Iterate through blocks CURRENTLY in the dock
         foreach (BlockController controller in gridManager.currentBlocks)
         {
             // Ensure the controller and its shape are valid
             if (controller == null || controller.shape == null) continue;
             // Use the GridManager helper which checks all positions on the grid
             if (gridManager.IsShapePlaceableAnywhere(controller.shape))
             {
                 return true; // Found at least one placeable block for this shape
             }
         }
         return false; // Looped through all dock blocks, and none were placeable anywhere
    }
}