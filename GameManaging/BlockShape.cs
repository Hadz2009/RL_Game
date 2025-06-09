using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScriptableObject representing a Tetris-style block shape for Block Blast.
/// Stores cell positions, color, and various shape factory methods.
/// </summary>
[CreateAssetMenu(fileName = "New Block Shape", menuName = "Block Blast/Block Shape")]
public class BlockShape : ScriptableObject
{
    public string shapeName;
    public List<Vector2Int> cells = new List<Vector2Int>();
    public Color blockColor = Color.gray;

    /// <summary>
    /// Returns the number of cells in this shape (its complexity).
    /// </summary>
    public int GetComplexity() => cells.Count;

    // --- Factory methods for standard shapes ---

    public static BlockShape Create3x3()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "3x3";
        shape.blockColor = new Color(0.95f, 0.3f, 0.3f, 1f);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                shape.cells.Add(new Vector2Int(x, y));
        return shape;
    }

    public static BlockShape CreateHorizontalDuo()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Horizontal Duo";
        shape.blockColor = new Color(0.3f, 0.7f, 1f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        return shape;
    }

    public static BlockShape CreateVerticalDuo()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Vertical Duo";
        shape.blockColor = new Color(0.5f, 0.9f, 1f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(0, 1));
        return shape;
    }

    public static BlockShape CreateTriBlock()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Triple Block";
        shape.blockColor = new Color(1f, 0.6f, 0.1f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(2, 0));
        return shape;
    }

    public static BlockShape CreateSquare()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Square";
        shape.blockColor = new Color(1f, 0.95f, 0.2f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(1, 1));
        return shape;
    }

    public static BlockShape CreateLShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "L Shape";
        shape.blockColor = new Color(1f, 0.45f, 0.85f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(0, 2));
        shape.cells.Add(new Vector2Int(1, 0));
        return shape;
    }

    public static BlockShape Create5x1()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "5x1";
        shape.blockColor = new Color(0.2f, 0.9f, 0.4f, 1f);
        for (int x = 0; x < 5; x++)
            shape.cells.Add(new Vector2Int(x, 0));
        return shape;
    }

    public static BlockShape Create1x5()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "1x5";
        shape.blockColor = new Color(0.4f, 1f, 0.6f, 1f);
        for (int y = 0; y < 5; y++)
            shape.cells.Add(new Vector2Int(0, y));
        return shape;
    }

    public static BlockShape CreateJShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "J Shape";
        shape.blockColor = new Color(0.3f, 0.5f, 1f, 1f);
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(1, 1));
        shape.cells.Add(new Vector2Int(1, 2));
        shape.cells.Add(new Vector2Int(0, 2));
        return shape;
    }

    public static BlockShape CreateSShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "S Shape";
        shape.blockColor = new Color(1f, 0.5f, 0.2f, 1f);
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(2, 0));
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(1, 1));
        return shape;
    }

    public static BlockShape CreateZShapeInverse()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Z Shape Inverse";
        shape.blockColor = new Color(1f, 0.35f, 0.35f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(1, 1));
        shape.cells.Add(new Vector2Int(2, 1));
        return shape;
    }

    public static BlockShape CreateNShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "N Shape";
        shape.blockColor = new Color(0.7f, 0.2f, 1f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(1, 1));
        shape.cells.Add(new Vector2Int(1, 2));
        return shape;
    }

    public static BlockShape CreateZShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Z Shape";
        shape.blockColor = new Color(0.7f, 0.2f, 1f, 1f);
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(1, 1));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(2, 0));
        return shape;
    }

    public static BlockShape CreateVerticalTriple()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Vertical Triple";
        shape.blockColor = new Color(1f, 0.7f, 0.3f, 1f);
        for (int y = 0; y < 3; y++)
            shape.cells.Add(new Vector2Int(0, y));
        return shape;
    }

    public static BlockShape CreateVerticalQuadruple()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Vertical Quadruple";
        shape.blockColor = new Color(0.5f, 1f, 0.5f, 1f);
        for (int y = 0; y < 4; y++)
            shape.cells.Add(new Vector2Int(0, y));
        return shape;
    }

    public static BlockShape CreateHorizontalQuadruple()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Horizontal Quadruple";
        shape.blockColor = new Color(0.4f, 0.8f, 1f, 1f);
        for (int x = 0; x < 4; x++)
            shape.cells.Add(new Vector2Int(x, 0));
        return shape;
    }

    public static BlockShape CreateReverseTShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Reverse T Shape";
        shape.blockColor = new Color(0.95f, 0.6f, 1f, 1f);
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(1, 1));
        shape.cells.Add(new Vector2Int(2, 1));
        shape.cells.Add(new Vector2Int(1, 0));
        return shape;
    }

    public static BlockShape CreateTShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "T Shape";
        shape.blockColor = new Color(0.8f, 0.4f, 1f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(2, 0));
        shape.cells.Add(new Vector2Int(1, 1));
        return shape;
    }

    public static BlockShape CreateRightTShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Right T Shape";
        shape.blockColor = new Color(0.8f, 0.4f, 1f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(2, 0));
        shape.cells.Add(new Vector2Int(1, 1));
        return shape;
    }

    public static BlockShape CreateLeftTShape()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Left T Shape";
        shape.blockColor = new Color(0.8f, 0.4f, 1f, 1f);
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(1, 1));
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(1, 2));
        return shape;
    }

    public static BlockShape Create2x3Rectangle()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "2x3 Rectangle";
        shape.blockColor = new Color(0.6f, 0.8f, 0.3f, 1f);
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 2; y++)
                shape.cells.Add(new Vector2Int(x, y));
        return shape;
    }

    public static BlockShape Create3x2Rectangle()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "3x2 Rectangle";
        shape.blockColor = new Color(0.7f, 0.9f, 0.4f, 1f);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 3; y++)
                shape.cells.Add(new Vector2Int(x, y));
        return shape;
    }

    public static BlockShape CreateBigL()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Big L";
        shape.blockColor = new Color(1f, 0.5f, 0.6f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(0, 2));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(2, 0));
        return shape;
    }

    public static BlockShape CreateBigJ()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Big J";
        shape.blockColor = new Color(0.5f, 0.6f, 1f, 1f);
        shape.cells.Add(new Vector2Int(2, 0));
        shape.cells.Add(new Vector2Int(2, 1));
        shape.cells.Add(new Vector2Int(2, 2));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(0, 0));
        return shape;
    }

    public static BlockShape CreateCorner2x2()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Corner 2x2";
        shape.blockColor = new Color(0.8f, 0.6f, 0.9f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(0, 1));
        return shape;
    }

    public static BlockShape CreateCorner2x2Inverse()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Corner 2x2 Inverse";
        shape.blockColor = new Color(0.9f, 0.6f, 0.8f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(1, 1));
        return shape;
    }

    public static BlockShape CreateCorner3x3()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Corner 3x3";
        shape.blockColor = new Color(0.7f, 0.5f, 0.8f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(2, 0));
        shape.cells.Add(new Vector2Int(0, 1));
        shape.cells.Add(new Vector2Int(0, 2));
        return shape;
    }

    public static BlockShape CreateCorner3x3Inverse()
    {
        var shape = CreateInstance<BlockShape>();
        shape.shapeName = "Corner 3x3 Inverse";
        shape.blockColor = new Color(0.8f, 0.5f, 0.7f, 1f);
        shape.cells.Add(new Vector2Int(0, 0));
        shape.cells.Add(new Vector2Int(1, 0));
        shape.cells.Add(new Vector2Int(2, 0));
        shape.cells.Add(new Vector2Int(2, 1));
        shape.cells.Add(new Vector2Int(2, 2));
        return shape;
    }

    /// <summary>
    /// Returns a list of all standard shapes.
    /// </summary>
    public static List<BlockShape> GetStandardShapes()
    {
        var list = new List<BlockShape>
        {
 // Line blocks
            
            CreateHorizontalDuo(),
            CreateVerticalDuo(),
            CreateTriBlock(),
            CreateVerticalTriple(),
            CreateHorizontalQuadruple(),
          
            CreateVerticalQuadruple(),
            Create5x1(),
            Create1x5(),
            
            // Square & Rectangle blocks
            CreateSquare(),
            Create3x3(),
            Create2x3Rectangle(),
            Create3x2Rectangle(),
            
            //// L-shaped blocks
            
            CreateLShape(),
            CreateJShape(),
           
           
            //// T-shaped blocks
            CreateTShape(),
            CreateReverseTShape(),
            CreateRightTShape(),
            CreateLeftTShape(),
            

            //// S/Z-shaped blocks
            CreateSShape(),
            CreateZShape(),
            CreateZShapeInverse(),
            
            CreateNShape(),
            
            
            //// Corner blocks
            CreateCorner2x2(),
            CreateCorner2x2Inverse(),
            ////CreateCorner3x3(),
            ////CreateCorner3x3Inverse()

        };

        // Deduplicate by name
        var names = new HashSet<string>();
        var deduped = new List<BlockShape>();
        foreach (var shape in list)
        {
            if (!names.Contains(shape.shapeName))
            {
                names.Add(shape.shapeName);
                deduped.Add(shape);
            }
        }

        // Optionally, add more similarity checks here

        return deduped;
    }

    // Utility: Checks if two shapes are similar (not used, but kept for future)
    private static void CheckShapeSimilarity(string nameA, string nameB, List<BlockShape> shapes)
    {
        var a = shapes.Find(s => s.shapeName == nameA);
        var b = shapes.Find(s => s.shapeName == nameB);
        if (a == null || b == null) return;
        if (a.cells.Count != b.cells.Count) return;

        var aCells = a.cells.OrderBy(c => c.x).ThenBy(c => c.y).ToList();
        var bCells = b.cells.OrderBy(c => c.x).ThenBy(c => c.y).ToList();

        for (int i = 0; i < aCells.Count; i++)
            if (aCells[i] != bCells[i])
                return; // Not similar
        // Could add to a group here if needed
    }
}
