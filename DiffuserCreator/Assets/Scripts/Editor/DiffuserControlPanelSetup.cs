#if UNITY_EDITOR
using DiffuserCreator.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DiffuserCreator.EditorTools
{
    // One-click scene setup for the runtime control panel: creates a GameObject with a UIDocument
    // (plus a PanelSettings asset if the project has none) and wires it to the scene's DiffuserGrid.
    public static class DiffuserControlPanelSetup
    {
        [MenuItem("Tools/DiffuserCreator/Create Control Panel")]
        private static void CreateControlPanel()
        {
            var grid = Object.FindObjectOfType<DiffuserGrid>();
            if (grid == null)
            {
                EditorUtility.DisplayDialog("DiffuserCreator",
                                            "No DiffuserGrid found in the open scene. Add one first.", "OK");
                return;
            }

            PanelSettings panelSettings = FindOrCreatePanelSettings();

            var gameObject = new GameObject("Diffuser Control Panel");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Diffuser Control Panel");

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            DiffuserControlPanel panel = gameObject.AddComponent<DiffuserControlPanel>();

            var serialized = new SerializedObject(panel);
            serialized.FindProperty("_grid").objectReferenceValue = grid;
            serialized.ApplyModifiedProperties();

            Selection.activeGameObject = gameObject;
            Debug.Log("Created Diffuser Control Panel. Press Play to use it.");
        }

        private static PanelSettings FindOrCreatePanelSettings()
        {
            string[] existing = AssetDatabase.FindAssets("t:PanelSettings");
            if (existing.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(existing[0]));
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            string[] themes = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            if (themes.Length > 0)
            {
                panelSettings.themeStyleSheet =
                    AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themes[0]));
            }
            else
            {
                Debug.LogWarning("No ThemeStyleSheet found. Create one via Assets > Create > UI Toolkit > "
                                 + "Panel Settings Asset (it generates a default runtime theme) and assign it "
                                 + "to the Diffuser Control Panel's UIDocument.");
            }

            if (!AssetDatabase.IsValidFolder("Assets/UI"))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }

            AssetDatabase.CreateAsset(panelSettings, "Assets/UI/DiffuserPanelSettings.asset");
            AssetDatabase.SaveAssets();
            return panelSettings;
        }
    }
}
#endif
