using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class Dialog_FloatMenuOptions : Window
    {
        private readonly HashSet<FloatMenuOption> dontShowItem = new();
        protected readonly List<FloatMenuOption> options;
        private readonly FloatMenuModule.CallInfo source;
        private Vector2 scrollPosition = new(0, 0);
        private string searchText = "";

        public Dialog_FloatMenuOptions(List<FloatMenuOption> opts)
        {
            options = opts;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            foreach (var option in opts)
            {
                var shown = option.shownItem;
                if (option.extraPartWidth <= 0f && option.extraPartOnGUI == null && shown != null)
                {
                    option.extraPartWidth = Widgets.InfoCardButtonSize + 7f;
                    option.extraPartOnGUI = rect =>
                    {
                        Widgets.InfoCardButton(rect.x + 7f, rect.y / 2 - Widgets.InfoCardButtonSize / 2, shown);
                        return false;
                    };
                    dontShowItem.Add(option);
                }
            }
        }

        public Dialog_FloatMenuOptions(List<FloatMenuOption> opts, FloatMenuModule.CallInfo caller) : this(opts) => source = caller;

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
            var shownOptions = options.Where(opt => opt.Label.ToLower().Contains(searchText.ToLower())).ToList();
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, shownOptions.Sum(opt => opt.RequiredHeight + 10f));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            try
            {
                var y = 0f;
                for (var i = 0; i < shownOptions.Count; i++)
                {
                    var opt = shownOptions[i];
                    var height = opt.RequiredHeight + 10f;
                    var rect2 = new Rect(0f, y, viewRect.width - 7f, height);
                    if (opt.shownItem is not null && !dontShowItem.Contains(opt))
                    {
                        rect2.xMax -= Widgets.InfoCardButtonSize + 7f;
                        Widgets.InfoCardButton(rect2.xMax + 7f, rect2.y / 2 - Widgets.InfoCardButtonSize / 2, opt.shownItem);
                    }

                    if (opt.DoGUI(rect2, false, null))
                    {
                        Close();
                        break;
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