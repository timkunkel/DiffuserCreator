using System.Linq;
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

    public AnimationCurve HorizontalCurve;
    public AnimationCurve VerticalCurve;

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
            block.UpdateDepthWithCurve();
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
                                                      HorizontalCurve);
        }

        for (int i = 0; i < _columns; i++)
        {
            _blockColumns[i] = new DiffuserBlockSequence(GetBlocksFromColumn(i),
                                                         DiffuserBlockSequence.SequenceOrientation.Vertical,
                                                         VerticalCurve);
        }
    }

    private DiffuserBlock CreateBlock(Vector2 blockPosition)
    {
        DiffuserBlock block = Instantiate(_blockPrefab, blockPosition, Quaternion.identity, transform);
        block.SetSize(_blockWidth, _blockHeight, _blockDepth);
        block.Initialize(this, blockPosition, HorizontalCurve, VerticalCurve);
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
}