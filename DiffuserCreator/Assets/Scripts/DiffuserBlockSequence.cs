using UnityEngine;

public class DiffuserBlockSequence
{
    public enum SequenceOrientation
    {
        Horizontal, Vertical
    }

    private          DiffuserBlock[]        _blocks;
    private          SequenceOrientation    _orientation;
    private          AnimationCurve         _curve;
    private readonly DiffuserGrid.CurveMode _curveMode;

    public DiffuserBlockSequence(
        DiffuserBlock[] blocks, SequenceOrientation orientation, AnimationCurve curve, DiffuserGrid.CurveMode curveMode)
    {
        _orientation = orientation;
        _blocks      = blocks;
        _curve       = curve;
        _curveMode   = curveMode;
    }

    public void SetCurve(AnimationCurve curve)
    {
        _curve = curve;
        foreach (DiffuserBlock block in _blocks)
        {
            block.SetCurve(curve, _orientation, _curveMode);
        }
    }
}