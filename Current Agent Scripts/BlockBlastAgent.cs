using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Agent class for the BlockBlast RL environment.
/// Handles observation, action masking, reward logic, and agent behavior.
/// </summary>
public class BlockBlastAgent : Agent
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GameManager gameManager;

    [Header("Agent Settings")]
    [SerializeField] private int maxStepsPerEpisode = 2000;
    [SerializeField] private float actionDelaySeconds = 0.1f;

    [Header("Reward Settings - User Defined Tiers")]
    [SerializeField] private float gameOverPenalty = -5f;
    [SerializeField] private float perNewHolePenalty = -1f;
    [SerializeField] private float isolationPenalty = -0.8f;
    [SerializeField] private float stepPenalty;
    [SerializeField] private float earlyGameBoundaryPlacementReward = 0.2f;
    [SerializeField] private float earlyGameDensityThreshold = 0.15f;

    [Header("Shaping Tuning")]
    [SerializeField] private float shapingCoefficient = 0.02f;
    [SerializeField] private float shapingClip = 0.5f;
    [SerializeField] private float scaleFactor = 0.02f;

    private int currentStep;
    private bool isActionInProgress;
    private int episodeLinesCleared;
    private int episodeNewHoles;
    private float compactnessReward;
    private Dictionary<int, List<Vector2Int>> validPlacementsByBlockIndex = new Dictionary<int, List<Vector2Int>>();

    private const int NumDockSlots = 3;
    private const int MaxCellsPerShapeObs = 9;

    // Properties for grid dimensions
    private int GridWidth => gridManager.gridWidth;
    private int GridHeight => gridManager.gridHeight;
    private int NumBlocks => 3;
    private int NumActions => NumBlocks * GridWidth * GridHeight;

    /// <summary>
    /// Decode a flat action integer into block index, x, and y grid positions.
    /// </summary>
    private void DecodeAction(int action, out int blockIdx, out int x, out int y)
    {
        int totalGridCells = GridWidth * GridHeight;
        blockIdx = action / totalGridCells;
        int remainder = action % totalGridCells;
        x = remainder % GridWidth;
        y = remainder / GridWidth;
    }

    public override void Initialize()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridManager not assigned to BlockBlastAgent!", this);
            enabled = false;
            return;
        }
        if (gameManager == null)
        {
            Debug.LogWarning("GameManager not assigned to BlockBlastAgent.", this);
        }
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log(currentStep);
        currentStep = 0;
        isActionInProgress = false;
        StopAllCoroutines();
        validPlacementsByBlockIndex.Clear();

        if (Academy.IsInitialized)
        {
            Academy.Instance.StatsRecorder.Add("Custom/LinesCleared", episodeLinesCleared, StatAggregationMethod.Average);
            Academy.Instance.StatsRecorder.Add("Custom/NewHolesCreated", episodeNewHoles, StatAggregationMethod.Average);
            Academy.Instance.StatsRecorder.Add("Custom/CompactnessReward", compactnessReward, StatAggregationMethod.Average);
        }

        episodeLinesCleared = 0;
        episodeNewHoles = 0;
        compactnessReward = 0f;

        gameManager?.ResetScore();
        gridManager.ClearGrid();
        gridManager.SpawnBlocks(NumDockSlots);
        PrecomputeValidPlacements();
    }

    private void Update()
    {
        if (!isActionInProgress && currentStep >= maxStepsPerEpisode)
        {
            if (gridManager.CheckGameOver())
            {
                AddReward(gameOverPenalty * scaleFactor);
                EndEpisode();
			}
        }
    }

    /// <summary>
    /// Precomputes all valid placements for each block.
    /// </summary>
    private void PrecomputeValidPlacements()
    {
        validPlacementsByBlockIndex.Clear();

        if (gridManager.currentBlocks == null)
            return;

        for (int i = 0; i < gridManager.currentBlocks.Count; i++)
        {
            BlockController block = gridManager.currentBlocks[i];
            var placements = new List<Vector2Int>();

            if (block?.shape != null)
            {
                for (int y = 0; y < gridManager.gridHeight; y++)
                {
                    for (int x = 0; x < gridManager.gridWidth; x++)
                    {
                        if (gridManager.CanPlaceBlock(block.shape, new Vector2Int(x, y)))
                            placements.Add(new Vector2Int(x, y));
                    }
                }
            }

            validPlacementsByBlockIndex[i] = placements;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Board state (1: occupied, 0: empty)
        for (int y = 0; y < gridManager.gridHeight; y++)
            for (int x = 0; x < gridManager.gridWidth; x++)
                sensor.AddObservation(gridManager.IsCellOccupied(x, y) ? 1f : 0f);

        // Current blocks (shapes)
        for (int k = 0; k < NumDockSlots; k++)
        {
            if (k < gridManager.currentBlocks.Count && gridManager.currentBlocks[k]?.shape != null)
            {
                BlockShape shape = gridManager.currentBlocks[k].shape;
                sensor.AddObservation(1f);
                int count = 0;
                foreach (Vector2Int cell in shape.cells)
                {
                    if (count >= MaxCellsPerShapeObs) break;
                    sensor.AddObservation(cell.x / 2f);
                    sensor.AddObservation(cell.y / 2f);
                    count++;
                }
                for (; count < MaxCellsPerShapeObs; count++)
                {
                    sensor.AddObservation(-1f);
                    sensor.AddObservation(-1f);
                }
            }
            else
            {
                sensor.AddObservation(0f);
                for (int i = 0; i < MaxCellsPerShapeObs; i++)
                {
                    sensor.AddObservation(-1f);
                    sensor.AddObservation(-1f);
                }
            }
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask mask)
    {
        PrecomputeValidPlacements();
        int gridCells = gridManager.gridWidth * gridManager.gridHeight;
        int totalActions = NumBlocks * gridCells;
        bool hasValidAction = false;

        for (int i = 0; i < totalActions; i++)
        {
            int blockIdx = i / gridCells;
            int remainder = i % gridCells;
            int x = remainder % gridManager.gridWidth;
            int y = remainder / gridManager.gridWidth;
            bool valid = false;

            if (blockIdx < gridManager.currentBlocks.Count && validPlacementsByBlockIndex.TryGetValue(blockIdx, out var placements))
            {
                foreach (var pos in placements)
                {
                    if (pos.x == x && pos.y == y)
                    {
                        valid = true;
                        break;
                    }
                }
            }

            mask.SetActionEnabled(0, i, valid);
            if (valid) hasValidAction = true;
        }

        if (!hasValidAction)
        {
            mask.SetActionEnabled(0, 0, true);
            Debug.LogWarning("Masking bug: no valid actions, unmasking 0");
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isActionInProgress) return;
        if (gridManager.isClearingLines)
        {
            //currentStep++;
            return;
        }

        int action = actions.DiscreteActions[0];
        DecodeAction(action, out int blockIdx, out int x, out int y);

        if (blockIdx < 0 || blockIdx >= NumBlocks)
        {
            Debug.LogError($"Invalid blockIdx {blockIdx}");
            EndEpisode();
            return;
        }
        if (blockIdx >= gridManager.currentBlocks.Count || gridManager.currentBlocks[blockIdx] == null)
        {
            Debug.LogError($"Empty slot chosen: {blockIdx}");
            EndEpisode();
            return;
        }

        BlockController block = gridManager.currentBlocks[blockIdx];
        BlockShape shape = block.shape;
        Vector2Int pos = new Vector2Int(x, y);

        if (!gridManager.CanPlaceBlock(shape, pos))
        {
            Debug.LogError($"CanPlaceBlock false for slot {blockIdx} at {pos}");
            EndEpisode();
            return;
        }

        StartCoroutine(ProcessActionWithDelay(block, pos));
    }

    private IEnumerator ProcessActionWithDelay(BlockController blockToPlace, Vector2Int position)
    {
        isActionInProgress = true;
        currentStep++;
        PrecomputeValidPlacements();
        int validMovesBefore = validPlacementsByBlockIndex.Sum(kvp => kvp.Value.Count);

        int[,] gridStateBefore = gridManager.GetGridState();
        int holesBefore = gridManager.CalculateHoles(gridStateBefore);
        CountTouchingCells(blockToPlace.shape, position, gridStateBefore);

        gridManager.TryPlaceBlock(blockToPlace.shape, position);
        int linesCleared = gridManager.GetLastLinesCleared();

        // Reward for lines cleared
        float reward = 0f;
        switch (linesCleared)
        {
            case 1: reward = 0.2f; break;
            case 2: reward = 0.5f; break;
            case 3: reward = 1.1f; break;
            case 4: reward = 2.3f; break;
            default: if (linesCleared >= 5) reward = 3f; break;
        }
        AddReward(reward * scaleFactor);

        int[,] gridStateAfter = gridManager.GetGridState();
        int holesAfter = gridManager.CalculateHoles(gridStateAfter);
        int newHoles = Mathf.Max(0, holesAfter - holesBefore);

        if (newHoles > 0)
            AddReward(newHoles * perNewHolePenalty * scaleFactor);

        if (gridManager.CalculateBoardDensity() < earlyGameDensityThreshold)
        {
            if (IsBlockOnBoundary(blockToPlace.shape, position))
                AddReward(earlyGameBoundaryPlacementReward * scaleFactor);
            else
                AddReward(isolationPenalty * scaleFactor);
        }
        else
        {
            int touchingCells = CountTouchingCells(blockToPlace.shape, position, gridStateBefore);
            if (touchingCells > 0)
            {
                var clusters = FindClusters(gridStateBefore);
                var clusterCenters = clusters.Select(CalculateClusterCOM).ToList();
                Vector2 blockCenter = CalculateBlockCentroid(blockToPlace.shape, position);

                float minDist = float.MaxValue;
                foreach (var center in clusterCenters)
                {
                    float dist = Vector2.Distance(center, blockCenter);
                    if (dist < minDist) minDist = dist;
                }
                float compactReward = 0.7f / (1f + minDist) + 0.005f * touchingCells;
                AddReward(compactReward * scaleFactor);
            }
            else
            {
                AddReward(isolationPenalty * scaleFactor);
            }
        }

        if (stepPenalty != 0f)
            AddReward(stepPenalty * scaleFactor);

        episodeLinesCleared += linesCleared;
        episodeNewHoles += newHoles;

        PrecomputeValidPlacements();
        int validMovesAfter = validPlacementsByBlockIndex.Sum(kvp => kvp.Value.Count);

        if (validMovesAfter == 0)
            AddReward(-1f * scaleFactor);
        else if (validMovesAfter <= 2)
            AddReward(-0.5f * scaleFactor);
        else if (validMovesAfter > validMovesBefore)
            AddReward(0.5f * scaleFactor);

        gridManager.RemoveBlock(blockToPlace);

        if (gridManager.currentBlocks.Count == 0)
        {
            if (actionDelaySeconds > 0.001f)
                yield return new WaitForSeconds(actionDelaySeconds);

            gridManager.SpawnBlocks(NumDockSlots);
        }
        else if (actionDelaySeconds > 0.001f)
        {
            yield return new WaitForSeconds(actionDelaySeconds);
        }

        PrecomputeValidPlacements();
        isActionInProgress = false;

        Academy.Instance.StatsRecorder.Add("Custom/GlobalStep", 1f, StatAggregationMethod.Average);
        CheckForGameOverAndEndEpisode();
    }

    private void CheckForGameOverAndEndEpisode()
    {
        if (gridManager.CheckGameOver())
        {
            bool hasValidMove = validPlacementsByBlockIndex.Values.Any(placements => placements != null && placements.Count > 0);
            if (!hasValidMove)
            {
                AddReward(gameOverPenalty * scaleFactor);
                EndEpisode();
            }
        }
    }

    private int CountTouchingCells(BlockShape shape, Vector2Int position, int[,] grid)
    {
        if (shape == null || shape.cells == null)
            return 0;

        int count = 0;
        foreach (Vector2Int cell in shape.cells)
        {
            Vector2Int cellPos = position + cell;
            foreach (Vector2Int offset in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int neighborPos = cellPos + offset;
                if (neighborPos.x >= 0 && neighborPos.x < gridManager.gridWidth &&
                    neighborPos.y >= 0 && neighborPos.y < gridManager.gridHeight &&
                    grid[neighborPos.x, neighborPos.y] == 1)
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private bool IsBlockOnBoundary(BlockShape shape, Vector2Int position)
    {
        if (shape == null || shape.cells == null)
            return false;

        foreach (Vector2Int cell in shape.cells)
        {
            Vector2Int pos = position + cell;
            if (pos.x == 0 || pos.x == gridManager.gridWidth - 1 || pos.y == 0 || pos.y == gridManager.gridHeight - 1)
                return true;
        }
        return false;
    }

    private Vector2 CalculateCenterOfMass(int[,] grid)
    {
        float sumX = 0f, sumY = 0f;
        int count = 0;
        for (int x = 0; x < gridManager.gridWidth; x++)
            for (int y = 0; y < gridManager.gridHeight; y++)
                if (grid[x, y] == 1)
                {
                    sumX += x;
                    sumY += y;
                    count++;
                }
        if (count == 0) return Vector2.zero;
        return new Vector2(sumX / count, sumY / count);
    }

    private Vector2 CalculateClusterCOM(List<Vector2Int> cluster)
    {
        float sumX = 0f, sumY = 0f;
        foreach (var v in cluster)
        {
            sumX += v.x;
            sumY += v.y;
        }
        if (cluster.Count == 0) return Vector2.zero;
        return new Vector2(sumX / cluster.Count, sumY / cluster.Count);
    }

    private Vector2 CalculateBlockCentroid(BlockShape shape, Vector2Int position)
    {
        float sumX = 0f, sumY = 0f;
        int count = 0;
        foreach (var cell in shape.cells)
        {
            sumX += position.x + cell.x;
            sumY += position.y + cell.y;
            count++;
        }
        if (count == 0) return Vector2.zero;
        return new Vector2(sumX / count, sumY / count);
    }

    private List<List<Vector2Int>> FindClusters(int[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        bool[,] visited = new bool[width, height];
        List<List<Vector2Int>> clusters = new List<List<Vector2Int>>();

        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == 1 && !visited[x, y])
                {
                    var cluster = new List<Vector2Int>();
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        var cur = queue.Dequeue();
                        cluster.Add(cur);

                        for (int d = 0; d < 4; d++)
                        {
                            int nx = cur.x + dx[d];
                            int ny = cur.y + dy[d];
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                                grid[nx, ny] == 1 && !visited[nx, ny])
                            {
                                visited[nx, ny] = true;
                                queue.Enqueue(new Vector2Int(nx, ny));
                            }
                        }
                    }
                    clusters.Add(cluster);
                }
            }
        }
        return clusters;
    }

    /// <summary>
    /// Human heuristic (choose first available action).
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;
        discreteActions[1] = 0;
        discreteActions[2] = 0;

        if (isActionInProgress || (gridManager != null && gridManager.isClearingLines))
            return;

        PrecomputeValidPlacements();
        float bestScore = float.MinValue;
        int bestBlock = -1;
        Vector2Int bestPos = Vector2Int.zero;

        for (int blockIdx = 0; blockIdx < NumBlocks; blockIdx++)
        {
            if (validPlacementsByBlockIndex.TryGetValue(blockIdx, out var placements) &&
                placements.Count > 0 && blockIdx < gridManager.currentBlocks.Count &&
                gridManager.currentBlocks[blockIdx] != null)
            {
                BlockShape shape = gridManager.currentBlocks[blockIdx].shape;
                int[,] gridState = gridManager.GetGridState();
                int holesBefore = gridManager.CalculateHoles(gridState);

                foreach (var pos in placements)
                {
                    float score = 0f;
                    int[,] testGrid = (int[,])gridState.Clone();
                    bool valid = true;
                    foreach (var offset in shape.cells)
                    {
                        Vector2Int cell = pos + offset;
                        if (cell.x < 0 || cell.x >= gridManager.gridWidth ||
                            cell.y < 0 || cell.y >= gridManager.gridHeight ||
                            testGrid[cell.x, cell.y] == 1)
                        {
                            valid = false;
                            break;
                        }
                        testGrid[cell.x, cell.y] = 1;
                    }
                    if (!valid) continue;

                    int linesCleared = gridManager.SimulatePlacementAndCheckClears(shape, pos);
                    if (linesCleared == 1) score += 0.2f;
                    else if (linesCleared == 2) score += 0.5f;
                    else if (linesCleared == 3) score += 1f;
                    else if (linesCleared == 4) score += 1.5f;
                    else if (linesCleared >= 5) score += 2f;

                    int holesAfter = gridManager.CalculateHoles(testGrid);
                    score += Mathf.Max(0, holesAfter - holesBefore) * perNewHolePenalty;

                    int touchingCells = CountTouchingCells(shape, pos, gridState);
                    if (touchingCells == 1) score += 0.05f;
                    else if (touchingCells == 2) score += 0.15f;
                    else if (touchingCells == 3) score += 0.5f;
                    else if (touchingCells >= 4) score += 1f;

                    if (gridManager.CalculateBoardDensity() > earlyGameDensityThreshold && linesCleared == 0)
                        score += isolationPenalty;
                    else if (gridManager.CalculateBoardDensity() <= earlyGameDensityThreshold && linesCleared == 0)
                        score += IsBlockOnBoundary(shape, pos) ? earlyGameBoundaryPlacementReward : isolationPenalty;

                    score += stepPenalty;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestBlock = blockIdx;
                        bestPos = pos;
                    }
                }
            }
        }

        if (bestBlock != -1)
        {
            discreteActions[0] = bestBlock;
            discreteActions[1] = bestPos.x;
            discreteActions[2] = bestPos.y;
        }
    }
}
