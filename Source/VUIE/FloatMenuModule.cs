using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class FloatMenuModule : Module
    {
        public Dictionary<CallInfo, bool?> FloatMenuSettings = new();
        private Vector2 scrollPos;
        public bool ShowSwitchButtons = true;
        public bool UseGrid = true;
        public override string Label => "VUIE.FloatMenus".Translate();


        public static FloatMenuModule Instance => UIMod.GetModule<FloatMenuModule>();

        private static CallInfo GetKey()
        {
            return new StackTrace(false).GetFrames()?.Skip(3).First(frame => !SubclassOrEqual(frame.GetMethod().DeclaringType, typeof(FloatMenu)));
        }

        private static bool SubclassOrEqual(Type type1, Type type2) => type1 == type2 || type1.IsSubclassOf(type2);

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add)), new HarmonyMethod(typeof(FloatMenuModule), nameof(AddPrefix)));
            harm.Patch(AccessTools.Constructor(typeof(FloatMenu), new[] {typeof(List<FloatMenuOption>)}), new HarmonyMethod(typeof(FloatMenuModule), nameof(AddSwitchOption)));
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var outRect = new Rect(inRect.ContractedBy(15f));
            outRect.yMin += 10f;
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, FloatMenuSettings.Count * 150f + 50f);
            var listing = new Listing_Standard();
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            listing.Begin(viewRect);
            listing.CheckboxLabeled("VUIE.FloatMenus.ShowSwitch".Translate(), ref ShowSwitchButtons);
            listing.CheckboxLabeled("VUIE.FloatMenus.UseGrid".Translate(), ref UseGrid);
            listing.GapLine();
            foreach (var setting in FloatMenuSettings.ToList())
            {
                listing.Label(setting.Key);
                listing.Gap(4f);
                listing.ColumnWidth -= 12f;
                listing.Indent();
                if (listing.RadioButton("VUIE.FloatMenus.ForceDialog".Translate(), setting.Value.HasValue && setting.Value.Value)) FloatMenuSettings[setting.Key] = true;
                if (listing.RadioButton("VUIE.FloatMenus.ForceVanilla".Translate(), setting.Value.HasValue && !setting.Value.Value))
                    Instance.FloatMenuSettings[setting.Key] = false;
                if (listing.RadioButton("VUIE.FloatMenus.Default".Translate(), !setting.Value.HasValue)) FloatMenuSettings[setting.Key] = null;
                listing.ColumnWidth += 12f;
                listing.Outdent();
                listing.GapLine();
            }

            listing.End();
            Widgets.EndScrollView();
        }

        public static bool AddPrefix(WindowStack __instance, Window window)
        {
            if (window is FloatMenu menu)
            {
                var key = GetKey();
                if (!Instance.FloatMenuSettings.ContainsKey(key)) Instance.FloatMenuSettings.Add(key, null);
                var res = Instance.FloatMenuSettings[key];
                if (!res.HasValue && menu.options.Count > 30 || res.HasValue && res.Value)
                {
                    if (Instance.UseGrid && menu.options.All(opt => opt.shownItem is not null))
                        __instance.Add(new Dialog_FloatMenuGrid(menu.options, key));
                    else
                        __instance.Add(new Dialog_FloatMenuOptions(menu.options, key));
                    return false;
                }
            }

            return true;
        }

        public static void AddSwitchOption(List<FloatMenuOption> options)
        {
            var key = GetKey();
            if (Instance.ShowSwitchButtons &&
                !(Instance.FloatMenuSettings.ContainsKey(key) && Instance.FloatMenuSettings[key].HasValue && Instance.FloatMenuSettings[key].Value) &&
                key.MethodName != "TryMakeFloatMenu")
                options.Add(new FloatMenuOption("VUIE.FloatMenus.SwitchToFull".Translate(), () => Instance.FloatMenuSettings[key] = true));
        }

        public override void SaveSettings()
        {
            Scribe_Collections.Look(ref FloatMenuSettings, "floatMenu", LookMode.Deep, LookMode.Value);
            Scribe_Values.Look(ref ShowSwitchButtons, "showSwitch", true);
            FloatMenuSettings ??= new Dictionary<CallInfo, bool?>();
        }

        public struct CallInfo : IExposable
        {
            public string TypeName;
            public string Namespace;
            public string MethodName;

            public static implicit operator string(CallInfo info) => $"{info.Namespace}.{info.TypeName}.{info.MethodName}";

            public static implicit operator CallInfo(StackFrame frame) => new(frame);

            public void ExposeData()
            {
                Scribe_Values.Look(ref MethodName, "method");
                Scribe_Values.Look(ref TypeName, "type");
                Scribe_Values.Look(ref Namespace, "namespace");
            }

            public CallInfo(StackFrame frame)
            {
                var method = frame.GetMethod();
                MethodName = method.Name;
                TypeName = method.DeclaringType?.Name;
                Namespace = method.DeclaringType?.Namespace;
            }

            public bool Valid => !MethodName.NullOrEmpty() && !TypeName.NullOrEmpty();
        }
    }
}