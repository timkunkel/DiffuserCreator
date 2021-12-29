using System;
using UnityEngine;

public class DiffuserBlock : MonoBehaviour
{
    public enum HeightMode
    {
        Middle,
        Corner,
        Horizontal,
        Vertical
    }

    private const float DEFAULT_DEPTH = 1f;

    public HeightMode Mode = HeightMode.Middle;

    public float Width  => transform.localScale.x;
    public float Height => transform.localScale.y;
    public float Depth  => transform.localScale.z;

    [SerializeField]
    private LayerMask _cuttingLayerMask;

    private Vector3[] _points       = new Vector3[8];
    private Vector3[] _bottomPoints = new Vector3[4];
    private Vector3[] _topPoints    = new Vector3[4];

    private float[] _depth = new float[4];

    private MeshFilter   _meshFilter;
    private MeshCollider _collider;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _collider   = GetComponent<MeshCollider>();
        SetDepth(DEFAULT_DEPTH);
        InitPoints();
        BuildMesh();
    }

    public void SetSize(float width, float height, float depth)
    {
        transform.localScale = new Vector3(width, height, depth);
    }

    public void CutWithSurface()
    {
        RaycastHit hit;

        void SetDepthsBetweenIndices(int a, int b)
        {
            Vector3 origin = _bottomPoints[a] + 0.5f * (_bottomPoints[b] - _bottomPoints[a]);
            if (RaycastAgainstCuttingObjects(transform.position + origin, out hit))
            {
                _depth[a] = -transform.InverseTransformPoint(hit.point).z;
                _depth[b] = -transform.InverseTransformPoint(hit.point).z;
            }
            else
            {
                _depth[a] = DEFAULT_DEPTH;
                _depth[b] = DEFAULT_DEPTH;
            }
        }

        switch (Mode)
        {
            case HeightMode.Middle:
                if (RaycastAgainstCuttingObjects(transform.position, out hit))
                {
                    // Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.back) * hit.distance,
                    //               Color.yellow);

                    SetDepth(-transform.InverseTransformPoint(hit.point).z);
                }
                else
                {
                    SetDepth(DEFAULT_DEPTH);
                }

                break;
            case HeightMode.Corner:
                for (int i = 0; i < _bottomPoints.Length; i++)
                {
                    Vector3 bottomPoint = _bottomPoints[i];
                    if (RaycastAgainstCuttingObjects(transform.position + bottomPoint, out hit))
                    {
                        _depth[i] = -transform.InverseTransformPoint(hit.point).z;
                    }
                    else
                    {
                        _depth[i] = DEFAULT_DEPTH;
                    }
                }

                break;
            case HeightMode.Horizontal:
                SetDepthsBetweenIndices(0, 1);
                SetDepthsBetweenIndices(2, 3);
                break;
            case HeightMode.Vertical:
                SetDepthsBetweenIndices(0,3);
                SetDepthsBetweenIndices(1,2);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        ApplyDepthToPoints();
        BuildMesh();
    }

    private bool RaycastAgainstCuttingObjects(Vector3 origin, out RaycastHit hit)
    {
        return Physics.Raycast(origin, transform.TransformDirection(Vector3.back), out hit,
                               DEFAULT_DEPTH * Depth,
                               _cuttingLayerMask);
    }

    private void BuildMesh()
    {
        Mesh mesh = _meshFilter.mesh;
        mesh.name = "CutBlock";
        mesh.Clear();
        mesh.vertices  = Vertices;
        mesh.triangles = Triangles;
        mesh.RecalculateNormals();

        _collider.sharedMesh = mesh;
    }

    private static int[] Triangles => new[]
    {
        0, 1, 2, // back
        0, 2, 3,

        7, 6, 5, // front
        7, 5, 4,

        9, 8, 10, // left
        9, 10, 11,

        13, 12, 14, // right
        13, 14, 15,

        17, 16, 18, // top
        17, 18, 19,

        20, 21, 23, // bottom
        20, 23, 22
    };

    private Vector3[] Vertices => new[]
    {
        // back
        _points[0], _points[1], _points[2], _points[3],

        // front
        _points[4], _points[5], _points[6], _points[7],

        // left
        _points[2], _points[3], _points[6], _points[7],

        // right
        _points[0], _points[1], _points[4], _points[5],

        // top
        _points[1], _points[2], _points[5], _points[6],

        // bottom
        _points[0], _points[3], _points[4], _points[7]
    };


    private void InitPoints()
    {
        _points    = new Vector3[8];
        _points[0] = new Vector3(0.5f,  -0.5f, 0f);
        _points[1] = new Vector3(0.5f,  0.5f,  0f);
        _points[2] = new Vector3(-0.5f, 0.5f,  0f);
        _points[3] = new Vector3(-0.5f, -0.5f, 0f);
        _points[4] = new Vector3(0.5f,  -0.5f, -_depth[0]);
        _points[5] = new Vector3(0.5f,  0.5f,  -_depth[1]);
        _points[6] = new Vector3(-0.5f, 0.5f,  -_depth[2]);
        _points[7] = new Vector3(-0.5f, -0.5f, -_depth[3]);

        _bottomPoints = new[]
        {
            _points[0], _points[1], _points[2], _points[3]
        };

        _topPoints = new[]
        {
            _points[4], _points[5], _points[6], _points[7]
        };
    }

    private void SetDepth(float depth)
    {
        SetDepth(depth, depth, depth, depth);
    }

    private void SetDepth(float depth0, float depth1, float depth2, float depth3)
    {
        _depth[0] = depth0;
        _depth[1] = depth1;
        _depth[2] = depth2;
        _depth[3] = depth3;

        ApplyDepthToPoints();
    }

    private void ApplyDepthToPoints()
    {
        _points[4] = new Vector3(0.5f,  -0.5f, -_depth[0]);
        _points[5] = new Vector3(0.5f,  0.5f,  -_depth[1]);
        _points[6] = new Vector3(-0.5f, 0.5f,  -_depth[2]);
        _points[7] = new Vector3(-0.5f, -0.5f, -_depth[3]);
    }

    private void OnDrawGizmos()
    {
    }
}