# Block Blast Prototype

This is a simple prototype of the Block Blast game, focusing on the core mechanics:
- Block movement via drag and drop
- Block spawning with Dynamic Difficulty Adjustment (DDA)
- Grid awareness
- Line clearing
- Score tracking

## Recommended Setup

### Grid and Cell Size Guidelines
- Cell Size: 0.6 units (set in GridManager)
- Cell Visual Scale: 0.9 of cell size (set in CellBehavior)
- Grid Size: 8x10 cells (standard)
- Block Cell Scale: 0.6 units (should match grid cell size)

### Optimal Grid Positioning
The grid is now positioned using the gridOffset parameter in GridManager. For the classic Block Blast look, place the grid in the center-top of the screen with blocks appearing below.

## Setup Instructions

1. Create the Cell Prefab:
   - Create a new Empty GameObject
   - Add a SpriteRenderer component (use a square sprite, white color)
   - Add the CellBehavior script
   - Add a Box Collider 2D component (set size to 0.9, 0.9)
   - Drag it to the Prefabs folder to create a prefab

2. Create the Block Prefab:
   - Create a new Empty GameObject
   - Add the BlockController script
   - Add a Box Collider 2D component (for drag handling)
   - Set the Cell Prefab field to the cell prefab you created
   - Set the Cell Scale to 0.6 to match grid cell size
   - Drag it to the Prefabs folder to create a prefab

3. Setup the Main Scene:
   - Create an Empty GameObject named "GameManager" and add the GameManager script
   - Create an Empty GameObject named "GridManager" and add the GridManager script
   - Create an Empty GameObject named "BlocksContainer" to hold the blocks
   - Add the CameraSetup script to the Main Camera
   - Create UI elements for score and game over screens
   - Set references in the Inspector:
     - In GridManager: 
       - Set Cell Prefab, Block Prefab, Blocks Container
       - Set GameManager reference
       - Adjust Grid Offset if needed (0, 0 centers the grid)
       - Set Block Spawn Area (e.g., 0, -2) for the area below the grid
     - In GameManager: Set GridManager and UIManager references
     - In Camera Setup: Set the GridManager reference

4. Configure the BlockShapes:
   - The prototype includes several predefined BlockShape configurations
   - You can adjust or create additional shapes in the BlockShape.cs file

5. DDA Settings:
   - Adjust the DDA settings in the GridManager Inspector:
     - Initial Difficulty: Starting difficulty level (0-1)
     - Max Difficulty: Maximum difficulty level (0-1)
     - Difficulty Increase/Decrease Rate: How quickly difficulty changes

## How It Works

- The game starts with a grid and 3 randomly selected blocks
- Player drags and drops blocks onto the grid
- When a row or column is completely filled, it is cleared and points are awarded
- The difficulty adjusts based on player performance:
  - Increases when lines are cleared
  - Decreases after several failed placements
- Game ends when no valid moves are possible

## Customization

- Grid Size: Change gridWidth and gridHeight in GridManager
- Cell Size: Adjust cellSize in GridManager (recommend 0.5-0.7)
- Grid Position: Modify gridOffset in GridManager 
- Scoring: Adjust pointsPerLine and comboMultiplier in GridManager
- Difficulty: Modify the DDA settings to change how difficulty progresses
- Block Shapes: Add or modify shapes in BlockShape.cs

Enjoy experimenting with this prototype! 