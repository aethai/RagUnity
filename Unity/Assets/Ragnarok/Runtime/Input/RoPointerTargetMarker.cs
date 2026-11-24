using UnityEngine;

namespace Ragnarok.Input {
    public sealed class RoPointerTargetMarker : MonoBehaviour {
        [SerializeField] private RoPointerTargetKind kind = RoPointerTargetKind.Player;
        [SerializeField] private bool blocksGroundClick = true;
        [SerializeField] private string label;

        public RoPointerTargetKind Kind {
            get => kind;
            set => kind = value;
        }

        public bool BlocksGroundClick {
            get => blocksGroundClick;
            set => blocksGroundClick = value;
        }

        public string Label {
            get => label;
            set => label = value;
        }

        public RoPointerTarget BuildTarget(RaycastHit hit, RoPointerModifierFlags modifiers) {
            Vector3 position = hit.collider != null ? hit.point : transform.position;
            return RoPointerTarget.WorldTarget(
                kind,
                position,
                gameObject,
                modifiers,
                blocksGroundClick,
                string.IsNullOrWhiteSpace(label) ? gameObject.name : label
            );
        }
    }
}
