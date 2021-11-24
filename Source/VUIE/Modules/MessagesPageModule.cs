using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class MessagesPageModule : Module
    {
        private static string searchText = "";

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(MainTabWindow_History), "DoMessagesPage"),
                transpiler: new HarmonyMethod(typeof(MessagesPageModule), nameof(AddMessagesSearchBox)));
        }

        public static IEnumerable<CodeInstruction> AddMessagesSearchBox(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var info1 = AccessTools.Method(typeof(Widgets), "CheckboxLabeled");
            var info2 = AccessTools.Method(typeof(Rect), "set_yMin");
            var idx1 = list.FindIndex(ins => ins.Calls(info1));
            var idx2 = list.FindIndex(idx1, ins => ins.Calls(info2));
            list.InsertRange(idx2 + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarga_S, 1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MessagesPageModule), nameof(DoSearchBar)))
            });
            var idx3 = list.FindIndex(ins =>
                ins.opcode == OpCodes.Isinst && ins.operand is Type t && t == typeof(Message));
            var label = list[idx3 + 1].operand;
            var labels = list[idx3 + 2].labels.ListFullCopy();
            list[idx3 + 2].labels.Clear();
            var load = new CodeInstruction(OpCodes.Ldloc_3);
            load.labels.AddRange(labels);
            list.InsertRange(idx3 + 2, new[]
            {
                load,
                new CodeInstruction(OpCodes.Ldloc_S, 6),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<IArchivable>), "get_Item")),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MessagesPageModule), nameof(ShouldShow))),
                new CodeInstruction(OpCodes.Brfalse_S, label)
            });
            return list;
        }

        public static void DoSearchBar(ref Rect rect)
        {
            searchText = Widgets.TextField(rect.TopPartPixels(20f), searchText);
            rect.yMin += 25f;
        }

        public static bool ShouldShow(IArchivable archivable)
        {
            return archivable.ArchivedLabel.ToLower().Contains(searchText.ToLower());
        }
    }
}