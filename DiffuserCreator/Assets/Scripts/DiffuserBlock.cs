using UnityEngine;

public class DiffuserBlock : MonoBehaviour
{
    public float Width  => transform.localScale.x;
    public float Height => transform.localScale.y;
    public float Depth  => transform.localScale.z;

    public Vector2 BottomCenter => new Vector2(Width/2, Height/2);
   
    public void SetSize(float width, float height, float depth)
    {
        transform.localScale = new Vector3(width, height, depth);
    }
}