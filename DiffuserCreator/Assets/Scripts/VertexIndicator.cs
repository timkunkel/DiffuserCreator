using TMPro;
using UnityEngine;

namespace DiffuserCreator
{
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
}
