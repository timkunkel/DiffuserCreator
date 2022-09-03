using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VertexIndicator : MonoBehaviour
{
    public int Index;

    [SerializeField]
    private TextMeshPro _textMeshPro;

    public void SetIndex(int index)
    {
        Index             = index;
        _textMeshPro.text = Index.ToString();
    }
}
