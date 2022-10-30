using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class Dialog_FloatMenuOptions : Window
    {
        private readonly HashSet<FloatMenuOption> dontShowItem = new();
        private readonly HashSet<FloatMenuOption> closedSubMenus = new();
        protected readonly List<FloatMenuOption> options;
        private readonly FloatMenuModule.CallInfo source;
        private Vector2 scrollPosition = new(0, 0);
        private string searchText = "";

        private static readonly Color arrowColor = new Color(0.6f, 0.6f, 0.6f);

        public Dialog_FloatMenuOptions(IEnumerable<FloatMenuOption> opts)
        {
            options = opts.ToList();
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            foreach (var option in options)
            {
                var shown = option.shownItem;
                if (option.extraPartWidth <= 0f && option.extraPartOnGUI == null && shown != null)
                {
                    option.extraPartWidth = Widgets.InfoCardButtonSize + 7f;
                    option.extraPartOnGUI = rect =>
                    {
                        Widgets.InfoCardButton(rect.x + 7f, rect.y / 2 - Widgets.InfoCardButtonSize / 2 + 5f, shown);
                        return false;
                    };
                    dontShowItem.Add(option);
                }
                else if (option.extraPartOnGUI is not null && PatchProcessor.GetCurrentInstructions(option.extraPartOnGUI.Method).Any(ins =>
                    ins.opcode == OpCodes.Call && ins.operand is MethodInfo {DeclaringType: var type, Name: var name} && type == typeof(Widgets) &&
                    name == nameof(Widgets.InfoCardButton)))
                    dontShowItem.Add(option);
            }
        }

        public Dialog_FloatMenuOptions(IEnumerable<FloatMenuOption> opts, FloatMenuModule.CallInfo caller) : this(opts) => source = caller;

        public override Vector2 InitialSize => new(620f, 500f);

        private List<(FloatMenuOption opt, int indent)> ShownOptions() {
            var shown = new List<(FloatMenuOption opt, int indent)>();
            BuildShownOptions(shown, options, false, 0);
            return shown;
        }

        private void BuildShownOptions(List<(FloatMenuOption opt, int indent)> shown, List<FloatMenuOption> options, bool noFilter, int indent) {
            foreach (var opt in options) {
                bool match = noFilter || opt.Label.ToLower().Contains(searchText.ToLower());
                if (ModCompatModule.IsSubMenuOption(opt) && !closedSubMenus.Contains(opt)) {
                    shown.Add((opt, indent));
                    int n = shown.Count;
                    BuildShownOptions(shown, ModCompatModule.SubMenuOptions(opt), match, indent + 1);
                    if (n == shown.Count) {
                        shown.RemoveAt(n - 1);
                    }
                } else if (match) {
                    shown.Add((opt, indent));
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            var outRect = new Rect(inRect);
            outRect.yMin += 20f;
            outRect.yMax -= 60f;
            outRect.width -= 16f;
            var searchRect = outRect.TopPartPixels(35f);
            searchRect.xMin += 12f;
            searchText = Widgets.TextField(searchRect, searchText);
            outRect.yMin += 40f;
            var shownOptions = ShownOptions();
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, shownOptions.Sum(so => so.opt.RequiredHeight + 17f));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            try
            {
                var y = 0f;
                foreach (var (opt, indent) in shownOptions)
                {
                    var height = opt.RequiredHeight + 10f;
                    var rect2 = new Rect(12f, y, viewRect.width - 7f - 12f, height);
                    rect2.xMin += indent * 18f;
                    if (opt.shownItem is not null && !dontShowItem.Contains(opt))
                    {
                        rect2.xMax -= Widgets.InfoCardButtonSize + 7f;
                        Widgets.InfoCardButton(rect2.xMax + 7f, rect2.y / 2 - Widgets.InfoCardButtonSize / 2, opt.shownItem);
                    }

                    bool isSubMenu = ModCompatModule.IsSubMenuOption(opt);
                    if (isSubMenu) {
                        Text.Anchor = TextAnchor.MiddleLeft;
                        GUI.color = arrowColor;
                        var arrowRect = new Rect(rect2.x - 12f, rect2.y, 12f, rect2.height);
                        var arrowText = closedSubMenus.Contains(opt) ? ">" : "v";
                        Widgets.Label(arrowRect, arrowText);
                        GUI.color = Color.white;
                    }

                    if (opt.DoGUI(rect2, false, null))
                    {
                        if (isSubMenu) {
                            if (closedSubMenus.Contains(opt)) closedSubMenus.Remove(opt);
                            else closedSubMenus.Add(opt);
                        } else {
                            Close();
                            break;
                        }
                    }

                    GUI.color = Color.white;
                    y += height + 7f;
                }
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
    }
}