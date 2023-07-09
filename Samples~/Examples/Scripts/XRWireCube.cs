using UnityEngine;

namespace com.darktable.utility.xrgizmos.example
{
    public class XRWireCube : MonoBehaviour
    {
        [SerializeField] private Color color = Color.red;

        [SerializeField] [Range(0.001f, 0.3f)] private float thickness = 0.01f;

        private Transform _transform;

        private void Awake()
        {
            _transform = transform;
        }

        private void Update()
        {
            XRGizmos.DrawWireCube(_transform.position, _transform.rotation, _transform.lossyScale, color, thickness);
        }
    }
}