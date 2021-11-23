using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class UIMod : Mod
    {
        public static UISettings Settings;
        private static List<Module> modules;
        public static Harmony Harm;

        private Module curModule;
        private List<TabRecord> tabs;

        public UIMod(ModContentPack content) : base(content)
        {
            Harm = new Harmony("vanillaexpanded.ui");
            modules = typeof(Module).AllSubclassesNonAbstract().Select(type => (Module) Activator.CreateInstance(type)).ToList();
            curModule = modules.First(mod => !mod.LabelKey.NullOrEmpty());
            LongEventHandler.ExecuteWhenFinished(() => Settings = GetSettings<UISettings>());
            foreach (var module in modules) module.DoPatches(Harm);
        }

        public static IEnumerable<Module> AllModules => modules;

        public static T GetModule<T>() where T : Module => modules.OfType<T>().First();

        public override string SettingsCategory() => "VUIE".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            tabs ??= modules.Where(mod => !mod.LabelKey.NullOrEmpty()).Select(mod => new TabRecord(mod.LabelKey.Translate(), () => curModule = mod, curModule == mod)).ToList();
            inRect.yMin += 25f;
            TabDrawer.DrawTabs(inRect, tabs);
            if (!curModule.LabelKey.NullOrEmpty()) curModule.DoSettingsWindowContents(inRect);
        }
    }

    public class UISettings : ModSettings
    {
        public override void ExposeData()
        {
            base.ExposeData();
            foreach (var module in UIMod.AllModules) module.SaveSettings();
        }
    }

    public abstract class Module
    {
        public virtual string LabelKey => "";

        public virtual void SaveSettings()
        {
        }

        public virtual void DoSettingsWindowContents(Rect inRect)
        {
        }

        public abstract void DoPatches(Harmony harm);
    }
}