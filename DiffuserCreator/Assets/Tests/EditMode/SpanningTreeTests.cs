using NUnit.Framework;

namespace DiffuserCreator.Papercraft.Tests
{
    public class SpanningTreeTests
    {
        [Test]
        public void Cube_HasFiveFoldsAndSevenCuts()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());
            bool[]          folds = SpanningTreeBuilder.BuildFoldEdges(model);

            Assert.AreEqual(5, CountFolds(folds));
        }

        [Test]
        public void Cube_FoldEdgesConnectAllFaces()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());
            bool[]          folds = SpanningTreeBuilder.BuildFoldEdges(model);

            var parent = new int[model.Faces.Length];
            for (int i = 0; i < parent.Length; i++) { parent[i] = i; }

            for (int e = 0; e < model.Edges.Length; e++)
            {
                if (!folds[e]) { continue; }
                parent[Find(parent, model.Edges[e].FaceA)] = Find(parent, model.Edges[e].FaceB);
            }

            int root = Find(parent, 0);
            for (int f = 1; f < model.Faces.Length; f++)
            {
                Assert.AreEqual(root, Find(parent, f), $"face {f} is not fold-connected to face 0");
            }
        }

        [Test]
        public void PerturbedCube_FrontDiagonalIsAlwaysAFold()
        {
            PapercraftModel model = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24(1f, 1f, 1f, 2f));
            bool[]          folds = SpanningTreeBuilder.BuildFoldEdges(model);

            Assert.AreEqual(6, CountFolds(folds));

            int flattest = -1;
            for (int e = 0; e < model.Edges.Length; e++)
            {
                if (flattest < 0 || model.Edges[e].DihedralAngleDeg < model.Edges[flattest].DihedralAngleDeg)
                {
                    flattest = e;
                }
            }

            Assert.IsTrue(folds[flattest], "the near-flat front diagonal must be folded, not cut");
        }

        private static int CountFolds(bool[] folds)
        {
            int count = 0;
            foreach (bool fold in folds)
            {
                if (fold) { count++; }
            }
            return count;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i) { i = parent[i]; }
            return i;
        }
    }
}
