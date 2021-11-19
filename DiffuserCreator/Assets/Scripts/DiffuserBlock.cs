using UnityEngine;

public class DiffuserBlock : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetSize(float width, float height, float depth)
    {
        transform.localScale = new Vector3(width, height, depth);
    }
}
