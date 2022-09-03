using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Selectable : MonoBehaviour
{
    [SerializeField]
    private Renderer _renderer;

    [SerializeField]
    private Material _selectedMaterial, _hoveredMaterial;

    private Material _originalMaterial;

    private bool _isHovered, _isSelected;

        // Start is called before the first frame update
    void Start()
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

    // Update is called once per frame
    void Update()
    {
        
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
    }

    public void Hover()
    {
        _isHovered         = true;
        if (!_isSelected)
        {
            _renderer.material = _hoveredMaterial;
        }
    }

    public void Unhover()
    {
        _isHovered         = false;
        if (!_isSelected)
        {
            _renderer.material = _originalMaterial;
        }
    }
        
}
