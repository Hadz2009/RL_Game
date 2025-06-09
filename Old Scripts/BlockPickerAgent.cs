// BlockSpawnerAgent.cs
// Attach this to an empty GameObject (e.g., "AIBlockSpawner") that also has
// a Behavior Parameters component. Hook the required references in the Inspector.
//
// IMPORTANT ‑‑ You need ML‑Agents 2.0+ in the project. The Behavior Parameters
// MUST be configured like this:
//   • Behavior Name:  "BlockSpawner"  (or change below)
//   • Space Type → Vector Observation, Space Size = gridWidth * gridHeight + 1
//   • Actions → Multi Discrete, Branch Count = 3, Branch Sizes = [BlockTypeCount, BlockTypeCount, BlockTypeCount]
//     (each branch chooses one block‑shape index for the dock slot)
//
// Simplest reward scheme:
//   +1  for every line the human clears after the AI’s last spawn
//   ‑1  if the player cannot place any of the three blocks
//   Small negative step penalty (‑0.01) so it learns to finish faster
//
// GridManager must expose:
//   int   Width { get; }
//   int   Height { get; }
//   bool  IsFilled(int x, int y)   // returns true if cell occupied
//   int   LinesClearedSinceLastSpawn { get; }  // lastLinesCleared you already track
//   bool  HasLegalMoves()                     // false if player is stuck
//   void  AISpawnBlocks(int id0, int id1, int id2);  // spawn specific shapes by index
//
// You already have most of this data; you might need to add two tiny helper getters.
//
/*
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class BlockSpawnerAgent : Agent
{
    [Header("Game References")]
    public GridManager gridManager;   // drag the GridManager from the scene

    [Tooltip("How many distinct BlockShape assets you have in the availableBlocks list")] 
    public int blockTypeCount = 18;   // set to your actual number

    [Header("Training Tweaks")]
    public float stuckPenalty = -1f;
    public float stepPenalty = -0.01f;

    // ---- EPISODE LIFECYCLE ----

    public override void OnEpisodeBegin()
    {
        // Reset the whole game: wipe grid, score, dock, etc.
        // Call whatever reset method you already use in GridManager / GameManager.
        gridManager.gameObject.SendMessage("ResetGame", SendMessageOptions.DontRequireReceiver);
    }

    // ---- OBSERVATIONS ----

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Grid occupancy (0 = empty, 1 = filled)
        for (int y = 0; y < gridManager.Height; y++)
        {
            for (int x = 0; x < gridManager.Width; x++)
            {
                sensor.AddObservation(gridManager.IsFilled(x, y) ? 1f : 0f);
            }
        }

        // 2. Current fill ratio (0‑1)
        sensor.AddObservation(gridManager.BoardDensity);
    }

    // ---- ACTIONS ----

    public override void OnActionReceived(ActionBuffers actions)
    {
        var da = actions.DiscreteActions;
        int id0 = Mathf.Clamp(da[0], 0, blockTypeCount - 1);
        int id1 = Mathf.Clamp(da[1], 0, blockTypeCount - 1);
        int id2 = Mathf.Clamp(da[2], 0, blockTypeCount - 1);

        // Tell the GridManager to spawn those specific shapes
        gridManager.AISpawnBlocks(id0, id1, id2);

        // Give step penalty so it doesn’t stall
        AddReward(stepPenalty);

        // Reward or punish based on outcome after player interaction
        int lines = gridManager.LinesClearedSinceLastSpawn;
        if (lines > 0)
            AddReward(lines); // +1 per line

        if (!gridManager.HasLegalMoves())
        {
            AddReward(stuckPenalty);
            EndEpisode();
        }
    }

    // ---- MANUAL TESTING (optional) ----

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var da = actionsOut.DiscreteActions;
        da[0] = Random.Range(0, blockTypeCount);
        da[1] = Random.Range(0, blockTypeCount);
        da[2] = Random.Range(0, blockTypeCount);
    }
}
*/ 