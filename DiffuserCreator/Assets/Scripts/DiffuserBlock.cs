using UnityEngine;

namespace DiffuserCreator
{
    // A single diffuser cell: a unit cube (scaled by transform.localScale) whose four front-face
    // corners are pushed toward -Z by independent depths. It owns only its geometry and mesh; how the
    // depths are decided lives outside, in a DepthShaper (see DepthShaper.cs).
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    public class DiffuserBlock : MonoBehaviour
    {
        // Back-face corners in local space (z = 0), order: bottom-right, top-right, top-left, bottom-left.
        // Front-face corners share the same XY and are offset to -depth. Depth i belongs to front corner i.
        public static readonly Vector3[] BackCorners =
        {
            new Vector3(0.5f,  -0.5f, 0f),
            new Vector3(0.5f,  0.5f,  0f),
            new Vector3(-0.5f, 0.5f,  0f),
            new Vector3(-0.5f, -0.5f, 0f)
        };

        #region Serialized fields

        [SerializeField]
        private VertexIndicator _vertexIndicatorPrefab;

        #endregion

        #region State

        public float Width  => transform.localScale.x;
        public float Height => transform.localScale.y;
        public float Depth  => transform.localScale.z;

        public float   InitialDepth       { get; private set; }
        public Vector2 NormalizedPosition { get; set; }
        public int     Angle              { get; set; }

        private readonly Vector3[] _points = new Vector3[8];
        private readonly float[]   _depth  = new float[4];

        private MeshFilter   _meshFilter;
        private MeshCollider _collider;

        private VertexIndicator[] _indicators;

        #endregion

        #region MonoBehaviour methods

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _collider   = GetComponent<MeshCollider>();

            InitPoints();
            SetUniformDepth(InitialDepth > 0f ? InitialDepth : 1f);
        }

        #endregion

        #region Public API

        public void SetSize(float width, float height, float depth)
        {
            InitialDepth         = depth;
            transform.localScale = new Vector3(width, height, depth);
        }

        public void SetUniformDepth(float depth)
        {
            SetDepths(depth, depth, depth, depth);
        }

        public void SetDepths(float depth0, float depth1, float depth2, float depth3)
        {
            _depth[0] = depth0;
            _depth[1] = depth1;
            _depth[2] = depth2;
            _depth[3] = depth3;

            ApplyDepthToPoints();
            BuildMesh();
        }

        #endregion

        #region Mesh construction

        private void InitPoints()
        {
            _points[0] = BackCorners[0];
            _points[1] = BackCorners[1];
            _points[2] = BackCorners[2];
            _points[3] = BackCorners[3];
            ApplyDepthToPoints();
        }

        private void ApplyDepthToPoints()
        {
            _points[4] = new Vector3(0.5f,  -0.5f, -_depth[0]);
            _points[5] = new Vector3(0.5f,  0.5f,  -_depth[1]);
            _points[6] = new Vector3(-0.5f, 0.5f,  -_depth[2]);
            _points[7] = new Vector3(-0.5f, -0.5f, -_depth[3]);
        }

        private void BuildMesh()
        {
            Mesh mesh = _meshFilter.mesh;
            mesh.name = "DiffuserBlock";
            mesh.Clear();
            mesh.vertices  = Vertices;
            mesh.triangles = Triangles;
            mesh.RecalculateNormals();

            _collider.sharedMesh = mesh;
        }

        private Vector3[] Vertices => new[]
        {
            _points[0], _points[1], _points[2], _points[3], // back
            _points[4], _points[5], _points[6], _points[7], // front
            _points[2], _points[3], _points[6], _points[7], // left
            _points[0], _points[1], _points[4], _points[5], // right
            _points[1], _points[2], _points[5], _points[6], // top
            _points[0], _points[3], _points[4], _points[7]  // bottom
        };

        // Winding chosen so RecalculateNormals faces every quad outward (front is reversed vs back).
        private static int[] Triangles => new[]
        {
            0, 1, 2, 0, 2, 3,       // back
            7, 6, 5, 7, 5, 4,       // front
            9, 8, 10, 9, 10, 11,    // left
            13, 12, 14, 13, 14, 15, // right
            17, 16, 18, 17, 18, 19, // top
            20, 21, 23, 20, 23, 22  // bottom
        };

        #endregion

        #region Vertex indicators

        public void ShowIndicators()
        {
            if (_vertexIndicatorPrefab == null) { return; }

            if (_indicators == null)
            {
                _indicators = new VertexIndicator[_points.Length];
                for (int i = 0; i < _points.Length; i++)
                {
                    VertexIndicator indicator = Instantiate(_vertexIndicatorPrefab, transform);
                    indicator.transform.localPosition = _points[i];
                    indicator.SetIndex(i);
                    _indicators[i] = indicator;
                }
            }

            foreach (VertexIndicator indicator in _indicators)
            {
                indicator.gameObject.SetActive(true);
            }
        }

        public void HideIndicators()
        {
            if (_indicators == null) { return; }

            foreach (VertexIndicator indicator in _indicators)
            {
                indicator.gameObject.SetActive(false);
            }
        }

        #endregion
    }
}
