using System;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class DragDropManager<T>
    {
        private readonly Action<T, Vector2> drawDragged;
        private T dragged;
        private Vector2 dragOffset;

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
            if (dragged != null)
            {
                if (!Input.GetMouseButton(0))
                {
                    onDragStop(dragged);
                    dragOffset = Vector2.zero;
                    dragged = default;
                }

                drawDragged(dragged, UI.MousePositionOnUIInverted + dragOffset);
            }
        }
    }
}