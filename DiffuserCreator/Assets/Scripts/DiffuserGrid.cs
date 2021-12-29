using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    
    public AnimationCurve Curve;

    private DiffuserBlock[,] _blocks;

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
    }

    private DiffuserBlock CreateBlock(Vector2 blockPosition)
    {
        DiffuserBlock block = Instantiate(_blockPrefab, blockPosition, Quaternion.identity, transform);
        block.SetSize(_blockWidth, _blockHeight, _blockDepth);
        block.Initialize(this, blockPosition);
        return block;
    }
}