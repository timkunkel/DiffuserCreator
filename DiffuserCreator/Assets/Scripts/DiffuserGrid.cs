using System.Collections;
using System.Collections.Generic;
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

    // Start is called before the first frame update
    void Start()
    {
        Generate();
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void Generate()
    {
        float currentVerticalSpacing = 0f;
        var   currentBlockPosition   = new Vector2();
        for (int i = 0; i < _rows; i++)
        {
            currentBlockPosition.y = i * _blockHeight + currentVerticalSpacing;
            
            float currentHorizontalSpacing = 0f;
            for (int j = 0; j < _columns; j++)
            {
                currentBlockPosition.x = j * _blockWidth + currentHorizontalSpacing;
                
                DiffuserBlock block = CreateBlock(currentBlockPosition);
                block.SetSize(_blockWidth, _blockHeight, _blockDepth);

                currentHorizontalSpacing += _horizontalSpacing;
            }

            currentVerticalSpacing += _verticalSpacing;
        }
    }

    private DiffuserBlock CreateBlock(Vector2 blockPosition)
    {
        return Instantiate(_blockPrefab, blockPosition, Quaternion.identity, transform);
    }
}