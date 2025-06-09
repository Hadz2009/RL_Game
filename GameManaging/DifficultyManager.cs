using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for Average()

// Structure to hold the metrics provided by GridManager
public struct GridMetrics
{
    public float BoardDensity;          // 0.0 to 1.0
    public int AvailablePlacements;     // Total placements for upcoming potential shapes
    public int MaxPossiblePlacements;   // Max possible placements if grid was empty (for normalization)
    public float TimeSinceClear;        // Seconds or moves
    public int IsolatedHoles;
    public float BoardBumpiness;
    public bool IsAnyFuturePieceUnplaceable; // Flag if any hypothetical next piece has 0 placements
}

public class DifficultyManager : MonoBehaviour
{
    // --- DDA Tuning Parameters (Inspectable) ---
    [Header("Tier 1: Subtle Adjustment")]
    [Range(0, 1)] public float struggleThresholdLow = 0.3f;    // Below this, no adjustment/bias towards harder
    [Range(0, 1)] public float struggleThresholdHigh = 0.7f;   // Above this, max adjustment/bias towards easier
    [Range(0, 0.5f)] public float maxAdjustmentFactor = 0.2f;  // Max probability shift towards Easy pieces - NOW PUBLIC
    [SerializeField, Range(1, 10)] private int smoothingWindow = 5; // How many historical scores to average

    [Header("Metric Weights (Relative Importance)")]
    [Range(0, 1)] public float weightDensity = 0.2f;
    [Range(0, 1)] public float weightAvailablePlacements = 0.3f; // Higher weight for low placements
    [Range(0, 1)] public float weightTimeSinceClear = 0.15f;
    [Range(0, 1)] public float weightIsolatedHoles = 0.15f;
    [Range(0, 1)] public float weightBoardBumpiness = 0.1f;
    [Range(0, 1)] public float weightFutureUnplaceable = 0.1f; // Penalty if next set looks bad

    [Header("Game Timer")]
    [SerializeField] private float gameEndTime = 60f; // Target game duration in seconds (1 minute for testing)
    [SerializeField, ReadOnly] public float gameTimer = 0f; // Current elapsed game time

    [Header("State (Read Only)")]
    [SerializeField, Range(0, 1)] private float currentRawStruggleScore;
    [SerializeField, Range(0, 1)] private float currentSmoothedStruggleScore;

    // --- Internal State ---
    private int consecutiveFailures = 0; // Failures = placements without clear
    private int consecutiveSuccesses = 0; // Successes = placements with clear
    private List<float> historicalScores = new List<float>();
    private int recentGameOverCount = 0;
    // Consider adding a timer or session counter to reset recentGameOverCount periodically

    // --- Game Reference ---
    [SerializeField] private GameManager gameManager; // Assign in Inspector

    // --- Public Accessors ---
    public float SmoothedStruggleScore => currentSmoothedStruggleScore;
    public int GetConsecutiveSuccesses() => consecutiveSuccesses; // Keep for potential scoring use

    // --- Lifecycle Methods ---
    private void Awake()
    {
        currentSmoothedStruggleScore = 0f; // Start with no struggle
        gameTimer = 0f; // Reset timer on awake
        //Debug.Log("New DifficultyManager Initialized.");
    }

    private void Update()
    {
        // Increment game timer only if the game is actively playing
        if (gameManager != null && gameManager.IsGameActive())
        {
             gameTimer += Time.deltaTime;
        }
    }

    // --- DDA Core Logic ---

    // Called by GridManager before selecting next blocks
    public void UpdateStruggleScore(GridMetrics metrics)
    {
        currentRawStruggleScore = CalculateRawStruggleScore(metrics);
        ApplySmoothing();

        // Log DDA state periodically for //Debugging/tuning
//         //Debug.Log($"DDA Update: Raw={currentRawStruggleScore:F2}, Smoothed={currentSmoothedStruggleScore:F2}");
    }

    private float CalculateRawStruggleScore(GridMetrics metrics)
    {
        // Normalize metrics to 0-1 range where 1 represents max struggle
        float normDensity = metrics.BoardDensity; // Already 0-1

        // Normalize available placements: 1 - (available / max). Max struggle at 0 placements.
        float normAvailablePlacements = 1.0f;
        if (metrics.MaxPossiblePlacements > 0)
        {
            normAvailablePlacements = Mathf.Clamp01(1.0f - (float)metrics.AvailablePlacements / metrics.MaxPossiblePlacements);
        }

        // Normalize TimeSinceClear: Clamp and scale (needs tuning based on typical gameplay speed)
        float maxExpectedTime = 30.0f; // Example: max struggle after 30s/moves without clear
        float normTimeSinceClear = Mathf.Clamp01(metrics.TimeSinceClear / maxExpectedTime);

        // Normalize IsolatedHoles: Scale based on a reasonable max expected count
        float maxExpectedHoles = 10.0f;
        float normIsolatedHoles = Mathf.Clamp01(metrics.IsolatedHoles / maxExpectedHoles);

        // Normalize BoardBumpiness: Scale based on a reasonable max expected value
        float maxExpectedBumpiness = 50.0f; // Example value, needs tuning
        float normBoardBumpiness = Mathf.Clamp01(metrics.BoardBumpiness / maxExpectedBumpiness);

        // Future Unplaceable Penalty
        float normFutureUnplaceable = metrics.IsAnyFuturePieceUnplaceable ? 1.0f : 0.0f;

        // Time Pressure Factor - REMOVED from score calculation
        // float timePressureFactor = GetTimePressureBias();

        // Calculate weighted score
        // Note: Removed weightTimePressure from total weight
        float totalWeight = weightDensity + weightAvailablePlacements + weightTimeSinceClear + weightIsolatedHoles + weightBoardBumpiness + weightFutureUnplaceable;
        if (totalWeight <= 0) totalWeight = 1; // Avoid division by zero

        float rawScore = (
            (weightDensity * normDensity) +
            (weightAvailablePlacements * normAvailablePlacements) +
            (weightTimeSinceClear * normTimeSinceClear) +
            (weightIsolatedHoles * normIsolatedHoles) +
            (weightBoardBumpiness * normBoardBumpiness) +
            (weightFutureUnplaceable * normFutureUnplaceable) // REMOVED: + (weightTimePressure * timePressureFactor)
        ) / totalWeight;

        return Mathf.Clamp01(rawScore);
    }

    private void ApplySmoothing()
    {
        historicalScores.Add(currentRawStruggleScore);
        if (historicalScores.Count > smoothingWindow)
        {
            historicalScores.RemoveAt(0); // Keep window size
        }

        if (historicalScores.Count > 0)
        {
            currentSmoothedStruggleScore = historicalScores.Average();
        }
        else
        {
            currentSmoothedStruggleScore = currentRawStruggleScore;
        }
    }

    // Calculate the time pressure factor (0 early game, 1 late game)
    // Renamed: This bias is used directly by GridManager to force harder blocks
    
    public float GetTimePressureBias() 
    {
        if (gameEndTime <= 0) return 0f; // Avoid division by zero

        // Simple linear increase for now. Could be adjusted (e.g., exponential near the end)
        float pressure = 0;

        // Optional: Make the pressure ramp up more sharply near the end
        // float rampUpStart = 0.8f; // Start sharp ramp-up at 80% of time
        // if (gameTimer / gameEndTime > rampUpStart) {
        //     float progressInRamp = (gameTimer / gameEndTime - rampUpStart) / (1.0f - rampUpStart);
        //     pressure = Mathf.Lerp(rampUpStart, 1.0f, progressInRamp * progressInRamp); // Quadratic ramp-up
        // }

        return pressure;
    }

    // Calculate the bias factor for piece selection (Tier 1)
    // This factor biases towards EASIER blocks based on struggle score
    public float GetTier1AdjustmentFactor()
    {
        if (currentSmoothedStruggleScore < struggleThresholdLow)
        {
            // Optional: Could slightly bias towards harder pieces if desired
            // return - (1 - (currentSmoothedStruggleScore / struggleThresholdLow)) * maxAdjustmentFactor;
            return 0; // No adjustment below low threshold for now
        }
        else if (currentSmoothedStruggleScore > struggleThresholdHigh)
        {
            return maxAdjustmentFactor; // Max positive adjustment (bias easy)
        }
        else // Between low and high thresholds
        {
            // Linear scaling
            float scale = (currentSmoothedStruggleScore - struggleThresholdLow) / (struggleThresholdHigh - struggleThresholdLow);
            return scale * maxAdjustmentFactor;
        }
    }

    // --- Player Performance Tracking ---

    // Called from GridManager when a block is placed successfully *with* line clears
    public void RecordSuccess()
    {
        consecutiveSuccesses++;
        consecutiveFailures = 0;
        // Reset game over counter on success
        recentGameOverCount = 0;
        // //Debug.Log($"Success Recorded. Consecutive: {consecutiveSuccesses}.");
        // Note: Score update happens immediately after metrics are provided.
    }

    // Called from GridManager when a block is placed successfully *without* line clears
    public void RecordFailure()
    {
        consecutiveFailures++;
        consecutiveSuccesses = 0;
        // //Debug.Log($"Failure (No Clear) Recorded. Consecutive: {consecutiveFailures}.");
        // Note: Score update happens immediately after metrics are provided.
    }

    // Called from GridManager when a game over condition is met
    public void RecordGameOver()
    {
        recentGameOverCount++;
        consecutiveFailures = 0; // Reset placement failure count on game over
        consecutiveSuccesses = 0;
        //Debug.Log($"Game Over Recorded. Recent Count: {recentGameOverCount}. Final Time: {gameTimer:F1}s");
        // Consider resetting struggle score or historical data on game over if desired
        // historicalScores.Clear();
        // currentSmoothedStruggleScore = 0;
    }
}

// Helper attribute for ReadOnly fields in Inspector (Optional but nice)
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        UnityEditor.EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
#endif 