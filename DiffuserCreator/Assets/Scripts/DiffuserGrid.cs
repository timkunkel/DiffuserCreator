using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class DiffuserGrid : MonoBehaviour
{
    [SerializeField]
    private int _rows, _columns;

    [SerializeField]
    private float _horizontalSpacing, _verticalSpacing;

    [SerializeField]
    private float _blockWidth, _blockHeight, _blockDepth;

    [SerializeField]
    private DiffuserBlock _blockPrefab;

    [Header("Curve")]
    public CurveMode SelectedCurveMode;

    public bool           UseHorizontalCurve;
    public AnimationCurve HorizontalCurve;
    public bool           UseVerticalCurve;
    public AnimationCurve VerticalCurve;
    public bool           UseDioganalCurve;
    public AnimationCurve DioganalCurve;

    public int SnapAngle = 5;

    private DiffuserBlock[,] _blocks;

    private DiffuserBlockSequence[] _blockRows;
    private DiffuserBlockSequence[] _blockColumns;

    public float Width  => _columns * _blockWidth + ((_columns - 1) * _horizontalSpacing);
    public float Height => _rows * _blockHeight + ((_rows - 1) * _verticalSpacing);

    void Start()
    {
        Generate();
    }

    private void Update()
    {
        //CutCubes();
        //UpdateCurveCubes();
    }

    private void UpdateCurveCubes()
    {
        foreach (DiffuserBlock block in _blocks)
        {
            block.UpdateDepthWithCurve(SelectedCurveMode);
        }
    }

    [ContextMenu("Cut with Surface")]
    private void CutCubes()
    {
        foreach (DiffuserBlock diffuserBlock in _blocks)
        {
            diffuserBlock.CutWithSurface();
        }
    }

    [ContextMenu("Generate Grid")]
    public void Generate()
    {
        if (_blocks != null && _blocks.Length > 0)
        {
            foreach (DiffuserBlock block in _blocks)
            {
                Destroy(block.gameObject);
            }
        }

        _blocks = new DiffuserBlock[_rows, _columns];

        float totalWidth  = Width;
        float totalHeight = Height;

        float rowStartPosition    = -totalHeight / 2;
        float columnStartPosition = -totalWidth / 2;

        float currentVerticalSpacing = 0f;
        var   currentBlockPosition   = new Vector2(columnStartPosition, rowStartPosition);

        for (int i = 0; i < _rows; i++)
        {
            currentBlockPosition.y = i * _blockHeight + _blockHeight / 2 + currentVerticalSpacing + rowStartPosition;

            float currentHorizontalSpacing = 0f;
            for (int j = 0; j < _columns; j++)
            {
                currentBlockPosition.x =
                    j * _blockWidth + _blockWidth / 2 + currentHorizontalSpacing + columnStartPosition;

                DiffuserBlock block = CreateBlock(currentBlockPosition);
                _blocks[i, j] = block;

                currentHorizontalSpacing += _horizontalSpacing;
            }

            currentVerticalSpacing += _verticalSpacing;
        }


        _blockRows    = new DiffuserBlockSequence[_rows];
        _blockColumns = new DiffuserBlockSequence[_columns];
        for (int i = 0; i < _rows; i++)
        {
            _blockRows[i] = new DiffuserBlockSequence(GetBlocksFromRow(i),
                                                      DiffuserBlockSequence.SequenceOrientation.Horizontal,
                                                      HorizontalCurve, SelectedCurveMode);
        }

        for (int i = 0; i < _columns; i++)
        {
            _blockColumns[i] = new DiffuserBlockSequence(GetBlocksFromColumn(i),
                                                         DiffuserBlockSequence.SequenceOrientation.Vertical,
                                                         VerticalCurve, SelectedCurveMode);
        }
    }

    private DiffuserBlock CreateBlock(Vector2 blockPosition)
    {
        DiffuserBlock block = Instantiate(_blockPrefab, blockPosition, Quaternion.identity, transform);
        block.SetSize(_blockWidth, _blockHeight, _blockDepth);
        block.Initialize(this, blockPosition, HorizontalCurve, VerticalCurve, DioganalCurve, SelectedCurveMode);
        return block;
    }

    public DiffuserBlock[] GetBlocksFromRow(int rowIndex)
    {
        var blocksInRow = new DiffuserBlock[_columns];
        for (int i = 0; i < _columns; i++)
        {
            blocksInRow[i] = _blocks[rowIndex, i];
        }

        return blocksInRow;
    }

    public DiffuserBlock[] GetBlocksFromColumn(int columnIndex)
    {
        var blocksInColumn = new DiffuserBlock[_rows];
        for (int i = 0; i < _rows; i++)
        {
            blocksInColumn[i] = _blocks[i, columnIndex];
        }

        return blocksInColumn;
    }

    public enum CurveMode
    {
        Height, Angle
    }

    [ContextMenu("Print Grid")]
    public void PrintGrid()
    {
        Dictionary<int, List<DiffuserBlock>> dic = new Dictionary<int, List<DiffuserBlock>>();
        foreach (DiffuserBlock diffuserBlock in _blocks)
        {
            if (!dic.ContainsKey(diffuserBlock.Angle))
            {
                dic[diffuserBlock.Angle] = new List<DiffuserBlock>();
            }

            dic[diffuserBlock.Angle].Add(diffuserBlock);
        }

        foreach (int dicKey in dic.Keys)
        {
            Debug.LogError("Angle " + dicKey + ": " + dic[dicKey].Count);
        }
    }

    [ContextMenu("Offset X")]
    public void OffsetX()
    {
        for (int i = 0; i < _blocks.GetLength(0); i += 2)
        {
            for (int j = 0; j < _blocks.GetLength(1); j++)
            {
                var block    = _blocks[i, j];
                var localPos = block.transform.localPosition;
                block.transform.localPosition = new Vector3(localPos.x + 0.5f, localPos.y, localPos.z);
            }
        }
    }

    [ContextMenu("Rotate 90°")]
    private void Rotate90()
    {
        for (int i = 0; i < _blocks.GetLength(0); i++)
        {
            for (int j = 0; j < _blocks.GetLength(1); j++)
            {
                var block = _blocks[i, j];
                block.transform.Rotate(Vector3.back, 90);
            }
        }
    }

    [ContextMenu("Save as Mesh")]
    public void SaveAsMesh()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        var          combine     = new CombineInstance[meshFilters.Length];

        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh      = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
        }

        var mesh = new Mesh();
        mesh.CombineMeshes(combine);

        //ObjExporter.DoExport(true);
        SaveMesh(mesh, "Diffusor_generated", true, false);
    }
    
    public static void SaveMesh (Mesh mesh, string name, bool makeNewInstance, bool optimizeMesh) {
        string path = EditorUtility.SaveFilePanel("Save Separate Mesh Asset", "Assets/GeneratedMesh", name, "asset");
        if (string.IsNullOrEmpty(path)) return;
        
        path = FileUtil.GetProjectRelativePath(path);

        Mesh meshToSave = (makeNewInstance) ? Object.Instantiate(mesh) as Mesh : mesh;
		
        if (optimizeMesh)
            MeshUtility.Optimize(meshToSave);
        
        AssetDatabase.CreateAsset(meshToSave, path);
        AssetDatabase.SaveAssets();
    }
}