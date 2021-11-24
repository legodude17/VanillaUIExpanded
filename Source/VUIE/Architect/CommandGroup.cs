using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class CommandGroup : Window
    {
        public readonly Vector2 Anchor;
        private readonly Func<bool> closeFunc;

        private readonly List<Command> elements;
        private readonly Func<Command, bool> highlightFunc;
        private readonly Func<Command, bool> lowlightFunc;
        private readonly Action<Command> onChosen;
        private readonly Action<Command> onMouseOver;
        public int Columns;
        public bool vanishIfMouseDistant;

        public CommandGroup(List<Command> gizmos, Vector2 anchor, Action<Command> onMouseOver = null, Action<Command> onChosen = null, Func<Command, bool> highlightFunc = null,
            Func<Command, bool> lowlightFunc = null, Func<bool> shouldClose = null)
        {
            elements = gizmos;
            Anchor = anchor;
            doCloseButton = false;
            doCloseX = false;
            drawShadow = false;
            preventCameraMotion = false;
            layer = WindowLayer.Super;
            this.onMouseOver = onMouseOver ?? (_ => { });
            this.onChosen = onChosen ?? (_ => { });
            this.highlightFunc = highlightFunc ?? (_ => false);
            this.lowlightFunc = lowlightFunc ?? (_ => false);
            closeFunc = shouldClose ?? (() => false);
        }

        public override Vector2 InitialSize => GizmoDrawer.GizmoAreaSize(elements.Cast<Gizmo>().ToList(), true, Columns);

        public override float Margin => GizmoGridDrawer.GizmoSpacing.x;

        public override void DoWindowContents(Rect inRect)
        {
            if (vanishIfMouseDistant &&
                GenUI.DistFromRect(new Rect(0, 0, InitialSize.x, InitialSize.y).ExpandedBy(Gizmo.Height * 2), Event.current.mousePosition) > 95f) Close();
            if (closeFunc()) Close(false);
            GizmoDrawer.DrawGizmos(elements, inRect, true, (gizmo, _) =>
            {
                onChosen((Command) gizmo);
                return false;
            }, gizmo => onMouseOver((Command) gizmo), gizmo => highlightFunc((Command) gizmo), gizmo => lowlightFunc((Command) gizmo), false);
        }

        public override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect(Anchor.x, Anchor.y - InitialSize.y, InitialSize.x, InitialSize.y);
        }
    }
}