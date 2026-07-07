using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DiffuserCreator.Papercraft.Tests
{
    public class UnfolderTests
    {
        [Test]
        public void Cube_UnfoldsIntoOneConnectedPiece()
        {
            PapercraftModel     model  = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());
            bool[]              folds  = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces = Unfolder.Unfold(model, folds);

            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(6, pieces[0].Faces.Count);
        }

        [Test]
        public void Cube_UnfoldingPreservesEdgeLengths()
        {
            AssertIsometry(PapercraftTestMeshes.Cube24());
        }

        [Test]
        public void PerturbedCube_UnfoldingPreservesEdgeLengths()
        {
            AssertIsometry(PapercraftTestMeshes.Cube24(1f, 1f, 1f, 2f));
        }

        [Test]
        public void Cube_UnfoldingPreservesFaceAreas()
        {
            PapercraftModel     model  = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());
            bool[]              folds  = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces = Unfolder.Unfold(model, folds);

            foreach (UnfoldedFace face in pieces[0].Faces)
            {
                Assert.AreEqual(1f, Polygon2D.Area(face.Outline), 1e-4f, $"face {face.FaceIndex} area changed");
            }
        }

        [Test]
        public void FoldEdges_EndpointsCoincideBetweenParentAndChild()
        {
            PapercraftModel     model  = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24(1f, 1f, 1f, 2f));
            bool[]              folds  = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces = Unfolder.Unfold(model, folds);

            var instances = new Dictionary<int, UnfoldedFace>();
            foreach (UnfoldedPiece piece in pieces)
            {
                foreach (UnfoldedFace face in piece.Faces)
                {
                    instances[face.FaceIndex] = face;
                }
            }

            for (int e = 0; e < model.Edges.Length; e++)
            {
                if (!folds[e]) { continue; }

                PapercraftEdge edge = model.Edges[e];
                (Vector2 a1, Vector2 b1) = EdgeSegment(model, instances[edge.FaceA], e);
                (Vector2 a2, Vector2 b2) = EdgeSegment(model, instances[edge.FaceB], e);

                bool sameOrder = Vector2.Distance(a1, a2) < 1e-4f && Vector2.Distance(b1, b2) < 1e-4f;
                bool swapped   = Vector2.Distance(a1, b2) < 1e-4f && Vector2.Distance(b1, a2) < 1e-4f;
                Assert.IsTrue(sameOrder || swapped, $"fold edge {e} does not coincide between its two faces");
            }
        }

        private static void AssertIsometry(PapercraftMeshData data)
        {
            PapercraftModel     model  = PapercraftTestMeshes.BuildModel(data);
            bool[]              folds  = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces = Unfolder.Unfold(model, folds);

            foreach (UnfoldedPiece piece in pieces)
            {
                foreach (UnfoldedFace face in piece.Faces)
                {
                    int[] loop = model.Faces[face.FaceIndex].VertexIndices;
                    for (int i = 0; i < loop.Length; i++)
                    {
                        float length3D = Vector3.Distance(
                            model.Vertices[loop[i]], model.Vertices[loop[(i + 1) % loop.Length]]);
                        float length2D = Vector2.Distance(
                            face.Outline[i], face.Outline[(i + 1) % face.Outline.Length]);

                        Assert.AreEqual(length3D, length2D, 1e-4f,
                            $"edge {i} of face {face.FaceIndex} changed length during unfolding");
                    }
                }
            }
        }

        private static (Vector2, Vector2) EdgeSegment(PapercraftModel model, UnfoldedFace face, int edgeIndex)
        {
            int loop = System.Array.IndexOf(model.Faces[face.FaceIndex].EdgeIndices, edgeIndex);
            return (face.Outline[loop], face.Outline[(loop + 1) % face.Outline.Length]);
        }
    }
}
