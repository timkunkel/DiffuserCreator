using UnityEngine;

namespace DiffuserCreator
{
    public class SelectableBlock : MonoBehaviour
    {
        [SerializeField]
        private Renderer _renderer;

        [SerializeField]
        private Material _selectedMaterial, _hoveredMaterial;

        [SerializeField]
        private DiffuserBlock _block;

        private Material _originalMaterial;

        private bool _isHovered, _isSelected;

        private void Start()
        {
            if (!_renderer)
            {
                _renderer = GetComponent<Renderer>();
            }

            if (_renderer)
            {
                _originalMaterial = _renderer.material;
            }
        }

        public void Select()
        {
            _isSelected        = true;
            _renderer.material = _selectedMaterial;
        }

        public void Deselect()
        {
            _isSelected        = false;
            _renderer.material = _originalMaterial;
            _block.HideIndicators();
        }

        public void Hover()
        {
            _isHovered = true;
            if (!_isSelected)
            {
                _renderer.material = _hoveredMaterial;
                _block.ShowIndicators();
            }
        }

        public void Unhover()
        {
            _isHovered = false;
            if (!_isSelected)
            {
                _renderer.material = _originalMaterial;
                _block.HideIndicators();
            }
        }
    }
}
