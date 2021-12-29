using System;
using RuntimeHandle;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public enum Mode
    {
        Idle, Hovering, Selecting
    }

    [SerializeField]
    private LayerMask _blockLayer, _cuttingLayer;

    [SerializeField]
    private LayerMask _selectionLayer;

    private Selectable _hoveredSelectable, _selectedSelectable;

    private Camera _mainCamera;

    [SerializeField]
    private Mode _currentMode = Mode.Idle;

    private RuntimeTransformHandle _transformHandle;

    private void Awake()
    {
        _transformHandle      = RuntimeTransformHandle.Create(transform, HandleType.POSITION);
        _transformHandle.name = "TransformHandle";
        _selectionLayer       = _blockLayer;
    }

    // Start is called before the first frame update
    void Start()
    {
        _mainCamera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        if (_currentMode == Mode.Idle)
        {
            return;
        }

        CheckHoveredSelectable();
        CheckForSelection();
    }

    private void CheckForSelection()
    {
        if (Input.GetMouseButtonUp(0))
        {
            if (_selectedSelectable)
            {
                _selectedSelectable.Deselect();
            }

            if (_hoveredSelectable && _hoveredSelectable != _selectedSelectable)
            {
                _selectedSelectable = _hoveredSelectable;
                _selectedSelectable.Select();
            }
            else
            {
                _selectedSelectable = null;
            }
        }
    }

    private void CheckHoveredSelectable()
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000, _selectionLayer))
        {
            var selectable = hit.collider.gameObject.GetComponent<Selectable>();
            if (!selectable)
            {
                if (_hoveredSelectable)
                {
                    _hoveredSelectable.Unhover();
                    _hoveredSelectable = null;
                }

                return;
            }

            if (_hoveredSelectable && _hoveredSelectable != selectable)
            {
                _hoveredSelectable.Unhover();
            }

            _hoveredSelectable = selectable;
            _hoveredSelectable.Hover();
        }
        else if (_hoveredSelectable)
        {
            _hoveredSelectable.Unhover();
            _hoveredSelectable = null;
        }
    }
}