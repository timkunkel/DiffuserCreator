using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DiffuserCreator.UI
{
    // Runtime UI Toolkit panel that exposes every DiffuserGrid setting that previously had to be
    // edited in the inspector. Structural changes (grid/block size, spacing) regenerate the wall;
    // shaping changes (depth source, curves, snap angle) only re-shape the existing blocks.
    //
    // Setup: add this component to a GameObject that also has a UIDocument with a PanelSettings
    // assigned, then assign the DiffuserGrid. (Tools > DiffuserCreator > Create Control Panel wires
    // this up automatically in the editor.)
    [RequireComponent(typeof(UIDocument))]
    public class DiffuserControlPanel : MonoBehaviour
    {
        [SerializeField]
        private DiffuserGrid _grid;

        private void OnEnable()
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
            root.Clear();
            root.Add(BuildPanel());
        }

        #region Panel construction

        private VisualElement BuildPanel()
        {
            var panel = new VisualElement();
            StylePanel(panel.style);

            panel.Add(Title("Diffuser Controls"));

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            panel.Add(scroll);

            scroll.Add(Section("Grid"));
            scroll.Add(IntSlider("Rows", _grid.Rows, 1, 30, v => { _grid.Rows = v; _grid.Generate(); }));
            scroll.Add(IntSlider("Columns", _grid.Columns, 1, 30, v => { _grid.Columns = v; _grid.Generate(); }));
            scroll.Add(FloatSlider("Spacing X", _grid.HorizontalSpacing, 0f, 2f, v => { _grid.HorizontalSpacing = v; _grid.Generate(); }));
            scroll.Add(FloatSlider("Spacing Y", _grid.VerticalSpacing, 0f, 2f, v => { _grid.VerticalSpacing = v; _grid.Generate(); }));

            scroll.Add(Section("Block"));
            scroll.Add(FloatSlider("Width", _grid.BlockWidth, 0.1f, 3f, v => { _grid.BlockWidth = v; _grid.Generate(); }));
            scroll.Add(FloatSlider("Height", _grid.BlockHeight, 0.1f, 3f, v => { _grid.BlockHeight = v; _grid.Generate(); }));
            scroll.Add(FloatSlider("Depth", _grid.BlockDepth, 0.1f, 3f, v => { _grid.BlockDepth = v; _grid.Generate(); }));

            scroll.Add(Section("Depth"));
            scroll.Add(EnumDropdown("Source", _grid.DepthSource, e => { _grid.DepthSource = (DepthSource)e; _grid.Reshape(); }));
            scroll.Add(EnumDropdown("Height mode", _grid.HeightMode, e => { _grid.HeightMode = (HeightMode)e; _grid.Reshape(); }));
            scroll.Add(FloatSlider("Default depth", _grid.DefaultDepth, 0f, 3f, v => { _grid.DefaultDepth = v; _grid.Reshape(); }));

            scroll.Add(Section("Curve"));
            scroll.Add(EnumDropdown("Curve mode", _grid.CurveMode, e => { _grid.CurveMode = (CurveMode)e; _grid.Reshape(); }));
            scroll.Add(BoolToggle("Use horizontal", _grid.UseHorizontalCurve, v => { _grid.UseHorizontalCurve = v; _grid.Reshape(); }));
            scroll.Add(BoolToggle("Use vertical", _grid.UseVerticalCurve, v => { _grid.UseVerticalCurve = v; _grid.Reshape(); }));
            scroll.Add(BoolToggle("Use diagonal", _grid.UseDiagonalCurve, v => { _grid.UseDiagonalCurve = v; _grid.Reshape(); }));
            scroll.Add(IntSlider("Snap angle", _grid.SnapAngle, 1, 90, v => { _grid.SnapAngle = v; _grid.Reshape(); }));
            scroll.Add(Hint("Curve shapes are edited on the DiffuserGrid component in the inspector."));

            scroll.Add(Section("Actions"));
            scroll.Add(ActionButton("Regenerate", _grid.Generate));
            scroll.Add(ActionButton("Reshape", _grid.Reshape));
            scroll.Add(ActionButton("Print grid", _grid.PrintGrid));

            return panel;
        }

        #endregion

        #region Control factories

        private static Label Title(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize                = 16;
            label.style.marginBottom            = 8;
            label.style.color                   = Color.white;
            return label;
        }

        private static Label Section(string text)
        {
            var label = new Label(text.ToUpperInvariant());
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize                = 11;
            label.style.marginTop               = 10;
            label.style.marginBottom            = 2;
            label.style.color                   = new Color(0.6f, 0.75f, 1f);
            return label;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace   = WhiteSpace.Normal;
            label.style.fontSize     = 10;
            label.style.marginTop    = 2;
            label.style.color        = new Color(0.7f, 0.7f, 0.7f);
            return label;
        }

        private static SliderInt IntSlider(string label, int value, int min, int max, Action<int> onChanged)
        {
            var slider = new SliderInt(label, min, max) { value = value, showInputField = true };
            slider.style.marginTop = 2;
            slider.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return slider;
        }

        private static Slider FloatSlider(string label, float value, float min, float max, Action<float> onChanged)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true };
            slider.style.marginTop = 2;
            slider.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return slider;
        }

        private static Toggle BoolToggle(string label, bool value, Action<bool> onChanged)
        {
            var toggle = new Toggle(label) { value = value };
            toggle.style.marginTop = 2;
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return toggle;
        }

        private static EnumField EnumDropdown(string label, Enum value, Action<Enum> onChanged)
        {
            var field = new EnumField(label, value);
            field.style.marginTop = 2;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return field;
        }

        private static Button ActionButton(string label, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.style.marginTop  = 4;
            button.style.height     = 26;
            return button;
        }

        private static void StylePanel(IStyle style)
        {
            style.position        = Position.Absolute;
            style.top             = 12;
            style.right           = 12;
            style.width           = 320;
            style.maxHeight       = Length.Percent(92);
            style.paddingLeft     = 12;
            style.paddingRight    = 12;
            style.paddingTop      = 12;
            style.paddingBottom   = 12;
            style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.9f);
            style.borderTopLeftRadius     = 8;
            style.borderTopRightRadius    = 8;
            style.borderBottomLeftRadius  = 8;
            style.borderBottomRightRadius = 8;
        }

        #endregion
    }
}
