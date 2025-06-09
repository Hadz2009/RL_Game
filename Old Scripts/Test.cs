using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;

public class DebugAgent : Agent
{
    public override void OnEpisodeBegin()
    {
        // Nothing
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Just give one dummy value
        sensor.AddObservation(0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        if (action == 0 || action == 4) // First or last action
        {
            AddReward(+1.0f); // Good
            Debug.Log("Good action: " + action);
        }
        else // Actions 1, 2, 3
        {
            AddReward(-1.0f); // Bad
            Debug.Log("Bad action: " + action);
        }

        EndEpisode();
    }

    // Define the action space with 5 possible actions
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // Make all 5 actions available
        actionMask.SetActionEnabled(0, 0, true);
        actionMask.SetActionEnabled(0, 1, true);
        actionMask.SetActionEnabled(0, 2, true);
        actionMask.SetActionEnabled(0, 3, true);
        actionMask.SetActionEnabled(0, 4, true);
    }
}
