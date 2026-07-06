using RuntimeHandle;
using UnityEngine;

namespace DiffuserCreator
{
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

        [SerializeField]
        private Mode _currentMode = Mode.Idle;

        private SelectableBlock _hoveredSelectableBlock, _selectedSelectableBlock;

        private Camera _mainCamera;

        private RuntimeTransformHandle _transformHandle;

        private void Awake()
        {
            _transformHandle                      = RuntimeTransformHandle.Create(transform, HandleType.POSITION);
            _transformHandle.name                 = "TransformHandle";
            _transformHandle.transform.localScale = new Vector3(2, 2, 2);
            _transformHandle.gameObject.SetActive(false);
            _selectionLayer                       = _blockLayer;
        }

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (_currentMode == Mode.Idle)
            {
                return;
            }

            // The gizmo consumes input first, so dragging it never changes the selection.
            bool handleWasUsed = _transformHandle.TryInteract();
            if (handleWasUsed)
            {
                return;
            }

            CheckHoveredSelectable();
            CheckForSelection();
        }

        private void CheckForSelection()
        {
            if (!Input.GetMouseButtonUp(0))
            {
                return;
            }

            if (_selectedSelectableBlock)
            {
                _selectedSelectableBlock.Deselect();
            }

            if (_hoveredSelectableBlock && _hoveredSelectableBlock != _selectedSelectableBlock)
            {
                _selectedSelectableBlock = _hoveredSelectableBlock;
                _selectedSelectableBlock.Select();
                ActivateTransformHandleForSelected();
            }
            else
            {
                _selectedSelectableBlock = null;
                _transformHandle.gameObject.SetActive(false);
            }
        }

        private void ActivateTransformHandleForSelected()
        {
            Transform selectedTransform = _selectedSelectableBlock.transform;
            _transformHandle.target             = null;
            _transformHandle.transform.position = selectedTransform.position;
            _transformHandle.target             = selectedTransform;
            _transformHandle.gameObject.SetActive(true);
        }

        private void CheckHoveredSelectable()
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 10000, _selectionLayer))
            {
                if (_hoveredSelectableBlock)
                {
                    _hoveredSelectableBlock.Unhover();
                    _hoveredSelectableBlock = null;
                }

                return;
            }

            var selectable = hit.collider.gameObject.GetComponent<SelectableBlock>();
            if (!selectable)
            {
                if (_hoveredSelectableBlock)
                {
                    _hoveredSelectableBlock.Unhover();
                    _hoveredSelectableBlock = null;
                }

                return;
            }

            if (_hoveredSelectableBlock && _hoveredSelectableBlock != selectable)
            {
                _hoveredSelectableBlock.Unhover();
            }

            _hoveredSelectableBlock = selectable;
            _hoveredSelectableBlock.Hover();
        }
    }
}
