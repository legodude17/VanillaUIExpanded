using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class PlaySettingsModule : Module
    {
        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls)),
                postfix: new HarmonyMethod(typeof(PlaySettingsModule), nameof(AdditionalControls)));
        }

        public static void AdditionalControls(WidgetRow row, bool worldView)
        {
            foreach (var def in DefDatabase<PlaySettingDef>.AllDefs.Where(def => def.Worker.ShouldDraw(worldView))) def.Worker.Draw(row);
        }
    }

    public class PlaySettingDef : Def
    {
        [Unsaved] private Texture2D iconInt;

        public string iconPath;
        public bool showOnMap = true;
        public bool showOnWorld = true;
        public bool visible;
        public Type workerClass = typeof(PlaySettingWorker);

        [Unsaved] private PlaySettingWorker workerInt;

        public Texture2D Icon => iconInt ??= iconPath.NullOrEmpty() ? TexButton.Add : ContentFinder<Texture2D>.Get(iconPath);
        public PlaySettingWorker Worker => workerInt ??= (PlaySettingWorker) Activator.CreateInstance(workerClass, this);
    }

    public class PlaySettingWorker
    {
        public bool Active;
        public PlaySettingDef def;
        public PlaySettingWorker(PlaySettingDef parent) => def = parent;
        public virtual bool ShouldDraw(bool worldView) => def.visible && worldView ? def.showOnWorld : def.showOnMap;

        public virtual void Draw(WidgetRow row)
        {
            row.ToggleableIcon(ref Active, def.Icon, def.label);
        }
    }
}