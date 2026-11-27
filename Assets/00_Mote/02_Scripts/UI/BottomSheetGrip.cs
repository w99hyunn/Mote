using UnityEngine;
using UnityEngine.EventSystems;

namespace DoHwa.UI
{
    public sealed class BottomSheetGrip : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform motionLayer;
        [SerializeField] private RectTransform grip;
        [SerializeField] private Animator panelAnimator;
        private static readonly int Held = Animator.StringToHash("Held");
        private bool dragging;
        // Distances are measured in canvas units so the pull feels consistent across resolutions.
        private const float ResistanceDistance = 48f;
        private const float InitialFollow = .7f;
        private RectTransform coordinateSpace;
        private Vector2 restingPosition;
        private Vector2 displacement, dragOrigin, velocity;
        private int? pointer;

        private void Awake()
        {
            coordinateSpace = (RectTransform)motionLayer.parent;
            restingPosition = motionLayer.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer.HasValue || e.button != PointerEventData.InputButton.Left ||
                !LocalPoint(e.pressPosition, e, out var start)) return;

            pointer = e.pointerId;
            velocity = Vector2.zero;
            // Recover the equivalent finger travel when a returning sheet is caught.
            float travel = ResistanceDistance * (Mathf.Exp(displacement.magnitude / ResistanceDistance) - 1f) / InitialFollow;
            dragOrigin = start - displacement.normalized * travel;
            panelAnimator.SetBool(Held, true);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (pointer != e.pointerId || e.pointerPressRaycast.gameObject != grip.gameObject) return;
            dragging = true;
            e.eligibleForClick = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging || pointer != e.pointerId || !LocalPoint(e.position, e, out var position)) return;
            var pull = position - dragOrigin;
            float distance = ResistanceDistance * Mathf.Log(1f + InitialFollow * pull.magnitude / ResistanceDistance);
            Move(pull.normalized * distance);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (pointer == e.pointerId) Release();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (pointer == e.pointerId) Release();
        }

        private void Release()
        {
            pointer = null;
            dragging = false;
            panelAnimator.SetBool(Held, false);
        }

        private bool LocalPoint(Vector2 screen, PointerEventData e, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(coordinateSpace, screen, e.pressEventCamera, out local);

        private void LateUpdate()
        {
            if (pointer.HasValue || displacement == Vector2.zero) return;
            var next = Vector2.SmoothDamp(displacement, Vector2.zero, ref velocity, .105f,
                Mathf.Infinity, Time.unscaledDeltaTime);
            Move(next.sqrMagnitude < .08f * .08f ? Vector2.zero : next);
        }

        private void Move(Vector2 offset)
        {
            displacement = offset;
            motionLayer.anchoredPosition = restingPosition + offset;
        }

        private void OnDisable()
        {
            pointer = null;
            dragging = false;
            velocity = Vector2.zero;
            if (motionLayer && displacement != Vector2.zero) Move(Vector2.zero);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && pointer.HasValue) Release();
        }
    }
}
