using UnityEngine;

namespace Ragnarok.Entities {
    public sealed class RoPlayerPlaceholder : MonoBehaviour {
        [SerializeField] private float height = 3.2f;
        [SerializeField] private float radius = 0.7f;

        private void OnDrawGizmos() {
            Gizmos.color = new Color(1f, 0.25f, 0.15f, 1f);
            Vector3 bottom = transform.position;
            Vector3 top = transform.position + Vector3.up * height;
            Gizmos.DrawLine(bottom, top);
            Gizmos.DrawWireSphere(top, radius);
        }
    }
}
