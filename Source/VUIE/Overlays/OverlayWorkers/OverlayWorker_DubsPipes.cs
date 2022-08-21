using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VUIE;

public abstract class OverlayWorker_DubsPipes : OverlayWorker
{
    public static Dictionary<string, ActiveTypes> PipeTypesActive = new();

    private static Type tempType;
    private static readonly HashSet<Type> patched = new();
    private Type sectionLayerTypeCached;
    public abstract Type SectionLayerType { get; }
    public abstract int PipeType { get; }

    public override bool Visible
    {
        get => sectionLayerTypeCached?.FullName is not null && PipeTypesActive is not null &&
               PipeTypesActive.TryGetValue(sectionLayerTypeCached.FullName, out var types) &&
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
            PipeTypesActive.Add(name, new ActiveTypes { Active = new Dictionary<int, bool>() });
    }

    public override void InitInner()
    {
        base.InitInner();
        sectionLayerTypeCached = SectionLayerType;
        if (patched.Contains(sectionLayerTypeCached) || sectionLayerTypeCached?.FullName is null) return;
        patched.Add(sectionLayerTypeCached);
        if (!PipeTypesActive.ContainsKey(sectionLayerTypeCached.FullName))
            PipeTypesActive.Add(sectionLayerTypeCached.FullName,
                new ActiveTypes { Active = new Dictionary<int, bool>() });

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