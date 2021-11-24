using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class Dialog_ChooseIcon : Dialog_FloatMenuOptions
    {
        private readonly Action<string> selected;

        public Dialog_ChooseIcon(Action<string> onPath) : base(new List<FloatMenuOption>())
        {
            selected = onPath;
            options.AddRange(ArchitectModule.AllPossibleIcons().Select(path =>
                new FloatMenuOption(path, () => { selected(path); }, extraPartWidth: 20f, extraPartOnGUI: rect =>
                {
                    Widgets.DrawTextureFitted(new Rect(new Vector2(rect.x + 4f, rect.y + rect.height / 2 - 8), Vector2.one * 16f), ArchitectModule.LoadIcon(path), 1f);
                    return Widgets.ButtonInvisible(rect, false);
                })));
        }
    }
}