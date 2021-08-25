using System;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class DragDropManager<T>
    {
        private readonly Action<T, Vector2> drawDragged;
        private T active;
        private T dragged;
        private Vector2 dragOffset;
        private Vector3 lastClickPos;

        public DragDropManager(Action<T, Vector2> drawDragged)
        {
            this.drawDragged = drawDragged;
        }

        public bool DraggingNow => dragged != null;

        public void StartDrag(T draggee, Vector2 topLeft)
        {
            dragged = draggee;
            dragOffset = topLeft - UI.MousePositionOnUIInverted;
        }

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
                StartDrag(draggee, rect.position);
                return true;
            }

            return false;
        }

        public void DropLocation(Rect rect, Action<T> onOver, Func<T, bool> onDrop)
        {
            if (Mouse.IsOver(rect) && dragged != null)
            {
                onOver(dragged);
                if (!Input.GetMouseButton(0) && onDrop(dragged))
                {
                    dragOffset = Vector2.zero;
                    dragged = default;
                }
            }
        }

        public void DragDropOnGUI(Action<T> onDragStop)
        {
            if (dragged == null) return;
            if (Input.GetMouseButton(0))
            {
                drawDragged(dragged, dragOffset + UI.MousePositionOnUIInverted);
            }
            else
            {
                onDragStop(dragged);
                dragOffset = Vector2.zero;
                dragged = default;
            }
        }
    }
}