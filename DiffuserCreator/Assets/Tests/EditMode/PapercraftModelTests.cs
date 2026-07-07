using NUnit.Framework;
using UnityEngine;

namespace DiffuserCreator.Papercraft.Tests
{
    public class PapercraftModelTests
    {
        [Test]
        public void Weld_ReducesDuplicatedCubeVerticesToEight()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());

            Assert.AreEqual(8, model.Vertices.Length);
        }

        [Test]
        public void CoplanarMerge_UniformCubeHasSixQuadFaces()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());

            Assert.AreEqual(6, model.Faces.Length);
            foreach (PapercraftFace face in model.Faces)
            {
                Assert.AreEqual(4, face.VertexIndices.Length);
            }
        }

        [Test]
        public void DualGraph_UniformCubeHasTwelveInteriorEdgesAtNinetyDegrees()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());

            Assert.AreEqual(12, model.Edges.Length);
            foreach (PapercraftEdge edge in model.Edges)
            {
                Assert.IsTrue(edge.IsInterior);
                Assert.AreEqual(90f, edge.DihedralAngleDeg, 0.01f);
            }
        }

        [Test]
        public void PerturbedCube_KeepsNonPlanarFrontAsTwoTriangles()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24(1f, 1f, 1f, 2f));

            Assert.AreEqual(7, model.Faces.Length);
            Assert.AreEqual(13, model.Edges.Length);

            int triangleCount = 0;
            foreach (PapercraftFace face in model.Faces)
            {
                if (face.VertexIndices.Length == 3) { triangleCount++; }
            }
            Assert.AreEqual(2, triangleCount);
        }

        [Test]
        public void PerturbedCube_FrontDiagonalIsFlattestEdge()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24(1f, 1f, 1f, 2f));

            float flattest = float.MaxValue;
            foreach (PapercraftEdge edge in model.Edges)
            {
                flattest = Mathf.Min(flattest, edge.DihedralAngleDeg);
            }

            Assert.Less(flattest, 45f);
        }

        [Test]
        public void FaceNormals_PointOutward()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());
            Vector3 center = new Vector3(0f, 0f, -0.5f);

            foreach (PapercraftFace face in model.Faces)
            {
                Vector3 faceCenter = Vector3.zero;
                foreach (int v in face.VertexIndices)
                {
                    faceCenter += model.Vertices[v];
                }
                faceCenter /= face.VertexIndices.Length;

                Assert.Greater(Vector3.Dot(face.Normal, faceCenter - center), 0f);
            }
        }
    }
}
