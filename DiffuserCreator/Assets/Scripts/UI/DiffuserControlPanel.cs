using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DiffuserCreator.UI
{
    // Binds the DiffuserControlPanel.uxml controls to the grid's DiffuserSettings so the wall can be
    // tuned at runtime. Structural controls (grid/block size, spacing) regenerate; shaping controls
    // (depth source, curves, snap angle) only re-shape the existing blocks.
    //
    // Setup: Tools > DiffuserCreator > Create Control Panel wires the UIDocument (theme + UXML),
    // an EventSystem, and the grid reference.
    [RequireComponent(typeof(UIDocument))]
    public class DiffuserControlPanel : MonoBehaviour
    {
        [SerializeField]
        private DiffuserGrid _grid;

        // Bind in Start so the UIDocument has already built its visual tree (OnEnable order between
        // the two components on the same GameObject is not guaranteed).
        private void Start()
        {
            if (_grid == null)
            {
                _grid = FindObjectOfType<DiffuserGrid>();
            }

            if (_grid == null)
            {
                Debug.LogWarning("DiffuserControlPanel has no DiffuserGrid assigned and none was found in the scene.");
                return;
            }

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning("DiffuserControlPanel: the UIDocument has no root; is a source UXML assigned?");
                return;
            }

            DiffuserSettings settings = _grid.Settings;

            BindIntSlider(root, "rows", settings.Rows, value => { settings.Rows = value; _grid.Generate(); });
            BindIntSlider(root, "columns", settings.Columns, value => { settings.Columns = value; _grid.Generate(); });
            BindFloatSlider(root, "spacing-x", settings.HorizontalSpacing, value => { settings.HorizontalSpacing = value; _grid.Generate(); });
            BindFloatSlider(root, "spacing-y", settings.VerticalSpacing, value => { settings.VerticalSpacing = value; _grid.Generate(); });

            BindFloatSlider(root, "block-width", settings.BlockWidth, value => { settings.BlockWidth = value; _grid.Generate(); });
            BindFloatSlider(root, "block-height", settings.BlockHeight, value => { settings.BlockHeight = value; _grid.Generate(); });
            BindFloatSlider(root, "block-depth", settings.BlockDepth, value => { settings.BlockDepth = value; _grid.Generate(); });

            BindEnum<DepthSource>(root, "depth-source", settings.DepthSource, value => { settings.DepthSource = value; _grid.Reshape(); });
            BindEnum<HeightMode>(root, "height-mode", settings.HeightMode, value => { settings.HeightMode = value; _grid.Reshape(); });
            BindFloatSlider(root, "default-depth", settings.DefaultDepth, value => { settings.DefaultDepth = value; _grid.Reshape(); });

            BindEnum<CurveMode>(root, "curve-mode", settings.CurveMode, value => { settings.CurveMode = value; _grid.Reshape(); });
            BindToggle(root, "use-horizontal", settings.UseHorizontalCurve, value => { settings.UseHorizontalCurve = value; _grid.Reshape(); });
            BindToggle(root, "use-vertical", settings.UseVerticalCurve, value => { settings.UseVerticalCurve = value; _grid.Reshape(); });
            BindToggle(root, "use-diagonal", settings.UseDiagonalCurve, value => { settings.UseDiagonalCurve = value; _grid.Reshape(); });
            BindIntSlider(root, "snap-angle", settings.SnapAngle, value => { settings.SnapAngle = value; _grid.Reshape(); });

            BindButton(root, "regenerate", _grid.Generate);
            BindButton(root, "reshape", _grid.Reshape);
            BindButton(root, "print", _grid.PrintGrid);
        }

        #region Binding helpers

        private static void BindIntSlider(VisualElement root, string name, int value, Action<int> onChanged)
        {
            SliderInt slider = root.Q<SliderInt>(name);
            if (!Found(slider, name)) { return; }

            slider.value = value;
            slider.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        }

        private static void BindFloatSlider(VisualElement root, string name, float value, Action<float> onChanged)
        {
            Slider slider = root.Q<Slider>(name);
            if (!Found(slider, name)) { return; }

            slider.value = value;
            slider.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        }

        private static void BindToggle(VisualElement root, string name, bool value, Action<bool> onChanged)
        {
            Toggle toggle = root.Q<Toggle>(name);
            if (!Found(toggle, name)) { return; }

            toggle.value = value;
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        }

        private static void BindEnum<TEnum>(VisualElement root, string name, TEnum value, Action<TEnum> onChanged)
            where TEnum : struct, Enum
        {
            DropdownField dropdown = root.Q<DropdownField>(name);
            if (!Found(dropdown, name)) { return; }

            dropdown.choices = Enum.GetNames(typeof(TEnum)).ToList();
            dropdown.index   = Convert.ToInt32(value);
            dropdown.RegisterValueChangedCallback(_ =>
            {
                var selected = (TEnum)Enum.GetValues(typeof(TEnum)).GetValue(dropdown.index);
                onChanged(selected);
            });
        }

        private static void BindButton(VisualElement root, string name, Action onClick)
        {
            Button button = root.Q<Button>(name);
            if (!Found(button, name)) { return; }

            button.clicked += onClick;
        }

        private static bool Found(VisualElement element, string name)
        {
            if (element == null)
            {
                Debug.LogWarning($"DiffuserControlPanel: control '{name}' not found in the UXML.");
                return false;
            }

            return true;
        }

        #endregion
    }
}
