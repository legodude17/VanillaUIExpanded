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
        private readonly List<TabRecord> tabs;

        private Module curModule;

        public UIMod(ModContentPack content) : base(content)
        {
            var harm = new Harmony("vanillaexpanded.ui");
            modules = typeof(Module).AllSubclassesNonAbstract().Select(type => (Module) Activator.CreateInstance(type)).ToList();
            tabs = modules.Where(mod => !mod.Label.NullOrEmpty()).Select(mod => new TabRecord(mod.Label, () => curModule = mod, curModule == mod)).ToList();
            curModule = modules.First();
            Settings = GetSettings<UISettings>();
            foreach (var module in modules) module.DoPatches(harm);
        }

        public static IEnumerable<Module> AllModules => modules;

        public static T GetModule<T>() where T : Module
        {
            return modules.OfType<T>().First();
        }


        public override string SettingsCategory()
        {
            return "Vanilla UI Expanded";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            inRect.yMin += 25f;
            TabDrawer.DrawTabs(inRect, tabs);
            if (!curModule.Label.NullOrEmpty()) curModule.DoSettingsWindowContents(inRect);
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
        public virtual string Label => "";

        public virtual void SaveSettings()
        {
        }

        public virtual void DoSettingsWindowContents(Rect inRect)
        {
        }

        public abstract void DoPatches(Harmony harm);
    }
}