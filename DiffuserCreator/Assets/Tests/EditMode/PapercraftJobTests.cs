using System.Collections;
using NUnit.Framework;

namespace DiffuserCreator.Papercraft.Tests
{
    public class PapercraftJobTests
    {
        private static PapercraftOptions TestOptions()
        {
            return new PapercraftOptions { MillimetersPerModelUnit = 30f };
        }

        [Test]
        public void Job_RunToCompletion_MatchesExport()
        {
            var meshes = new[] { PapercraftTestMeshes.Cube24(), PapercraftTestMeshes.Cube24(1f, 1f, 1f, 2f) };

            PapercraftResult reference = PapercraftExporter.Export(meshes, TestOptions());

            var job = new PapercraftJob(meshes, TestOptions());
            foreach (object _ in job.Run()) { }

            Assert.IsTrue(job.IsDone);
            Assert.IsNotNull(job.Result);
            Assert.AreEqual(reference.Pages.Count, job.Result.Pages.Count);
            Assert.AreEqual(reference.PieceCount, job.Result.PieceCount);
            Assert.AreEqual(reference.SvgPages.Length, job.Result.SvgPages.Length);
            Assert.AreEqual(reference.PdfBytes.Length, job.Result.PdfBytes.Length);
        }

        [Test]
        public void Job_Progress_IsMonotonicAndReachesOne()
        {
            var job  = new PapercraftJob(new[] { PapercraftTestMeshes.Cube24() }, TestOptions());
            float previous = 0f;

            foreach (object _ in job.Run())
            {
                Assert.GreaterOrEqual(job.Progress, previous, "progress must not go backwards");
                Assert.LessOrEqual(job.Progress, 1f);
                previous = job.Progress;
            }

            Assert.AreEqual(1f, job.Progress, 1e-6f);
            Assert.IsTrue(job.IsDone);
        }

        [Test]
        public void Job_StoppedEarly_LeavesNoResult()
        {
            var         job   = new PapercraftJob(new[] { PapercraftTestMeshes.Cube24() }, TestOptions());
            IEnumerator steps = job.Run().GetEnumerator();

            Assert.IsTrue(steps.MoveNext(), "job should produce at least one step");

            // Caller cancels by not enumerating further.
            Assert.IsFalse(job.IsDone);
            Assert.IsNull(job.Result);
        }

        [Test]
        public void Job_MultiMesh_ReportsEveryMeshInStatus()
        {
            var meshes = new[] { PapercraftTestMeshes.Cube24(), PapercraftTestMeshes.Cube24() };
            var job    = new PapercraftJob(meshes, TestOptions());

            bool sawSecondMesh = false;
            foreach (object _ in job.Run())
            {
                if (job.Status.Contains("2/2")) { sawSecondMesh = true; }
            }

            Assert.IsTrue(sawSecondMesh, "status should mention the second mesh while processing it");
        }
    }
}
