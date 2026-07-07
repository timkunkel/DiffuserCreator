using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DiffuserCreator.Papercraft.Tests
{
    public class ExportTests
    {
        private static PapercraftOptions TestOptions()
        {
            // 30 mm per unit keeps a unit cube's net well inside a single A4 page.
            return new PapercraftOptions { MillimetersPerModelUnit = 30f };
        }

        [Test]
        public void Cube_EveryInteriorCutEdgeGetsExactlyOneTab()
        {
            PapercraftModel     model  = PapercraftTestMeshes.BuildModel(PapercraftTestMeshes.Cube24());
            bool[]              folds  = SpanningTreeBuilder.BuildFoldEdges(model);
            List<UnfoldedPiece> pieces = OverlapResolver.Resolve(model, folds, out _);

            foreach (UnfoldedPiece piece in pieces)
            {
                piece.Scale(30f);
            }

            List<GlueTab> tabs = TabGenerator.CreateTabs(model, pieces, folds, TestOptions());

            Assert.AreEqual(7, tabs.Count, "a cube has 12 edges, 5 folds, so 7 cut edges need 7 tabs");

            var seenEdges = new HashSet<int>();
            foreach (GlueTab tab in tabs)
            {
                Assert.IsTrue(seenEdges.Add(tab.EdgeIndex), $"edge {tab.EdgeIndex} received more than one tab");
                Assert.AreEqual(4, tab.Outline.Length);
            }
        }

        [Test]
        public void Cube_ExportProducesOnePageWithPairedLabels()
        {
            PapercraftResult result = PapercraftExporter.Export(
                new[] { PapercraftTestMeshes.Cube24() }, TestOptions());

            Assert.AreEqual(1, result.Pages.Count);
            Assert.AreEqual(1, result.PieceCount);
            Assert.AreEqual(0, result.OverlapSplitCount);
            Assert.AreEqual(14, result.Pages[0].Labels.Count, "7 cut edges must be labeled on both sides");

            var labelCounts = new Dictionary<string, int>();
            foreach (PapercraftLabel label in result.Pages[0].Labels)
            {
                labelCounts.TryGetValue(label.Text, out int count);
                labelCounts[label.Text] = count + 1;
            }

            Assert.AreEqual(7, labelCounts.Count);
            foreach (KeyValuePair<string, int> entry in labelCounts)
            {
                Assert.AreEqual(2, entry.Value, $"label {entry.Key} must appear exactly twice");
            }
        }

        [Test]
        public void Cube_SvgPageContainsSolidCutsDashedFoldsAndLabels()
        {
            PapercraftResult result = PapercraftExporter.Export(
                new[] { PapercraftTestMeshes.Cube24() }, TestOptions());

            string svg = result.SvgPages[0];
            StringAssert.Contains("<svg", svg);
            StringAssert.Contains("stroke-dasharray", svg);
            Assert.AreEqual(14, Regex.Matches(svg, "<text").Count);
        }

        [Test]
        public void Cube_PdfIsWellFormed()
        {
            PapercraftResult result = PapercraftExporter.Export(
                new[] { PapercraftTestMeshes.Cube24() }, TestOptions());

            string pdf = Encoding.ASCII.GetString(result.PdfBytes);
            StringAssert.StartsWith("%PDF-1.4", pdf);
            StringAssert.EndsWith("%%EOF", pdf);
            StringAssert.Contains("/Count 1", pdf);
            StringAssert.Contains("/BaseFont /Helvetica", pdf);
            StringAssert.Contains("(7)", pdf);
        }

        [Test]
        public void PerturbedCube_ExportsWithSplitFrontFace()
        {
            PapercraftResult result = PapercraftExporter.Export(
                new[] { PapercraftTestMeshes.Cube24(1f, 1f, 1f, 2f) }, TestOptions());

            Assert.GreaterOrEqual(result.Pages.Count, 1);
            Assert.IsNotEmpty(result.SvgPages[0]);
        }

        [Test]
        public void TwoMeshes_GetDistinctLabelNumbers()
        {
            PapercraftResult result = PapercraftExporter.Export(
                new[] { PapercraftTestMeshes.Cube24(), PapercraftTestMeshes.Cube24() }, TestOptions());

            Assert.AreEqual(2, result.PieceCount);

            var labels = new HashSet<string>();
            foreach (PapercraftPage page in result.Pages)
            {
                foreach (PapercraftLabel label in page.Labels)
                {
                    labels.Add(label.Text);
                }
            }

            Assert.AreEqual(14, labels.Count, "the second cube must continue numbering at 8-14, not reuse 1-7");
        }
    }
}
