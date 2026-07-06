using UnityEngine;

namespace DiffuserCreator
{
    public class CameraLookAt : MonoBehaviour
    {
        [SerializeField]
        private Camera _camera;

        [SerializeField]
        private Transform _lookAtTarget;

        private void Update()
        {
            _camera.transform.LookAt(_lookAtTarget);
        }
    }
}
