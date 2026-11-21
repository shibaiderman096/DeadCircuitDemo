using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeadCircuit
{
    /// <summary>Two-axis panning with cursor-centred wheel zoom, using the existing UI input module.</summary>
    public sealed class TechnologyTreeViewport : UnityEngine.UI.ScrollRect
    {
        public Action<float, Vector2, Camera> zoom;
        public override void OnScroll(PointerEventData eventData)
        {
            if (zoom == null || !IsActive()) return;
            zoom(eventData.scrollDelta.y, eventData.position, eventData.enterEventCamera);
            eventData.Use();
        }
    }
}
