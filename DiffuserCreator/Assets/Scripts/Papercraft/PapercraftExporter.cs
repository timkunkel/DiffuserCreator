using System.Collections.Generic;
using UnityEngine;

namespace DiffuserCreator.Papercraft
{
    public struct PapercraftMeshData
    {
        public Vector3[] Vertices;
        public int[]     Triangles;

        public static PapercraftMeshData FromMesh(Mesh mesh)
        {
            return FromMesh(mesh, Matrix4x4.identity);
        }

        public static PapercraftMeshData FromMesh(Mesh mesh, Matrix4x4 transform)
        {
            Vector3[] source   = mesh.vertices;
            var       vertices = new Vector3[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                vertices[i] = transform.MultiplyPoint3x4(source[i]);
            }

            int[] triangles = (int[])mesh.triangles.Clone();
            if (transform.determinant < 0f)
            {
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
                }
            }

            return new PapercraftMeshData { Vertices = vertices, Triangles = triangles };
        }
    }

    public class PapercraftResult
    {
        public List<PapercraftPage> Pages;
        public string[]             SvgPages;
        public byte[]               PdfBytes;
        public int                  PieceCount;
        public int                  OverlapSplitCount;
    }

    // Facade over the whole pipeline: welded polygon model -> dual-graph spanning tree ->
    // tree unfolding -> overlap safety cuts -> glue tabs -> page layout -> SVG + PDF pages.
    // For a progress-reporting, cancelable run (e.g. from a UI coroutine), drive a PapercraftJob
    // directly; Export just enumerates one to completion.
    public static class PapercraftExporter
    {
        public static PapercraftResult Export(Mesh mesh, PapercraftOptions options = null)
        {
            return Export(new[] { PapercraftMeshData.FromMesh(mesh) }, options);
        }

        public static PapercraftResult Export(IReadOnlyList<PapercraftMeshData> meshes, PapercraftOptions options = null)
        {
            var job = new PapercraftJob(meshes, options);
            foreach (object _ in job.Run()) { }
            return job.Result;
        }
    }
}
