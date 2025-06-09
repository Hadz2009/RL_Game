using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellBehavior : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color gridColor = new Color(0.2f, 0.2f, 0.22f); // Dark gray with slight blue tint for grid
    
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Make sure the sprite renderer exists
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // Set default size and color
      //  transform.localScale = new Vector3(0.95f, 0.95f, 1f); // Slightly smaller than 1x1 to create grid lines
        spriteRenderer.color = gridColor; // Use dark gray instead of white
    }
    
    public void SetFilled(bool filled)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = filled ? Color.gray : gridColor;
        }
    }
    
    public void SetFilledWithColor(bool filled, Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = filled ? color : gridColor;
        }
    }
} 