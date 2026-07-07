using UnityEngine;

namespace DiffuserCreator.Papercraft.Tests
{
    // Test fixtures. Cube24 replicates the DiffuserBlock mesh layout exactly: 24 vertices
    // (4 per face, duplicated for flat shading), 12 triangles, front corners pushed to -depth
    // in BR, TR, TL, BL order. OverlappingFan is a strip of five near-flat triangles whose apex
    // angles sum to ~400 degrees, so any single-piece unfolding must self-overlap.
    public static class PapercraftTestMeshes
    {
        public static PapercraftMeshData Cube24(float depth0 = 1f, float depth1 = 1f, float depth2 = 1f, float depth3 = 1f)
        {
            var p = new[]
            {
                new Vector3(0.5f,  -0.5f, 0f),
                new Vector3(0.5f,  0.5f,  0f),
                new Vector3(-0.5f, 0.5f,  0f),
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f,  -0.5f, -depth0),
                new Vector3(0.5f,  0.5f,  -depth1),
                new Vector3(-0.5f, 0.5f,  -depth2),
                new Vector3(-0.5f, -0.5f, -depth3)
            };

            var vertices = new[]
            {
                p[0], p[1], p[2], p[3],
                p[4], p[5], p[6], p[7],
                p[2], p[3], p[6], p[7],
                p[0], p[1], p[4], p[5],
                p[1], p[2], p[5], p[6],
                p[0], p[3], p[4], p[7]
            };

            var triangles = new[]
            {
                0, 1, 2, 0, 2, 3,
                7, 6, 5, 7, 5, 4,
                9, 8, 10, 9, 10, 11,
                13, 12, 14, 13, 14, 15,
                17, 16, 18, 17, 18, 19,
                20, 21, 23, 20, 23, 22
            };

            return new PapercraftMeshData { Vertices = vertices, Triangles = triangles };
        }

        public static PapercraftMeshData OverlappingFan()
        {
            var vertices = new Vector3[7];
            vertices[0] = Vector3.zero;

            for (int i = 0; i < 6; i++)
            {
                float angle = 80f * i * Mathf.Deg2Rad;
                float z     = i % 2 == 0 ? 0.05f : -0.05f;
                vertices[1 + i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), z);
            }

            var triangles = new int[5 * 3];
            for (int i = 0; i < 5; i++)
            {
                triangles[i * 3]     = 0;
                triangles[i * 3 + 1] = 1 + i;
                triangles[i * 3 + 2] = 2 + i;
            }

            return new PapercraftMeshData { Vertices = vertices, Triangles = triangles };
        }

        public static PapercraftModel BuildModel(PapercraftMeshData data)
        {
            return PapercraftModel.Build(data.Vertices, data.Triangles);
        }
    }
}
