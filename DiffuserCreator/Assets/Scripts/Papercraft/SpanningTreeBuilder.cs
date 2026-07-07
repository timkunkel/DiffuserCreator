namespace DiffuserCreator.Papercraft
{
    // Minimum spanning forest over the dual graph, weighted by dihedral angle: near-flat edges
    // become folds and the sharp creases become cuts, so glue seams land on the least visible
    // edges (Straub & Prautzsch 2011 style, without the optimization machinery).
    public static class SpanningTreeBuilder
    {
        public static bool[] BuildFoldEdges(PapercraftModel model)
        {
            var isFold = new bool[model.Edges.Length];
            var inTree = new bool[model.Faces.Length];

            for (int seed = 0; seed < model.Faces.Length; seed++)
            {
                if (inTree[seed]) { continue; }

                inTree[seed] = true;
                GrowTree(model, isFold, inTree);
            }

            return isFold;
        }

        private static void GrowTree(PapercraftModel model, bool[] isFold, bool[] inTree)
        {
            while (true)
            {
                int   best      = -1;
                float bestAngle = float.MaxValue;

                for (int e = 0; e < model.Edges.Length; e++)
                {
                    PapercraftEdge edge = model.Edges[e];
                    if (!edge.IsInterior) { continue; }
                    if (inTree[edge.FaceA] == inTree[edge.FaceB]) { continue; }

                    if (edge.DihedralAngleDeg < bestAngle)
                    {
                        bestAngle = edge.DihedralAngleDeg;
                        best      = e;
                    }
                }

                if (best < 0) { return; }

                isFold[best]                     = true;
                inTree[model.Edges[best].FaceA]  = true;
                inTree[model.Edges[best].FaceB]  = true;
            }
        }
    }
}
