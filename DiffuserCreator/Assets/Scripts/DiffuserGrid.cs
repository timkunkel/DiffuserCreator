using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DiffuserCreator
{
    // Lays out a grid of DiffuserBlocks and delegates their depth to a DepthShaper. It owns the
    // configuration (grid size, spacing, block size, depth source, curves) and exposes it as
    // properties so the runtime control panel can drive a rebuild.
    public class DiffuserGrid : MonoBehaviour
    {
        #region Serialized fields

        [Header("Grid")]
        [SerializeField]
        private int _rows, _columns;

        [SerializeField]
        private float _horizontalSpacing, _verticalSpacing;

        [Header("Block")]
        [SerializeField]
        private float _blockWidth, _blockHeight, _blockDepth;

        [SerializeField]
        private DiffuserBlock _blockPrefab;

        [Header("Depth")]
        [SerializeField]
        private DepthSource _depthSource = DepthSource.Curve;

        [SerializeField]
        private HeightMode _heightMode = HeightMode.Middle;

        [SerializeField]
        private LayerMask _cuttingLayerMask;

        [SerializeField]
        private float _defaultDepth = 1f;

        [Header("Curve")]
        public CurveMode SelectedCurveMode;

        public bool           UseHorizontalCurve;
        public AnimationCurve HorizontalCurve;
        public bool           UseVerticalCurve;
        public AnimationCurve VerticalCurve;

        [FormerlySerializedAs("UseDioganalCurve")]
        public bool UseDiagonalCurve;

        [FormerlySerializedAs("DioganalCurve")]
        public AnimationCurve DiagonalCurve;

        public int SnapAngle = 5;

        #endregion

        #region Runtime state

        private DiffuserBlock[,] _blocks;

        public float Width  => _columns * _blockWidth + (_columns - 1) * _horizontalSpacing;
        public float Height => _rows * _blockHeight + (_rows - 1) * _verticalSpacing;

        #endregion

        #region Settings properties

        public int   Rows              { get => _rows;              set => _rows = Mathf.Max(1, value); }
        public int   Columns           { get => _columns;           set => _columns = Mathf.Max(1, value); }
        public float HorizontalSpacing { get => _horizontalSpacing; set => _horizontalSpacing = value; }
        public float VerticalSpacing   { get => _verticalSpacing;   set => _verticalSpacing = value; }
        public float BlockWidth        { get => _blockWidth;        set => _blockWidth = value; }
        public float BlockHeight       { get => _blockHeight;       set => _blockHeight = value; }
        public float BlockDepth        { get => _blockDepth;        set => _blockDepth = value; }
        public float DefaultDepth      { get => _defaultDepth;      set => _defaultDepth = value; }

        public DepthSource DepthSource { get => _depthSource; set => _depthSource = value; }
        public HeightMode  HeightMode  { get => _heightMode;  set => _heightMode = value; }
        public CurveMode   CurveMode   { get => SelectedCurveMode; set => SelectedCurveMode = value; }

        #endregion

        #region MonoBehaviour methods

        private void Start()
        {
            Generate();
        }

        #endregion

        #region Grid generation

        [ContextMenu("Generate Grid")]
        public void Generate()
        {
            DestroyBlocks();

            _blocks = new DiffuserBlock[_rows, _columns];

            float totalWidth  = Width;
            float totalHeight = Height;

            float rowStartPosition    = -totalHeight / 2f;
            float columnStartPosition = -totalWidth / 2f;

            float currentVerticalSpacing = 0f;

            for (int row = 0; row < _rows; row++)
            {
                float y = row * _blockHeight + _blockHeight / 2f + currentVerticalSpacing + rowStartPosition;

                float currentHorizontalSpacing = 0f;
                for (int column = 0; column < _columns; column++)
                {
                    float x = column * _blockWidth + _blockWidth / 2f + currentHorizontalSpacing + columnStartPosition;

                    _blocks[row, column] = CreateBlock(new Vector2(x, y));

                    currentHorizontalSpacing += _horizontalSpacing;
                }

                currentVerticalSpacing += _verticalSpacing;
            }

            Reshape();
        }

        private DiffuserBlock CreateBlock(Vector2 blockPosition)
        {
            DiffuserBlock block = Instantiate(_blockPrefab, blockPosition, Quaternion.identity, transform);
            block.SetSize(_blockWidth, _blockHeight, _blockDepth);
            block.NormalizedPosition = Normalize(blockPosition);
            return block;
        }

        private Vector2 Normalize(Vector2 blockPosition)
        {
            float width  = Width;
            float height = Height;
            float x      = width  > 0f ? (blockPosition.x + width / 2f) / width : 0f;
            float y      = height > 0f ? (blockPosition.y + height / 2f) / height : 0f;
            return new Vector2(x, y);
        }

        private void DestroyBlocks()
        {
            if (_blocks == null) { return; }

            foreach (DiffuserBlock block in _blocks)
            {
                if (block != null)
                {
                    Destroy(block.gameObject);
                }
            }

            _blocks = null;
        }

        #endregion

        #region Shaping

        [ContextMenu("Reshape Blocks")]
        public void Reshape()
        {
            if (_blocks == null) { return; }

            DiffuserSettings settings = BuildSettings();
            DepthShaper       shaper   = DepthShaper.For(_depthSource);

            foreach (DiffuserBlock block in _blocks)
            {
                if (block != null)
                {
                    shaper.Shape(block, settings);
                }
            }
        }

        private DiffuserSettings BuildSettings()
        {
            return new DiffuserSettings
            {
                HeightMode         = _heightMode,
                CurveMode          = SelectedCurveMode,
                UseHorizontalCurve = UseHorizontalCurve,
                HorizontalCurve    = HorizontalCurve,
                UseVerticalCurve   = UseVerticalCurve,
                VerticalCurve      = VerticalCurve,
                UseDiagonalCurve   = UseDiagonalCurve,
                DiagonalCurve      = DiagonalCurve,
                SnapAngle          = SnapAngle,
                CuttingLayerMask   = _cuttingLayerMask,
                DefaultDepth       = _defaultDepth
            };
        }

        #endregion

        #region Grid operations

        [ContextMenu("Offset X")]
        private void OffsetX()
        {
            if (_blocks == null) { return; }

            for (int row = 0; row < _blocks.GetLength(0); row += 2)
            {
                for (int column = 0; column < _blocks.GetLength(1); column++)
                {
                    DiffuserBlock block    = _blocks[row, column];
                    Vector3       localPos = block.transform.localPosition;
                    block.transform.localPosition = new Vector3(localPos.x + 0.5f, localPos.y, localPos.z);
                }
            }
        }

        [ContextMenu("Rotate 90°")]
        private void Rotate90()
        {
            if (_blocks == null) { return; }

            foreach (DiffuserBlock block in _blocks)
            {
                block.transform.Rotate(Vector3.back, 90);
            }
        }

        [ContextMenu("Print Grid")]
        public void PrintGrid()
        {
            if (_blocks == null) { return; }

            var blocksByAngle = new Dictionary<int, int>();
            foreach (DiffuserBlock block in _blocks)
            {
                blocksByAngle.TryGetValue(block.Angle, out int count);
                blocksByAngle[block.Angle] = count + 1;
            }

            foreach (KeyValuePair<int, int> entry in blocksByAngle)
            {
                Debug.Log("Angle " + entry.Key + ": " + entry.Value);
            }
        }

        #endregion

        #region Mesh export

#if UNITY_EDITOR
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

            SaveMesh(mesh, "Diffusor_generated", true, false);
        }

        private static void SaveMesh(Mesh mesh, string name, bool makeNewInstance, bool optimizeMesh)
        {
            string path = EditorUtility.SaveFilePanel("Save Separate Mesh Asset", "Assets/GeneratedMesh", name, "asset");
            if (string.IsNullOrEmpty(path)) { return; }

            path = FileUtil.GetProjectRelativePath(path);

            Mesh meshToSave = makeNewInstance ? Instantiate(mesh) : mesh;

            if (optimizeMesh)
            {
                MeshUtility.Optimize(meshToSave);
            }

            AssetDatabase.CreateAsset(meshToSave, path);
            AssetDatabase.SaveAssets();
        }
#endif

        #endregion
    }
}
