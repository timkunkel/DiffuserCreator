using System;
using UnityEngine;
using UnityEngine.ProBuilder;

public class DiffuserBlock : MonoBehaviour
{
    public enum HeightMode
    {
        Middle,
        Corner,
        Horizontal,
        Vertical
    }

    public enum HeightEditing
    {
        Cutting, Curve, Custom
    }

    private const float DEFAULT_DEPTH = 1f;

    public HeightMode    Mode        = HeightMode.Middle;
    public HeightEditing EditingMode = HeightEditing.Cutting;

    public float Width  => transform.localScale.x;
    public float Height => transform.localScale.y;
    public float Depth  => transform.localScale.z;

    [SerializeField]
    private LayerMask _cuttingLayerMask;

    [SerializeField]
    private VertexIndicator _vertexIndicatorPrefab;

    private bool _useHorizontalCurve, _useVerticalCurve, _useDioganalCurve;

    private Vector3[] _points       = new Vector3[8];
    private Vector3[] _bottomPoints = new Vector3[4];
    private Vector3[] _topPoints    = new Vector3[4];

    private float[] _depth = new float[4];

    private MeshFilter   _meshFilter;
    private MeshCollider _collider;

    private DiffuserGrid _diffuserGrid;
    private Vector2      _positionInGrid;
    private Vector2      _relativePosInGrid;

    private AnimationCurve _horizontalCurve;
    private AnimationCurve _verticalCurve;
    private AnimationCurve _dioganalCurve;

    private float _initialDepth;

    private VertexIndicator[] _indicators;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _collider   = GetComponent<MeshCollider>();
        SetDepth(DEFAULT_DEPTH);
        InitPoints();
        BuildMesh();
    }

    public void Initialize(
        DiffuserGrid           grid, Vector2 positionInGrid, AnimationCurve horizontalCurve,
        AnimationCurve         verticalCurve,
        AnimationCurve         diagonalCurve,
        DiffuserGrid.CurveMode curveMode)
    {
        _diffuserGrid = grid;
        var normalizedPos = new Vector2(positionInGrid.x + grid.Width / 2f, positionInGrid.y + grid.Height / 2f);
        _positionInGrid    = normalizedPos;
        _relativePosInGrid = new Vector2(_positionInGrid.x / grid.Width, _positionInGrid.y / grid.Height);

        _horizontalCurve    = horizontalCurve;
        _verticalCurve      = verticalCurve;
        _dioganalCurve      = diagonalCurve;
        _useHorizontalCurve = grid.UseHorizontalCurve;
        _useVerticalCurve   = grid.UseVerticalCurve;
        _useDioganalCurve   = grid.UseDioganalCurve;
        UpdateDepthWithCurve(curveMode);
        BuildMesh();
    }

    public void SetSize(float width, float height, float depth)
    {
        _initialDepth        = depth;
        transform.localScale = new Vector3(width, height, depth);
    }

    public void CutWithSurface()
    {
        if (EditingMode != HeightEditing.Cutting)
        {
            return;
        }

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
                SetDepthsBetweenIndices(0, 3);
                SetDepthsBetweenIndices(1, 2);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        ApplyDepthToPoints();
        BuildMesh();
    }

    public void UpdateDepthWithCurve(DiffuserGrid.CurveMode curveMode)
    {
        if (EditingMode != HeightEditing.Curve)
        {
            return;
        }

        switch (curveMode)
        {
            case DiffuserGrid.CurveMode.Height:
                SetDepthWithHeightCurve();
                break;
            case DiffuserGrid.CurveMode.Angle:
                SetDepthWithAngleCurve();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(curveMode), curveMode, null);
        }
    }

    private void SetDepthWithAngleCurve()
    {
        float horizontalAngle = 0f;
        float verticalAngle   = 0f;
        int   curveCount      = 0;

        if (_useDioganalCurve)
        {
            curveCount++;
            horizontalAngle += _dioganalCurve.Evaluate((_relativePosInGrid.x + _relativePosInGrid.y) / 2f);
        }
        if (_useHorizontalCurve)
        {
            curveCount++;
            horizontalAngle += _horizontalCurve.Evaluate(_relativePosInGrid.x);
        }
        if (_useVerticalCurve)
        {
            curveCount++;
            horizontalAngle += _verticalCurve.Evaluate(_relativePosInGrid.y);
        }

        if (curveCount > 0)
        {
            horizontalAngle /= curveCount;
        }

        SetDepth(_initialDepth);
        Vector3 dir1 = _points[5] - _points[4];
        Vector3 dir2 = Quaternion.Euler(horizontalAngle, 0, 0) * dir1;
        LineLineIntersection(out Vector3 intersection, _points[4], dir2, _points[1], _points[5] - _points[1]);
        float magnitude = (_points[5] - intersection).magnitude;
        Debug.LogError("Magnitude " + magnitude);
        float moreDepth = _initialDepth + magnitude;

        SetDepth(_initialDepth, _initialDepth, moreDepth, moreDepth);
    }

    private void SetDepthWithHeightCurve()
    {
        float value = 0f;

        if (_useHorizontalCurve)
        {
            value += _horizontalCurve.Evaluate(_relativePosInGrid.x);
        }

        if (_useVerticalCurve)
        {
            value += _verticalCurve.Evaluate(_relativePosInGrid.y);
        }

        if (_useHorizontalCurve && _useVerticalCurve)
        {
            // average of both curves
            value /= 2f;
        }

        float depthValue = _initialDepth + value * _initialDepth;
        SetDepth(depthValue);
    }

    public void SetCurve(
        AnimationCurve curve, DiffuserBlockSequence.SequenceOrientation orientation, DiffuserGrid.CurveMode curveMode)
    {
        switch (orientation)
        {
            case DiffuserBlockSequence.SequenceOrientation.Horizontal:
                _horizontalCurve    = curve;
                _useHorizontalCurve = true;
                break;
            case DiffuserBlockSequence.SequenceOrientation.Vertical:
                _verticalCurve    = curve;
                _useVerticalCurve = false;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null);
        }

        UpdateDepthWithCurve(curveMode);
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

    public static bool LineLineIntersection(
        out Vector3 intersection, Vector3 linePoint1,
        Vector3     lineVec1,     Vector3 linePoint2, Vector3 lineVec2)
    {
        Vector3 lineVec3      = linePoint2 - linePoint1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

        float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

        //is coplanar, and not parallel
        if (Mathf.Abs(planarFactor) < 0.0001f
            && crossVec1and2.sqrMagnitude > 0.0001f)
        {
            float s = Vector3.Dot(crossVec3and2, crossVec1and2)
                      / crossVec1and2.sqrMagnitude;
            intersection = linePoint1 + (lineVec1 * s);
            return true;
        }
        else
        {
            intersection = Vector3.zero;
            return false;
        }
    }

    public void ShowIndicators()
    {
        if (_indicators == null)
        {
            _indicators = new VertexIndicator[_points.Length];
            for (int i = 0; i < _points.Length; i++)
            {
                var             point     = _points[i];
                VertexIndicator indicator = Instantiate(_vertexIndicatorPrefab, transform);
                indicator.transform.localPosition = point;
                indicator.SetIndex(i);
                _indicators[i] = indicator;
            }
        }

        foreach (VertexIndicator vertexIndicator in _indicators)
        {
            vertexIndicator.gameObject.SetActive(true);
        }
    }

    private void OnDrawGizmos()
    {
    }

    public void HideIndicators()
    {
        if (_indicators == null) { return; }

        foreach (VertexIndicator vertexIndicator in _indicators)
        {
            vertexIndicator.gameObject.SetActive(false);
        }
    }
}