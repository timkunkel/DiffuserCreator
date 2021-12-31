using UnityEngine;

public class DiffuserBlockSequence
{
    public enum SequenceOrientation
    {
        Horizontal,
        Vertical
    }
    
    private DiffuserBlock[]     _blocks;
    private SequenceOrientation _orientation;
    private AnimationCurve      _curve;

    public DiffuserBlockSequence(DiffuserBlock[] blocks, SequenceOrientation orientation, AnimationCurve curve)
    {
        _orientation = orientation;
        _blocks      = blocks;
        _curve       = curve;
    }

    public void SetCurve(AnimationCurve curve)
    {
        _curve = curve;
        foreach (DiffuserBlock block in _blocks)
        {
            block.SetCurve(curve, _orientation);
        }
    }
}
