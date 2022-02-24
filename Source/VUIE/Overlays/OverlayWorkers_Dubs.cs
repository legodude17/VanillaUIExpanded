using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VUIE
{
    public abstract class OverlayWorker_DubsPipes : OverlayWorker_Mod
    {
        public static Dictionary<string, ActiveTypes> PipeTypesActive = new();

        private static Type tempType;
        private static readonly HashSet<Type> patched = new();
        private Type sectionLayerTypeCached;
        public abstract Type SectionLayerType { get; }
        public abstract int PipeType { get; }

        public override bool Visible
        {
            get => sectionLayerTypeCached?.FullName is not null && PipeTypesActive is not null && PipeTypesActive.TryGetValue(sectionLayerTypeCached.FullName, out var types) &&
                   types.Active is not null &&
                   types.Active.TryGetValue(PipeType, out var result) && result;
            set
            {
                if (sectionLayerTypeCached?.FullName is null) return;
                if (!PipeTypesActive.ContainsKey(sectionLayerTypeCached.FullName)) return;
                var types = PipeTypesActive[sectionLayerTypeCached.FullName];
                if (types.Active is null) return;
                types.Active[PipeType] = value;
            }
        }

        private Type SaveType => typeof(OverlayWorker_DubsPipes).AllLeafSubclasses().FirstOrDefault();

        public override void ExposeData()
        {
            base.ExposeData();
            if (GetType() != SaveType) return;
            Scribe_Collections.Look(ref PipeTypesActive, "pipeTypes", LookMode.Value, LookMode.Deep);
            foreach (var name in patched.Select(type => type.FullName).Except(PipeTypesActive.Keys))
                PipeTypesActive.Add(name, new ActiveTypes {Active = new Dictionary<int, bool>()});
        }

        protected override void ModInit(OverlayDef def)
        {
            base.ModInit(def);
            sectionLayerTypeCached = SectionLayerType;
            if (patched.Contains(sectionLayerTypeCached) || sectionLayerTypeCached?.FullName is null) return;
            patched.Add(sectionLayerTypeCached);
            if (!PipeTypesActive.ContainsKey(sectionLayerTypeCached.FullName))
                PipeTypesActive.Add(sectionLayerTypeCached.FullName,
                    new ActiveTypes {Active = new Dictionary<int, bool>()});

            try
            {
                tempType = sectionLayerTypeCached;
                UIMod.Harm.Patch(AccessTools.Method(sectionLayerTypeCached, "DrawLayer"), transpiler: new HarmonyMethod(GetType(), nameof(Transpile)));
            }
            catch (Exception e)
            {
                Log.Error("Error while patching: " + e);
            }
        }

        public static bool ShouldShow(Type layerType, int pipeType) =>
            layerType?.FullName is not null && PipeTypesActive[layerType.FullName].Active.TryGetValue(pipeType, out var result) && result;

        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var list = instructions.ToList();
            var idx1 = list.FindIndex(ins => ins.opcode == OpCodes.Ret);
            var length = tempType.Namespace == "DubsBadHygiene" ? 4 : 2;
            var list2 = list.GetRange(idx1 - length, length);
            var labels = list[idx1].ExtractLabels();
            var label1 = generator.DefineLabel();
            list[idx1].labels.Add(label1);
            Log.Message("Adding code for: " + tempType);
            list.InsertRange(idx1, new[]
            {
                new CodeInstruction(OpCodes.Ldtoken, tempType).WithLabels(labels),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle))),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(tempType, "mode")),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OverlayWorker_DubsPipes), nameof(ShouldShow))),
                new CodeInstruction(OpCodes.Brfalse, label1)
            });
            var idx2 = list.FindIndex(idx1, ins => ins.Branches(out _));
            list.InsertRange(idx2 + 1, list2.Select(ins => ins.Clone()));
            return list;
        }

        public struct ActiveTypes : IExposable
        {
            public Dictionary<int, bool> Active;

            public void ExposeData()
            {
                Scribe_Collections.Look(ref Active, "active", LookMode.Value, LookMode.Value);
            }
        }
    }

    public abstract class OverlayWorker_DBH : OverlayWorker_DubsPipes
    {
        protected override string ModName => "Dubs Bad Hygiene";

        public override Type SectionLayerType => AccessTools.TypeByName("DubsBadHygiene.SectionLayer_PipeOverlay");
    }

    public abstract class OverlayWorker_Rimatomics : OverlayWorker_DubsPipes
    {
        protected override string ModName => "Dubs Rimatomics";

        public override Type SectionLayerType => AccessTools.TypeByName("Rimatomics.SectionLayer_OverlayPipe");
    }

    public class OverlayWorker_Sewage : OverlayWorker_DBH
    {
        public override int PipeType => 0;
    }

    public class OverlayWorker_Air : OverlayWorker_DBH
    {
        public override int PipeType => 1;
    }

    public class OverlayWorker_HighVoltage : OverlayWorker_Rimatomics
    {
        public override int PipeType => 0;
    }

    public class OverlayWorker_Cooling : OverlayWorker_Rimatomics
    {
        public override int PipeType => 1;
    }

    public class OverlayWorker_Steam : OverlayWorker_Rimatomics
    {
        public override int PipeType => 2;
    }

    public class OverlayWorker_ColdWater : OverlayWorker_Rimatomics
    {
        public override int PipeType => 3;
    }

    public class OverlayWorker_Loom : OverlayWorker_Rimatomics
    {
        public override int PipeType => 4;
    }

    public abstract class OverlayWorker_Rimefeller : OverlayWorker_DubsPipes
    {
        protected override string ModName => "Rimefeller";
        public override Type SectionLayerType => AccessTools.TypeByName("Rimefeller.SectionLayer_PipeOverlay");
    }

    public class OverlayWorker_Oil : OverlayWorker_Rimefeller
    {
        public override int PipeType => 0;
    }

    public abstract class OverlayWorker_OilGrid : OverlayWorker_Mod
    {
        private static FastInvokeHandler drawOilGrid;
        private static AccessTools.FieldRef<object, object> getDeepOilGrid;
        private static AccessTools.FieldRef<object, object> getOilGrid;
        private static Func<Map, MapComponent> getRimefeller;
        private static MapComponent rimefeller;
        private static Type rimefellerMapComp;
        private static AccessTools.FieldRef<object, bool> towerDraw;
        protected override string ModName => "Rimefeller";
        protected static MapComponent Rimefeller => rimefeller ??= getRimefeller(Find.CurrentMap);

        protected override void ModInit(OverlayDef def)
        {
            base.ModInit(def);
            if (getRimefeller is not null) return;
            getRimefeller = AccessTools.MethodDelegate<Func<Map, MapComponent>>(AccessTools.Method(AccessTools.TypeByName("Rimefeller.DubUtils"), "Rimefeller"));
            rimefellerMapComp = AccessTools.TypeByName("Rimefeller.MapComponent_Rimefeller");
            getDeepOilGrid = AccessTools.FieldRefAccess<object>(rimefellerMapComp, "DeepOilGrid");
            getOilGrid = AccessTools.FieldRefAccess<object>(rimefellerMapComp, "OilGrid");
            towerDraw = AccessTools.FieldRefAccess<bool>(rimefellerMapComp, "MarkTowersForDraw");
            drawOilGrid = MethodInvoker.GetHandler(AccessTools.Method(AccessTools.TypeByName("Rimefeller.OilGrid"), "MarkFieldsForDraw"));
        }

        public override void OverlayOnGUI()
        {
            base.OverlayOnGUI();
            rimefeller = null;
            towerDraw(Rimefeller) = true;
        }

        protected void DrawOilGrid()
        {
            drawOilGrid(getOilGrid(Rimefeller));
        }

        protected void DrawDeepOilGrid()
        {
            drawOilGrid(getDeepOilGrid(Rimefeller));
        }
    }

    public class OverlayWorker_OilGrid_Normal : OverlayWorker_OilGrid
    {
        public override void OverlayOnGUI()
        {
            base.OverlayOnGUI();
            DrawOilGrid();
        }
    }


    public class OverlayWorker_OilGrid_Deep : OverlayWorker_OilGrid
    {
        public override void OverlayOnGUI()
        {
            base.OverlayOnGUI();
            DrawDeepOilGrid();
        }
    }

    public class OverlayWorker_WaterGrid : OverlayWorker_Mod
    {
        private FastInvokeHandler drawWaterGrid;
        private Func<Map, MapComponent> getHygiene;
        private AccessTools.FieldRef<object, object> getWaterGrid;
        private Type hygieneMapComp;
        private AccessTools.FieldRef<object, bool> towerDraw;
        protected override string ModName => "Dubs Bad Hygiene";

        protected override void ModInit(OverlayDef def)
        {
            base.ModInit(def);
            getHygiene = AccessTools.MethodDelegate<Func<Map, MapComponent>>(AccessTools.Method(AccessTools.TypeByName("DubsBadHygiene.DubUtils"), "Hygiene", new[] {typeof(Map)}));
            hygieneMapComp = AccessTools.TypeByName("DubsBadHygiene.MapComponent_Hygiene");
            getWaterGrid = AccessTools.FieldRefAccess<object>(hygieneMapComp, "WaterGrid");
            towerDraw = AccessTools.FieldRefAccess<bool>(hygieneMapComp, "MarkTowersForDraw");
            drawWaterGrid = MethodInvoker.GetHandler(AccessTools.Method(AccessTools.TypeByName("DubsBadHygiene.GridLayer"), "MarkForDraw"));
        }

        public override void OverlayOnGUI()
        {
            base.OverlayOnGUI();
            var hygiene = getHygiene(Find.CurrentMap);
            towerDraw(hygiene) = true;
            drawWaterGrid(getWaterGrid(hygiene));
        }
    }
}