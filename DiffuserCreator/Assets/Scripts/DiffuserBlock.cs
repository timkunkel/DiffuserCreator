using System;
using UnityEngine;

public class DiffuserBlock : MonoBehaviour
{
    private const float DEFAULT_DEPTH = 1f;

    public float Width  => transform.localScale.x;
    public float Height => transform.localScale.y;
    public float Depth  => transform.localScale.z;

    [SerializeField]
    private LayerMask _cuttingLayerMask;

    private MeshFilter _meshFilter;

    private float _depth = DEFAULT_DEPTH;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        CreateCube();
    }

    public void SetSize(float width, float height, float depth)
    {
        transform.localScale = new Vector3(width, height, depth);
    }

    public void CutWithSurface()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.back), out hit,
                            DEFAULT_DEPTH * Depth,
                            _cuttingLayerMask))
        {
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.back) * hit.distance, Color.yellow);
            // Debug.Log("Did Hit");

            _depth = -transform.InverseTransformPoint(hit.point).z;
            CreateCube();
        }
        else
        {
            _depth = DEFAULT_DEPTH;
            CreateCube();
        }
    }

    private void CreateCube()
    {
        var points = new Vector3[8];
        points[0] = new Vector3(0.5f,  -0.5f, 0f);
        points[1] = new Vector3(0.5f,  0.5f,  0f);
        points[2] = new Vector3(-0.5f, 0.5f,  0f);
        points[3] = new Vector3(-0.5f, -0.5f, 0f);

        points[4] = new Vector3(0.5f,  -0.5f, -_depth);
        points[5] = new Vector3(0.5f,  0.5f,  -_depth);
        points[6] = new Vector3(-0.5f, 0.5f,  -_depth);
        points[7] = new Vector3(-0.5f, -0.5f, -_depth);

        Vector3[] vertices =
        {
            // back
            points[0], points[1], points[2], points[3],

            // front
            points[4], points[5], points[6], points[7],

            // left
            points[2], points[3], points[6], points[7],

            // right
            points[0], points[1], points[4], points[5],

            // top
            points[1], points[2], points[5], points[6],

            // bottom
            points[0], points[3], points[4], points[7]
        };

        int[] triangles =
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


        Mesh mesh = _meshFilter.mesh;
        mesh.name = "CutBlock";
        mesh.Clear();
        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    private void OnDrawGizmos()
    {
    }
}