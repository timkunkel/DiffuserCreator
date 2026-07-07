using System.Collections.Generic;
using DiffuserCreator.Papercraft;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DiffuserCreator
{
    // Lays out a grid of DiffuserBlocks and delegates their depth to a DepthShaper. All configuration
    // lives in a single serialized DiffuserSettings object, exposed to the runtime control panel via
    // the Settings property; the block prefab is the only other serialized reference.
    public class DiffuserGrid : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField]
        private DiffuserSettings _settings = new DiffuserSettings();

        [SerializeField]
        private DiffuserBlock _blockPrefab;

        #endregion

        #region Runtime state

        private DiffuserBlock[,] _blocks;

        public DiffuserSettings Settings => _settings;

        public float Width  => _settings.Columns * _settings.BlockWidth + (_settings.Columns - 1) * _settings.HorizontalSpacing;
        public float Height => _settings.Rows * _settings.BlockHeight + (_settings.Rows - 1) * _settings.VerticalSpacing;

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

            int rows    = Mathf.Max(1, _settings.Rows);
            int columns = Mathf.Max(1, _settings.Columns);

            _blocks = new DiffuserBlock[rows, columns];

            float rowStartPosition    = -Height / 2f;
            float columnStartPosition = -Width / 2f;

            float currentVerticalSpacing = 0f;

            for (int row = 0; row < rows; row++)
            {
                float y = row * _settings.BlockHeight + _settings.BlockHeight / 2f
                          + currentVerticalSpacing + rowStartPosition;

                float currentHorizontalSpacing = 0f;
                for (int column = 0; column < columns; column++)
                {
                    float x = column * _settings.BlockWidth + _settings.BlockWidth / 2f
                              + currentHorizontalSpacing + columnStartPosition;

                    _blocks[row, column] = CreateBlock(new Vector2(x, y));

                    currentHorizontalSpacing += _settings.HorizontalSpacing;
                }

                currentVerticalSpacing += _settings.VerticalSpacing;
            }

            Reshape();
        }

        private DiffuserBlock CreateBlock(Vector2 blockPosition)
        {
            DiffuserBlock block = Instantiate(_blockPrefab, blockPosition, Quaternion.identity, transform);
            block.SetSize(_settings.BlockWidth, _settings.BlockHeight, _settings.BlockDepth);
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

            DepthShaper shaper = DepthShaper.For(_settings.DepthSource);

            foreach (DiffuserBlock block in _blocks)
            {
                if (block != null)
                {
                    shaper.Shape(block, _settings);
                }
            }
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

        #region Papercraft

        // Every block's mesh, baked into grid-local space. Runtime-safe (no editor APIs); the
        // runtime control panel and the editor context menu both feed this to the papercraft export.
        public List<PapercraftMeshData> CollectPapercraftMeshes()
        {
            var meshes = new List<PapercraftMeshData>();
            foreach (DiffuserBlock block in GetComponentsInChildren<DiffuserBlock>())
            {
                MeshFilter filter = block.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) { continue; }

                Matrix4x4 gridLocal = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                meshes.Add(PapercraftMeshData.FromMesh(filter.sharedMesh, gridLocal));
            }

            return meshes;
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

        [ContextMenu("Export Papercraft")]
        public void ExportPapercraft()
        {
            List<PapercraftMeshData> meshes = CollectPapercraftMeshes();
            if (meshes.Count == 0)
            {
                Debug.LogWarning("No block meshes to export - generate the grid (Play mode or Generate Grid) first.");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export Papercraft", "", "Diffusor_papercraft", "pdf");
            if (string.IsNullOrEmpty(path)) { return; }

            PapercraftResult result = PapercraftExporter.Export(meshes, new PapercraftOptions());
            PapercraftFiles.Write(result, path);

            Debug.Log($"Papercraft export: {result.PieceCount} pieces on {result.Pages.Count} page(s) "
                      + $"at {result.AppliedScaleMmPerUnit:0.#} mm/unit, "
                      + $"{result.OverlapSplitCount} overlap split(s) -> {path}");
        }
#endif

        #endregion
    }
}
