using System;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class DragDropManager<T>
    {
        private readonly Action<T, Vector2> drawDragged;
        private T active;
        private Vector2 dragOffset;
        private Vector3 lastClickPos;

        public DragDropManager(Action<T, Vector2> drawDragged) => this.drawDragged = drawDragged;

        public bool DraggingNow => Dragging != null;
        public T Dragging { get; private set; }

        public bool TryStartDrag(T draggee, Rect rect)
        {
            if (DraggingNow) return false;
            if (Mouse.IsOver(rect) && Input.GetMouseButtonDown(0))
            {
                lastClickPos = Input.mousePosition;
                active = draggee;
            }

            if (Input.GetMouseButtonUp(0)) active = default;

            if (Input.GetMouseButton(0) && (lastClickPos - Input.mousePosition).sqrMagnitude > Widgets.DragStartDistanceSquared && Equals(draggee, active))
            {
                Dragging = draggee;
                dragOffset = rect.position - UI.MousePositionOnUIInverted;
                return true;
            }

            return false;
        }

        public void DropLocation(Rect rect, Action<T> onOver, Func<T, bool> onDrop)
        {
            if (Mouse.IsOver(rect) && Dragging != null)
            {
                onOver?.Invoke(Dragging);
                if (!Input.GetMouseButton(0) && onDrop(Dragging))
                {
                    dragOffset = Vector2.zero;
                    Dragging = default;
                }
            }
        }

        public void DragDropOnGUI(Action<T> onDragStop)
        {
            if (Dragging == null) return;
            if (Input.GetMouseButton(0))
                drawDragged(Dragging, dragOffset + UI.MousePositionOnUIInverted);
            else
            {
                onDragStop(Dragging);
                dragOffset = Vector2.zero;
                Dragging = default;
            }
        }
    }
}