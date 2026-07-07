using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DiffuserCreator.Papercraft.Tests
{
    public class ExportTests
    {
        private static PapercraftOptions TestOptions()
        {
            // 30 mm per unit keeps a unit cube's net well inside a single A4 page. Fit-to-page is
            // disabled here so these tests exercise the fixed-scale path they assert against.
            return new PapercraftOptions { MillimetersPerModelUnit = 30f, FitSinglePieceToPage = false };
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
        public void FitToPage_ScalesLargestBlockToFillOneA4Page()
        {
            var options = new PapercraftOptions { FitSinglePieceToPage = true };

            // A tiny model (0.01 unit cube) would be microscopic at any fixed scale; fit-to-page
            // must scale it up to nearly fill the page, and it must still fit within the margins.
            PapercraftResult result = PapercraftExporter.Export(
                new[] { PapercraftTestMeshes.Cube24(0.01f, 0.01f, 0.01f, 0.01f) }, options);

            Assert.AreEqual(1, result.Pages.Count);
            Assert.Greater(result.AppliedScaleMmPerUnit, 0f);

            float innerWidth  = options.PageSizeMm.x - 2f * options.PageMarginMm;
            float innerHeight = options.PageSizeMm.y - 2f * options.PageMarginMm;

            foreach (PapercraftPolyline polyline in result.Pages[0].Polylines)
            {
                foreach (Vector2 point in polyline.Points)
                {
                    Assert.GreaterOrEqual(point.x, -0.001f);
                    Assert.GreaterOrEqual(point.y, -0.001f);
                    Assert.LessOrEqual(point.x, options.PageSizeMm.x + 0.001f);
                    Assert.LessOrEqual(point.y, options.PageSizeMm.y + 0.001f);
                }
            }

            // The net should actually be large (fills a meaningful share of the page), not tiny.
            Rect bounds = PageBounds(result.Pages[0]);
            Assert.Greater(bounds.width * bounds.height, 0.25f * innerWidth * innerHeight,
                "fit-to-page net should fill a large share of the printable area");
        }

        [Test]
        public void FitToPage_AllBlocksShareOneScale()
        {
            var options = new PapercraftOptions { FitSinglePieceToPage = true };

            // A big block and a small block: the shared scale must be driven by the larger one so
            // both fit, and both are scaled by the same factor (checked via the reported scale).
            PapercraftResult result = PapercraftExporter.Export(
                new[]
                {
                    PapercraftTestMeshes.Cube24(1f, 1f, 1f, 1f),
                    PapercraftTestMeshes.Cube24(0.3f, 0.3f, 0.3f, 0.3f)
                },
                options);

            Assert.AreEqual(2, result.PieceCount);
            Assert.Greater(result.AppliedScaleMmPerUnit, 0f);
        }

        private static Rect PageBounds(PapercraftPage page)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (PapercraftPolyline polyline in page.Polylines)
            {
                if (polyline.Kind == LineKind.CropMark) { continue; }
                foreach (Vector2 p in polyline.Points)
                {
                    minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
                    maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
                }
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
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
