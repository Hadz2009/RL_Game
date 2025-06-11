using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private UIManager uiManager;

    [Header("AI Agent Control")]
    [Tooltip("Check this to use the new BlockProviderAgent. Uncheck to use the old GridManager DDA.")]
    public bool useBlockProviderAgent = true; // <-- ADD THIS LINE

    public bool IsAgentTrainingMode = false;
    public bool useRandomBlockSpawningEqualWeights = false;
    private int score = 0;
    private bool gameOver = false;
    public bool playerAgentIsTrainingMode = false;
    [Header("Gameplay Mode")]
    public bool isManualPlayMode = false; // This is the flag for manual play
    
    void Start()
    {
        //Debug.Log("GameManager Start called");
        InitializeGame();
    }
    
    void InitializeGame()
    {
        //Debug.Log("Initializing game...");
        // Reset score
        score = 0;
        
        // Initialize UI
        if (uiManager != null)
        {
            uiManager.UpdateScoreText(score);
        }
        else
        {
            //Debug.LogWarning("UIManager reference not set!");
        }
        
        // Initialize grid manager with block shapes
        if (gridManager != null)
        {
            //Debug.Log("Setting up block shapes...");
            gridManager.availableBlocks = BlockShape.GetStandardShapes();
            //Debug.Log($"Number of block shapes added: {gridManager.availableBlocks.Count}");
        }
        else
        {
            //Debug.LogError("Grid Manager reference not set in Game Manager!");
        }
    }
    
    public void AddScore(int points)
    {
        score += points;
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateScoreText(score);
        }
    }
    
    // New method to get the current score
    public int GetScore()
    {
        return score;
    }
    
    // Reset the score and update the UI
    public void ResetScore()
    {
        score = 0;
        if (uiManager != null)
        {
            uiManager.UpdateScoreText(score);
            //Debug.Log("Score reset to 0.");
        }
        else
        {
            //Debug.LogWarning("UIManager not found when trying to reset score UI.");
        }
    }

    public void RestartGame()
    {
        // Reset game state
        gameOver = false;
        
        // Reload the scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }

    // Method for GridManager to check if the game is currently active
    public bool IsGameActive()
    {
        return !gameOver;
    }

    // Example of how to toggle manual mode (e.g., in Update or via a UI button)
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleManualPlayMode();
        }
        
        // ... rest of your Update method if any ...
    }

    public void ToggleManualPlayMode()
    {
        isManualPlayMode = !isManualPlayMode;
        //Debug.Log($"MANUAL PLAY MODE TOGGLED: {isManualPlayMode}");
    }
} 