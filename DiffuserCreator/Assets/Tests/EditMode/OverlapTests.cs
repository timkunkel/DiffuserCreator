using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiffuserCreator.Papercraft.Tests
{
    public class OverlapTests
    {
        private static Vector2[] Square(float x, float y, float size = 1f)
        {
            return new[]
            {
                new Vector2(x, y),
                new Vector2(x + size, y),
                new Vector2(x + size, y + size),
                new Vector2(x, y + size)
            };
        }

        [Test]
        public void Overlaps_DetectsIntersectingSquares()
        {
            Assert.IsTrue(Polygon2D.Overlaps(Square(0f, 0f), Square(0.5f, 0.5f)));
        }

        [Test]
        public void Overlaps_DetectsContainedPolygon()
        {
            Assert.IsTrue(Polygon2D.Overlaps(Square(0f, 0f, 4f), Square(1f, 1f)));
        }

        [Test]
        public void Overlaps_IgnoresDisjointSquares()
        {
            Assert.IsFalse(Polygon2D.Overlaps(Square(0f, 0f), Square(2f, 0f)));
        }

        [Test]
        public void Overlaps_IgnoresSquaresTouchingAlongASharedEdge()
        {
            Assert.IsFalse(Polygon2D.Overlaps(Square(0f, 0f), Square(1f, 0f)));
        }

        [Test]
        public void Cube_ResolvesWithoutAnySplit()
        {
            PapercraftModel     model  = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());
            bool[]              folds  = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces = OverlapResolver.Resolve(model, folds, out int splitCount);

            Assert.AreEqual(0, splitCount);
            Assert.AreEqual(1, pieces.Count);
        }

        [Test]
        public void PerturbedCube_ResolvedPiecesNeverOverlap()
        {
            PapercraftModel     model  = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24(1f, 1f, 1f, 2f));
            bool[]              folds  = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces = OverlapResolver.Resolve(model, folds, out _);

            AssertNoOverlaps(pieces);
        }

        [Test]
        public void OverlappingFan_IsSplitIntoMultiplePieces()
        {
            LogAssert.Expect(LogType.Warning, new Regex("overlap"));

            PapercraftModel     model  = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.OverlappingFan());
            bool[]              folds  = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces = OverlapResolver.Resolve(model, folds, out int splitCount);

            Assert.GreaterOrEqual(splitCount, 1);
            Assert.GreaterOrEqual(pieces.Count, 2);
            AssertNoOverlaps(pieces);
        }

        private static void AssertNoOverlaps(List<UnfoldedPiece> pieces)
        {
            foreach (UnfoldedPiece piece in pieces)
            {
                for (int i = 0; i < piece.Faces.Count; i++)
                {
                    for (int j = i + 1; j < piece.Faces.Count; j++)
                    {
                        Assert.IsFalse(
                            Polygon2D.Overlaps(piece.Faces[i].Outline, piece.Faces[j].Outline),
                            $"faces {piece.Faces[i].FaceIndex} and {piece.Faces[j].FaceIndex} overlap");
                    }
                }
            }
        }
    }
}
