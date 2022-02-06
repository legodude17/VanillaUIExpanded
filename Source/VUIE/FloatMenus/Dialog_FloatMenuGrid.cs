using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class Dialog_FloatMenuGrid : Window
    {
        private readonly List<Command_FloatMenuOption> commands;
        private readonly FloatMenuModule.CallInfo source;
        private Vector2 scrollPosition = new(0, 0);
        private string searchText = "";

        public Dialog_FloatMenuGrid(IEnumerable<FloatMenuOption> opts)
        {
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            commands = new List<Command_FloatMenuOption>();
            foreach (var option in opts)
            {
                var shown = option.shownItem;
                if (shown == null) throw new ArgumentException("Dialog_FloatMenuGrid needs every option to have a shownItem");
                commands.Add(new Command_FloatMenuOption(option, opt =>
                {
                    opt.Chosen(false, null);
                    Close();
                }));
            }
        }

        public Dialog_FloatMenuGrid(IEnumerable<FloatMenuOption> opts, FloatMenuModule.CallInfo caller) : this(opts) => source = caller;

        public override Vector2 InitialSize => new(620f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            var outRect = new Rect(inRect);
            outRect.yMin += 20f;
            outRect.yMax -= 60f;
            outRect.width -= 16f;
            searchText = Widgets.TextField(outRect.TopPartPixels(35f), searchText);
            outRect.yMin += 40f;
            var shown = commands.Where(c => c.Label.ToLower().Contains(searchText.ToLower())).ToList();
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.CeilToInt(shown.Count / 6f) * (Gizmo.Height + GizmoGridDrawer.GizmoSpacing.y + 10f));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            try
            {
                GizmoDrawer.DrawGizmos(shown, viewRect.ContractedBy(7f), additionalSpacing: new Vector2(10f, 10f));
            }
            finally
            {
                Widgets.EndScrollView();
            }

            Text.Font = GameFont.Small;
            if (source.Valid && FloatMenuModule.Instance.ShowSwitchButtons)
            {
                if (Widgets.ButtonText(new Rect(inRect.width / 2f + 10f, inRect.height - 55f, CloseButSize.x, CloseButSize.y),
                    "CloseButton".Translate()))
                    Close();
                if (Widgets.ButtonText(new Rect(inRect.width / 2f - 10f - CloseButSize.x, inRect.height - 55f, CloseButSize.x, CloseButSize.y), "VUIE.ToVanilla".Translate()))
                {
                    FloatMenuModule.Instance.FloatMenuSettings[source] = false;
                    Close();
                }
            }
            else
            {
                if (Widgets.ButtonText(new Rect(inRect.width / 2f - CloseButSize.x / 2f, inRect.height - 55f, CloseButSize.x, CloseButSize.y),
                    "CloseButton".Translate()))
                    Close();
            }
        }

        public class Command_FloatMenuOption : Command
        {
            private readonly Action<FloatMenuOption> chosen;
            private readonly FloatMenuOption option;

            public Command_FloatMenuOption(FloatMenuOption option, Action<FloatMenuOption> chosen)
            {
                this.option = option;
                this.chosen = chosen;
                defaultLabel = option.Label;
            }

            public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
            {
                Widgets.ThingIcon(rect, option.shownItem, null, option.thingStyle ?? Faction.OfPlayer.ideos?.PrimaryIdeo?.GetStyleFor(option.shownItem) ?? null);
            }

            public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
            {
                var result = base.GizmoOnGUI(topLeft, maxWidth, parms);
                if (result.State == GizmoState.Interacted) chosen(option);
                return result;
            }
        }
    }
}