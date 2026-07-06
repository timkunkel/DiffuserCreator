#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DiffuserCreator
{
    public class ObjExporterScript
    {
        private static int _startIndex;

        public static void Start()
        {
            _startIndex = 0;
        }

        public static void End()
        {
            _startIndex = 0;
        }

        public static string MeshToString(MeshFilter meshFilter, Transform transform)
        {
            Quaternion rotation = transform.localRotation;

            int  numVertices = 0;
            Mesh mesh        = meshFilter.sharedMesh;
            if (!mesh)
            {
                return "####Error####";
            }

            Material[] materials = meshFilter.GetComponent<Renderer>().sharedMaterials;

            var sb = new StringBuilder();

            foreach (Vector3 localVertex in mesh.vertices)
            {
                Vector3 worldVertex = transform.TransformPoint(localVertex);
                numVertices++;
                sb.Append(string.Format("v {0} {1} {2}\n", worldVertex.x, worldVertex.y, -worldVertex.z));
            }

            sb.Append("\n");
            foreach (Vector3 normal in mesh.normals)
            {
                Vector3 rotatedNormal = rotation * normal;
                sb.Append(string.Format("vn {0} {1} {2}\n", -rotatedNormal.x, -rotatedNormal.y, rotatedNormal.z));
            }

            sb.Append("\n");
            foreach (Vector3 uv in mesh.uv)
            {
                sb.Append(string.Format("vt {0} {1}\n", uv.x, uv.y));
            }

            for (int material = 0; material < mesh.subMeshCount; material++)
            {
                sb.Append("\n");
                sb.Append("usemtl ").Append(materials[material].name).Append("\n");
                sb.Append("usemap ").Append(materials[material].name).Append("\n");

                int[] triangles = mesh.GetTriangles(material);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
                                            triangles[i]     + 1 + _startIndex,
                                            triangles[i + 1] + 1 + _startIndex,
                                            triangles[i + 2] + 1 + _startIndex));
                }
            }

            _startIndex += numVertices;
            return sb.ToString();
        }
    }

    public class ObjExporter : ScriptableObject
    {
        [MenuItem("File/Export/Wavefront OBJ")]
        private static void DoExportWithSubmeshes()
        {
            DoExport(true);
        }

        [MenuItem("File/Export/Wavefront OBJ (No Submeshes)")]
        private static void DoExportWithoutSubmeshes()
        {
            DoExport(false);
        }

        public static void DoExport(bool makeSubmeshes)
        {
            if (Selection.gameObjects.Length == 0)
            {
                Debug.Log("Didn't Export Any Meshes; Nothing was selected!");
                return;
            }

            string meshName = Selection.gameObjects[0].name;
            string fileName = EditorUtility.SaveFilePanel("Export .obj file", "", meshName, "obj");

            ObjExporterScript.Start();

            var meshString = new StringBuilder();
            meshString.Append("#" + meshName + ".obj"
                              + "\n#" + System.DateTime.Now.ToLongDateString()
                              + "\n#" + System.DateTime.Now.ToLongTimeString()
                              + "\n#-------"
                              + "\n\n");

            Transform transform        = Selection.gameObjects[0].transform;
            Vector3   originalPosition = transform.position;
            transform.position = Vector3.zero;

            if (!makeSubmeshes)
            {
                meshString.Append("g ").Append(transform.name).Append("\n");
            }

            meshString.Append(ProcessTransform(transform, makeSubmeshes));

            WriteToFile(meshString.ToString(), fileName);

            transform.position = originalPosition;

            ObjExporterScript.End();
            Debug.Log("Exported Mesh: " + fileName);
        }

        private static string ProcessTransform(Transform transform, bool makeSubmeshes)
        {
            var meshString = new StringBuilder();
            meshString.Append("#" + transform.name + "\n#-------\n");

            if (makeSubmeshes)
            {
                meshString.Append("g ").Append(transform.name).Append("\n");
            }

            MeshFilter meshFilter = transform.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshString.Append(ObjExporterScript.MeshToString(meshFilter, transform));
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                meshString.Append(ProcessTransform(transform.GetChild(i), makeSubmeshes));
            }

            return meshString.ToString();
        }

        private static void WriteToFile(string content, string fileName)
        {
            using var writer = new StreamWriter(fileName);
            writer.Write(content);
        }
    }
}
#endif
