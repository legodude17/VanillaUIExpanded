using System;
using System.Collections.Generic;
using System.IO;
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
        public static UIMod Instance;
        public static List<Window> StartingWindows = new();

        public static bool DoLateInit = true;
        private Module curModule;

        public UIMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Harm = new Harmony("vanillaexpanded.ui");
            modules = typeof(Module).AllSubclassesNonAbstract().Select(type => (Module) Activator.CreateInstance(type)).ToList();
            curModule = modules.First(mod => !mod.LabelKey.NullOrEmpty());
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Settings = GetSettings<UISettings>();
                if (DoLateInit) LateInit();
            });
            foreach (var module in modules) module.DoPatches(Harm);
            Harm.Patch(AccessTools.Method(typeof(UIRoot_Entry), nameof(UIRoot_Entry.Init)), postfix: new HarmonyMethod(GetType(), nameof(DoStartingWindows)));
        }

        public static IEnumerable<Module> AllModules => modules;

        public static void DisplayWindow(Window window)
        {
            if (Find.WindowStack is null)
                StartingWindows.Add(window);
            else
                Find.WindowStack.Add(window);
        }

        public void LateInit()
        {
            Log.Message("[VUIE] Initializing...");
            foreach (var module in modules) module.LateInit();
        }

        public static T GetModule<T>() where T : Module => modules.OfType<T>().First();

        public override string SettingsCategory() => "VUIE".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            inRect.yMin += 25f;
            TabDrawer.DrawTabs(inRect,
                modules.Where(mod => !mod.LabelKey.NullOrEmpty()).Select(mod => new TabRecord(mod.LabelKey.Translate(), () => curModule = mod, curModule == mod)).ToList());
            if (!curModule.LabelKey.NullOrEmpty()) curModule.DoSettingsWindowContents(inRect);
        }

        public static void DoStartingWindows()
        {
            foreach (var window in StartingWindows) Find.WindowStack.Add(window);

            StartingWindows.Clear();
        }
    }

    public class UISettings : ModSettings
    {
        public override void ExposeData()
        {
            base.ExposeData();
            foreach (var module in UIMod.AllModules) module.SaveSettings();
        }

        public void Read()
        {
            var settingsFilename = LoadedModManager.GetSettingsFilename(Mod.Content.FolderName, Mod.GetType().Name);
            try
            {
                if (File.Exists(settingsFilename))
                {
                    Scribe.loader.InitLoading(settingsFilename);
                    Scribe.EnterNode("ModSettings");
                    try
                    {
                        ExposeData();
                    }
                    finally
                    {
                        Scribe.ExitNode();
                        Scribe.loader.FinalizeLoading();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Caught exception while loading mod settings data for {Mod.Content.PackageId}. Generating fresh settings. The exception was: {ex}");
            }
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

        public virtual void LateInit()
        {
        }
    }
}