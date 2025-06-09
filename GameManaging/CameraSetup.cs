using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraSetup : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private float padding = 1f;
    
    private Camera mainCamera;
    
    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
    }
    
    void Start()
    {
        if (gridManager == null)
        {
            return;
        }
        
        SetupCamera();
    }
    
    void SetupCamera()
    {
        // Calculate grid dimensions
        float gridWidth = gridManager.gridWidth * gridManager.cellSize;
        float gridHeight = gridManager.gridHeight * gridManager.cellSize;
        
        // Calculate camera position
        Vector3 cameraPosition = new Vector3(
            gridWidth * 0.5f - gridManager.cellSize * 0.5f,
            gridHeight * 0.5f - gridManager.cellSize * 0.5f,
            -10f
        );
        
        // Set camera position
        transform.position = cameraPosition;
        
        // Set orthographic size to fit the grid with padding
        float aspectRatio = (float)Screen.width / Screen.height;
        float verticalSize = (gridHeight + padding * 2) * 0.5f;
        float horizontalSize = (gridWidth + padding * 2) * 0.5f / aspectRatio;
        
        // Use the larger size to ensure the entire grid is visible
        mainCamera.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
    }
} 