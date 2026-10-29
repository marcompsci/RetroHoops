using UnityEngine;
using UnityEngine.EventSystems;

namespace CallerRetroBall.City
{
    /// <summary>28 Oct 2026: passes the WORLD MAP viewport's touches, drags and scrolls to its <see cref="WorldMapView"/>.</summary>
    public sealed class WorldMapGestures : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        public WorldMapView Target;
        public void OnPointerDown(PointerEventData e) { if (Target != null) Target.OnPointerDown(e); }
        public void OnPointerUp(PointerEventData e) { if (Target != null) Target.OnPointerUp(e); }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { if (Target != null) Target.OnDrag(e); }
        public void OnScroll(PointerEventData e) { if (Target != null) Target.OnScroll(e); }
    }
}
