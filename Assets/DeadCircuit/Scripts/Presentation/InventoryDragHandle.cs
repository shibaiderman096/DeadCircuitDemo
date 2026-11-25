using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeadCircuit
{
    /// <summary>Forwards pointer events without owning, moving, or selecting inventory state.</summary>
    [DisallowMultipleComponent]
    public sealed class InventoryDragHandle : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<PointerEventData, bool> begin;
        public Action<PointerEventData> drag, end, drop, enter, exit;
        public Action tick, disabled;
        bool dragging;
        UnityEngine.UI.ScrollRect scrollDrag;

        public void OnInitializePotentialDrag(PointerEventData data)
        {
            if (begin == null && GetComponent<UnityEngine.UI.ScrollRect>() == null)
                GetComponentInParent<UnityEngine.UI.ScrollRect>()?.OnInitializePotentialDrag(data);
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            dragging = begin != null && begin(data);
            if (dragging) data.eligibleForClick = false;
            else
            {
                scrollDrag = GetComponent<UnityEngine.UI.ScrollRect>() == null ? GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
                scrollDrag?.OnBeginDrag(data);
            }
        }

        public void OnDrag(PointerEventData data)
        {
            if (scrollDrag != null) { scrollDrag.OnDrag(data); return; }
            if (!dragging) return;
            data.eligibleForClick = false;
            drag?.Invoke(data);
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (scrollDrag != null) { scrollDrag.OnEndDrag(data); scrollDrag = null; return; }
            if (!dragging) return;
            dragging = false;
            data.eligibleForClick = false;
            end?.Invoke(data);
        }

        public void OnDrop(PointerEventData data) => drop?.Invoke(data);
        public void OnPointerEnter(PointerEventData data) => enter?.Invoke(data);
        public void OnPointerExit(PointerEventData data) => exit?.Invoke(data);
        void LateUpdate() => tick?.Invoke();
        void OnDisable() { dragging = false; scrollDrag = null; disabled?.Invoke(); }
        void OnApplicationFocus(bool focus) { if (!focus) disabled?.Invoke(); }
        void OnApplicationPause(bool paused) { if (paused) disabled?.Invoke(); }
    }
}
