using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// An intelligent agent that provides blocks for a puzzle game.
/// Its goal is to manage player engagement and game tempo by selecting blocks
/// based on a deep analysis of the game state. This is achieved through a
/// "Board-Block Fitness Analysis" model.
/// </summary>
public class BlockProviderAgent : Agent
{
    #region References
    [Header("Game Core References")]
    [Tooltip("Reference to the GridManager to control the board and blocks.")]
    [SerializeField] private GridManager gridManager;
    [Tooltip("Reference to the GameManager for overall game state.")]
    [SerializeField] private GameManager gameManager;
    [Tooltip("Reference to the player agent to monitor its actions.")]
    [SerializeField] private BlockBlastAgent playerAgent; // The agent that places the blocks
    #endregion

    #region Agent Tuning
    [Header("Agent Behavior Tuning")]
    [Tooltip("The number of top-ranking blocks the agent will consider for its action.")]
    [SerializeField] private int topCandidatesToConsider = 7;
    [Tooltip("The number of blocks the agent provides each turn.")]
    private const int BLOCKS_TO_PROVIDE = 3;
    #endregion

    #region Reward Settings
    [Header("Reward Function Settings")]
    [SerializeField] private float multiLineClearBonus = 0.5f;
    [SerializeField] private float goodFitReward = 0.2f; // For reducing holes/bumpiness
    [SerializeField] private float comebackReward = 1.0f; // For saving a player from a high struggle state
    [SerializeField] private float gameOverPenalty = -2.0f;
    [SerializeField] private float stepPenalty = -0.01f;
    [SerializeField] private float rankBasedReward = 0.1f;
    #endregion

    #region Struggle Score Weights
    [Header("Struggle Score Weights")]
    [Range(0, 1)] public float weightDensity = 0.3f;
    [Range(0, 1)] public float weightHoles = 0.4f;
    [Range(0, 1)] public float weightBumpiness = 0.2f;
    
    [Range(0, 1)] public float weightPlacementAvailability = 0.5f;
    #endregion

    // Internal state
   #region Internal State
    // --- Stable Vocabulary of all shapes ---
    private List<BlockShape> shapeVocabulary;
    private Dictionary<string, int> shapeNameToId;
    private Dictionary<int, BlockShape> shapeIdToShape;

    // --- State for the Hybrid Model ---
    // This list will hold the Top 7 candidates for the current step and will be cached.
    private List<BlockAnalysisResult> topRankedCandidates;
    private float lastStruggleScore = 0f; // Still used for outcome-based rewards

    // The full struct with all the metrics you wanted.
    private struct BlockAnalysisResult
    {
        public BlockShape Shape;

        public int ShapeId; // The permanent ID from our vocabulary
        public float OverallFitnessScore;
        public int PlacementOpportunities;
        public int MaxLinesCleared;
        public float HoleBalance;
        public float BumpinessReduction;
    }
    #endregion

    public override void Initialize()
    {
        if (!gridManager) Debug.LogError("GridManager is not assigned!", this);
        if (!gameManager) Debug.LogError("GameManager is not assigned!", this);
        if (!playerAgent) Debug.LogError("PlayerAgent is not assigned!", this);

        // --- Create the fixed vocabulary and all necessary dictionaries ---
        shapeVocabulary = BlockShape.GetStandardShapes().OrderBy(s => s.shapeName).ToList();
        shapeNameToId = new Dictionary<string, int>();
        shapeIdToShape = new Dictionary<int, BlockShape>();
        for (int i = 0; i < shapeVocabulary.Count; i++)
        {
            shapeNameToId[shapeVocabulary[i].shapeName] = i;
            shapeIdToShape[i] = shapeVocabulary[i];
        }

        topRankedCandidates = new List<BlockAnalysisResult>();
        Debug.Log($"Provider Agent Initialized with a vocabulary of {shapeVocabulary.Count} shapes.");
    }

    /// <summary>
    /// Called at the beginning of each training episode.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Reset the entire game environment
        gameManager.ResetScore();
        gridManager.ClearGrid();
        
        // The episode starts with the Provider making the first move on an empty board.
        RequestDecision();
    }

    /// <summary>
    /// The main decision-making trigger for this agent.
    /// This should be called by the PlayerAgent after it has finished its turn.
    /// </summary>
    public void RequestNewBlocks()
    {
        // First, let's reward the agent for the outcome of the blocks it just provided.
        EvaluateAndRewardOutcome();

        // Now, request a new decision for the next set of blocks.
        RequestDecision();
    }


    /// <summary>
    /// Collects all necessary observations for the agent's decision.
    /// </summary>
   // In BlockProviderAgent.cs, REPLACE this method
    // In BlockProviderAgent.cs

    public override void CollectObservations(VectorSensor sensor)
    {
        // --- 1. Run Analysis and Cache Top Candidates ---
        // This is now a single, clean call to our helper function.
        RunAndCacheFitnessAnalysis();

        // --- 2. Observe Raw Grid State & High-Level Metrics ---
        for (int y = 0; y < gridManager.Height; y++)
        {
            for (int x = 0; x < gridManager.Width; x++)
            {
                sensor.AddObservation(gridManager.IsCellOccupied(x, y) ? 1.0f : 0.0f);
            }
        }
        lastStruggleScore = CalculateStruggleScore();
        sensor.AddObservation(lastStruggleScore);
        sensor.AddObservation(gridManager.CalculateBoardDensity());

        // --- 3. Observe ONLY the Top 7 Candidates' Fitness DNA ---
        for (int i = 0; i < topCandidatesToConsider; i++)
        {
            if (i < topRankedCandidates.Count)
            {
                var candidate = topRankedCandidates[i];
                sensor.AddObservation(1.0f); // Signal that a candidate exists
                sensor.AddObservation((float)candidate.ShapeId / shapeVocabulary.Count);
                sensor.AddObservation((float)candidate.PlacementOpportunities / (gridManager.Width * gridManager.Height));
                sensor.AddObservation((float)candidate.MaxLinesCleared / 4f);
                sensor.AddObservation(Mathf.Clamp01(candidate.HoleBalance / 5f));
                sensor.AddObservation(Mathf.Clamp01(candidate.BumpinessReduction / 10f));
            }
            else
            {
                sensor.AddObservation(0.0f); // Signal no candidate
                sensor.AddObservation(new float[5]); // Padding
            }
        }
    }

   // In BlockProviderAgent.cs, REPLACE this method
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // --- 1. Run the Full Fitness Analysis to Get a Ranked List ---
        List<BlockAnalysisResult> allAnalyzedResults = new List<BlockAnalysisResult>();
        int[,] gridState = gridManager.GetGridState();
        for (int i = 0; i < shapeVocabulary.Count; i++)
        {
            // We only need to analyze placeable blocks for ranking.
            if (gridManager.IsShapePlaceableAnywhere(shapeVocabulary[i]))
            {
                allAnalyzedResults.Add(AnalyzeSingleBlock(shapeVocabulary[i], gridState));
            }
        }
        
        // Sort the placeable blocks by their fitness score to find the best ones.
        var rankedPlaceableCandidates = allAnalyzedResults.OrderByDescending(r => r.OverallFitnessScore).ToList();

        // --- 2. Identify the IDs of the Top 7 Candidates ---
        HashSet<int> topCandidateIds = new HashSet<int>();
        for (int i = 0; i < Mathf.Min(topCandidatesToConsider, rankedPlaceableCandidates.Count); i++)
        {
            // Find the original ID of this top-ranked shape.
            BlockShape shape = rankedPlaceableCandidates[i].Shape;
            int shapeId = shapeVocabulary.IndexOf(shape);
            if (shapeId != -1)
            {
                topCandidateIds.Add(shapeId);
            }
        }

        // --- 3. Mask the Actions ---
        // If there are no placeable blocks at all, the game is over.
        if (topCandidateIds.Count == 0)
        {
            // Don't mask anything. The PlayerIsStuck method will be called to end the episode.
            return;
        }

        // For all three action branches...
        for (int branch = 0; branch < 3; branch++)
        {
            // ...disable every action by default...
            for (int i = 0; i < shapeVocabulary.Count; i++)
            {
                actionMask.SetActionEnabled(branch, i, false);
            }

            // ...then enable ONLY the actions corresponding to our Top 7 candidates.
            foreach (int id in topCandidateIds)
            {
                actionMask.SetActionEnabled(branch, id, true);
            }
        }
    }
    /// <summary>
    /// Executes when the agent decides on an action.
    /// </summary>
   // In BlockProviderAgent.cs, REPLACE this method
    public override void OnActionReceived(ActionBuffers actions)
    {
        var chosenActionIds = actions.DiscreteActions;
        
        float totalRankReward = 0f;

        // Loop through the 3 choices the agent made
        for (int i = 0; i < BLOCKS_TO_PROVIDE; i++)
        {
            int chosenShapeId = chosenActionIds[i];

            // Find the rank of the chosen block in our cached candidate list.
            int rank = topRankedCandidates.FindIndex(c => c.ShapeId == chosenShapeId);

            if (rank != -1) // This will be true because of the action mask
            {
                // Reward for picking higher-ranked blocks (rank 0 is best).
                totalRankReward += (topCandidatesToConsider - rank) * rankBasedReward;
            }

            // Spawn the chosen block
            gridManager.SpawnSpecificBlock(shapeIdToShape[chosenShapeId]);
        }
        
        AddReward(totalRankReward);
        AddReward(stepPenalty);
        
        // The method on the player agent is OnNewBlocksProvided, not StartTurnWithNewBlocks
        playerAgent.OnNewBlocksProvided();
    }

    /// <summary>
    /// Heuristic for manual testing. Provides a set of the top 3 blocks from the analysis.
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        // To decide the action, we must first run the analysis to find the best candidates.
        RunAndCacheFitnessAnalysis();

        // Now, we choose the top 3 candidates from the ranked list.
        for (int i = 0; i < BLOCKS_TO_PROVIDE; i++)
        {
            if (i < topRankedCandidates.Count)
            {
                // Set the action to the permanent ID of the best-ranked block.
                discreteActions[i] = topRankedCandidates[i].ShapeId;
            }
            else if (topRankedCandidates.Count > 0)
            {
                // If there are fewer than 3 candidates, just reuse the best one.
                discreteActions[i] = topRankedCandidates[0].ShapeId;
            }
            else
            {
                // If there are no candidates at all, default to action 0.
                discreteActions[i] = 0;
            }
        }
    }


    // Add this new public method inside your BlockProviderAgent.cs class

    /// <summary>
    /// Called by the Player Agent when it determines the game is truly over.
    /// This agent, as the "Game Master," will formally end the episode.
    /// </summary>
    public void PlayerIsStuckAndGameIsOver()
    {
        AddReward(gameOverPenalty);
        EndEpisode();
    }

    #region Fitness Analysis Core Logic

    /// <summary>
    /// This is now the single source of truth for analysis each turn.
    /// It runs the analysis on all placeable blocks and caches the Top 7 results.
    /// It is called by both CollectObservations and the Heuristic method.
    /// </summary>
    private void RunAndCacheFitnessAnalysis()
    {
        topRankedCandidates.Clear();
        var analysisResults = new List<BlockAnalysisResult>();
        int[,] gridState = gridManager.GetGridState();

        foreach (var shape in shapeVocabulary)
        {
            if (gridManager.IsShapePlaceableAnywhere(shape))
            {
                analysisResults.Add(AnalyzeSingleBlock(shape, gridState));
            }
        }

        // Sort all placeable blocks by their score and take the top N to cache for this turn.
        topRankedCandidates = analysisResults
            .OrderByDescending(r => r.OverallFitnessScore)
            .Take(topCandidatesToConsider)
            .ToList();
    }

    /// <summary>
    /// Performs the full suite of tests on a single block to determine its fitness.
    /// (This is your full-featured analysis method)
    /// </summary>
    private BlockAnalysisResult AnalyzeSingleBlock(BlockShape shape, int[,] gridState)
    {
        var result = new BlockAnalysisResult
        {
            ShapeId = shapeNameToId[shape.shapeName],
            Shape = shape
        };

        int initialHoles = gridManager.CalculateHoles(gridState);
        int initialBumpiness = gridManager.CalculateBumpiness(gridState);
        Vector2Int bestPlacement = Vector2Int.zero;
        int maxClears = 0;
        
        for (int y = 0; y < gridManager.Height; y++)
        {
            for (int x = 0; x < gridManager.Width; x++)
            {
                var pos = new Vector2Int(x, y);
                if (gridManager.CanPlaceBlock(shape, pos))
                {
                    result.PlacementOpportunities++;
                    int clears = gridManager.SimulatePlacementAndCheckClears(shape, pos);
                    if (clears > maxClears)
                    {
                        maxClears = clears;
                        bestPlacement = pos;
                    }
                }
            }
        }
        result.MaxLinesCleared = maxClears;
        
        int[,] simulatedGrid = GetSimulatedGridAfterPlacement(shape, bestPlacement, gridState);
        result.HoleBalance = initialHoles - gridManager.CalculateHoles(simulatedGrid);
        result.BumpinessReduction = initialBumpiness - gridManager.CalculateBumpiness(simulatedGrid);
        
        // The fitness score used for ranking the candidates.
        result.OverallFitnessScore = (result.MaxLinesCleared * 2.0f) +
                                    (result.HoleBalance * 1.5f) +
                                    (result.BumpinessReduction * 1.0f) +
                                    (result.PlacementOpportunities * 0.1f);
        return result;
    }

    private int[,] GetSimulatedGridAfterPlacement(BlockShape shape, Vector2Int position, int[,] initialGrid)
    {
        int[,] simulatedGrid = (int[,])initialGrid.Clone();
        foreach (var cell in shape.cells)
        {
            var p = position + cell;
            if (p.x >= 0 && p.x < gridManager.Width && p.y >= 0 && p.y < gridManager.Height)
            {
                simulatedGrid[p.x, p.y] = 1;
            }
        }
        return simulatedGrid;
    }

    #endregion
    /// <summary>
    /// Helper to get a new grid state after simulating a block placement.
    /// </summary>
    
    

    #region Reward & State Evaluation

    /// <summary>
    /// Calculates rewards based on the outcome of the player's last turn.
    /// </summary>
    private void EvaluateAndRewardOutcome()
    {
        // Multi-line clear bonus
        int linesCleared = gridManager.GetLastLinesCleared();
        if (linesCleared >= 1)
        {
            AddReward(linesCleared * linesCleared * multiLineClearBonus);
        }

        // Good Fit & Comeback Rewards
        float currentStruggleScore = CalculateStruggleScore();
        if (currentStruggleScore < lastStruggleScore)
        {
            // The board state has improved. This is a "Good Fit".
            AddReward(goodFitReward);

            // If the improvement was drastic from a bad state, this is a "Comeback".
            if (lastStruggleScore > 0.7f)
            {
                AddReward(comebackReward);
            }
        }
    }

    // In BlockProviderAgent.cs, add this new method
   

    /// <summary>
    /// Calculates the player's current "Struggle Score" from 0.0 (no struggle) to 1.0 (max struggle).
    /// </summary>
    // In BlockProviderAgent.cs, REPLACE the old method with this new one
    private float CalculateStruggleScore()
    {
        // --- 1. Calculate Placement Availability (The New Proactive Metric) ---
        int placeableShapeCount = 0;
        foreach (var shape in shapeVocabulary)
        {
            if (gridManager.IsShapePlaceableAnywhere(shape))
            {
                placeableShapeCount++;
            }
        }

        // Normalize this score. A low number of placeable shapes means high struggle.
        float normPlacementAvailability = 1.0f - ((float)placeableShapeCount / shapeVocabulary.Count);


        // --- 2. Calculate Other Metrics (as before) ---
        float normDensity = gridManager.CalculateBoardDensity();
        // Normalize by a reasonable maximum expected value
        float normHoles = Mathf.Clamp01((float)gridManager.CalculateHoles(gridManager.GetGridState()) / 20f);
        float normBumpiness = Mathf.Clamp01((float)gridManager.CalculateBumpiness(gridManager.GetGridState()) / 30f);

        // --- 3. Combine all weighted metrics ---
        float score = (normDensity * weightDensity) +
                    (normHoles * weightHoles) +
                    (normBumpiness * weightBumpiness) +
                    (normPlacementAvailability * weightPlacementAvailability);

        // Re-normalize the final score to ensure it's a clean 0-1 value
        float totalWeight = weightDensity + weightHoles + weightBumpiness + weightPlacementAvailability;
        if (totalWeight <= 0) return 0; // Avoid division by zero

        return Mathf.Clamp01(score / totalWeight);
    }
    #endregion
}
