#if UNITY_EDITOR
using System.IO;
using DiffuserCreator.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace DiffuserCreator.EditorTools
{
    // One-click, idempotent setup/repair for the runtime control panel. Ensures a GameObject with a
    // UIDocument that has a working theme + the DiffuserControlPanel.uxml, an EventSystem for input,
    // and a wired DiffuserGrid. Safe to re-run.
    public static class DiffuserControlPanelSetup
    {
        private const string UI_FOLDER      = "Assets/UI";
        private const string THEME_PATH     = "Assets/UI/DiffuserRuntimeTheme.tss";
        private const string UXML_PATH      = "Assets/UI/DiffuserControlPanel.uxml";
        private const string SETTINGS_PATH  = "Assets/UI/DiffuserPanelSettings.asset";
        private const string THEME_CONTENTS = "@import url(\"unity-theme://default\");\nVisualElement {}\n";

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
            panelSettings.themeStyleSheet = FindOrCreateTheme();
            EditorUtility.SetDirty(panelSettings);

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            if (visualTree == null)
            {
                Debug.LogWarning($"DiffuserControlPanel: could not find {UXML_PATH}.");
            }

            DiffuserControlPanel panel = Object.FindObjectOfType<DiffuserControlPanel>();
            if (panel == null)
            {
                var gameObject = new GameObject("Diffuser Control Panel");
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Diffuser Control Panel");
                panel = gameObject.AddComponent<DiffuserControlPanel>();
            }

            var document = panel.GetComponent<UIDocument>();
            if (document == null)
            {
                document = panel.gameObject.AddComponent<UIDocument>();
            }

            document.panelSettings  = panelSettings;
            document.visualTreeAsset = visualTree;

            var serialized = new SerializedObject(panel);
            serialized.FindProperty("_grid").objectReferenceValue = grid;
            serialized.ApplyModifiedProperties();

            EnsureEventSystem();

            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);

            Selection.activeGameObject = panel.gameObject;
            Debug.Log("Diffuser Control Panel is set up. Save the scene, then press Play.");
        }

        private static PanelSettings FindOrCreatePanelSettings()
        {
            string[] existing = AssetDatabase.FindAssets("t:PanelSettings");
            if (existing.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(existing[0]));
            }

            EnsureUIFolder();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panelSettings, SETTINGS_PATH);
            AssetDatabase.SaveAssets();
            return panelSettings;
        }

        // Always uses our own theme (which imports the default runtime styles). This overrides any
        // incomplete theme a user may have hand-created and assigned.
        private static ThemeStyleSheet FindOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(THEME_PATH);
            if (theme != null) { return theme; }

            EnsureUIFolder();
            File.WriteAllText(THEME_PATH, THEME_CONTENTS);
            AssetDatabase.ImportAsset(THEME_PATH);
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(THEME_PATH);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) { return; }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        private static void EnsureUIFolder()
        {
            if (!AssetDatabase.IsValidFolder(UI_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }
        }
    }
}
#endif
