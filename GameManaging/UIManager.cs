using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Canvas mainCanvas;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private GameManager gameManager;
    
    // Colors for score text based on game mode
    private Color normalScoreColor = Color.white;
    private Color hardModeScoreColor = Color.red;
    
    // Reference to the difficulty manager to check time pressure
    private DifficultyManager difficultyManager;
    
    void Start()
    {
        // Get reference to DifficultyManager
       // difficultyManager = DifficultyManager.Instance;
        difficultyManager = FindObjectOfType<DifficultyManager>();
        if (difficultyManager == null)
        {
            Debug.LogWarning("DifficultyManager not found in UIManager!");
        }
        
        // Ensure Canvas is properly set up
        if (mainCanvas == null)
        {
            mainCanvas = GetComponentInChildren<Canvas>();
            if (mainCanvas == null)
            {
                Debug.LogError("No Canvas found in UIManager or its children!");
            }
        }

        // Ensure Canvas has required components
        if (mainCanvas != null)
        {
            if (mainCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                mainCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
        
        // Hide game over panel on start
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        // Set up restart button
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartButtonClicked);
        }
        
        // Initialize score text
        UpdateScoreText(0);
    }
    
    void Update()
    {
        // Check if we should update the score color based on time pressure
        UpdateScoreColor();
    }
    
    // Update score color based on game progress
    private void UpdateScoreColor()
    {
        if (difficultyManager == null || scoreText == null) return;
        
        float timePressure = difficultyManager.GetTimePressureBias();
        
        // If in hard mode (85% or later), change score color to red
        if (timePressure >= 0.85f)
        {
            // Optional: Make the color pulse for extra effect in hard mode
            float pulseIntensity = Mathf.PingPong(Time.time * 2f, 1f) * 0.3f + 0.7f;
            scoreText.color = new Color(hardModeScoreColor.r * pulseIntensity, 
                                      hardModeScoreColor.g * pulseIntensity, 
                                      hardModeScoreColor.b * pulseIntensity);
        }
        else
        {
            scoreText.color = normalScoreColor;
        }
    }
    
    public Canvas GetMainCanvas()
    {
        return mainCanvas;
    }
    
    public void UpdateScoreText(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {score}";
        }
    }
    
    public void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }
    
    private void OnRestartButtonClicked()
    {
        if (gameManager != null)
        {
            gameManager.RestartGame();
        }
    }
} 