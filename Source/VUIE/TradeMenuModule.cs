using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class TradeMenuModule : Module
    {
        private static bool draggingTransferable;

        private static string lastTradPressed;

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(TradeUI), nameof(TradeUI.DrawTradeableRow)), transpiler: new HarmonyMethod(typeof(TradeMenuModule), nameof(AddThreshholdCode)));
            harm.Patch(AccessTools.Method(typeof(TransferableUIUtility), "DoCountAdjustInterfaceInternal"),
                transpiler: new HarmonyMethod(typeof(TradeMenuModule), nameof(ReplaceButtonText)));
        }

        public static IEnumerable<CodeInstruction> AddThreshholdCode(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var info1 = AccessTools.Method(typeof(TransferableUIUtility), nameof(TransferableUIUtility.DoCountAdjustInterface),
                new[]
                {
                    typeof(Rect), typeof(Transferable), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(List<TransferableCountToTransferStoppingPoint>), typeof(bool)
                });
            var idx1 = list.FindIndex(ins => ins.Calls(info1));
            var idx2 = list.FindLastIndex(idx1, ins => ins.opcode == OpCodes.Ldnull);
            list.RemoveAt(idx2);
            list.InsertRange(idx2, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TradeMenuModule), nameof(GetTradeStoppingPoints)))
            });
            return list;
        }

        public static IEnumerable<CodeInstruction> ReplaceButtonText(IEnumerable<CodeInstruction> instructions)
        {
            var info = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] {typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool)});
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(info))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    instruction.operand = AccessTools.Method(typeof(TradeMenuModule), nameof(ButtonTextReplace));
                }

                yield return instruction;
            }
        }

        public static bool ButtonTextReplace(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, Transferable trad)
        {
            var result = Widgets.ButtonTextDraggable(rect, label, drawBackground, doMouseoverSound, active);
            if (!Input.GetMouseButton(0)) draggingTransferable = false;
            if (result == Widgets.DraggableResult.Dragged) draggingTransferable = true;
            if (result.AnyPressed()) return true;
            if (!(draggingTransferable && Mouse.IsOver(rect))) return false;
            var oldTrad = lastTradPressed;
            lastTradPressed = trad.Label;
            return oldTrad != lastTradPressed || result == Widgets.DraggableResult.Dragged;
        }

        public static List<TransferableCountToTransferStoppingPoint> GetTradeStoppingPoints(Tradeable trad)
        {
            var currency = TradeSession.deal.CurrencyTradeable;
            var maxBuy = trad.CountToTransfer + Mathf.FloorToInt((currency.CountHeldBy(Transactor.Colony) + currency.CountToTransfer) / trad.GetPriceFor(TradeAction.PlayerBuys));
            var maxSell = trad.CountToTransfer - Mathf.FloorToInt((currency.CountHeldBy(Transactor.Trader) - currency.CountToTransfer) / trad.GetPriceFor(TradeAction.PlayerSells));
            var list = new List<TransferableCountToTransferStoppingPoint>();
            if (maxBuy > 0 && maxBuy < trad.GetMaximumToTransfer()) list.Add(new TransferableCountToTransferStoppingPoint(maxBuy, "A<", ">A"));
            if (maxSell < 0 && maxSell > trad.GetMinimumToTransfer()) list.Add(new TransferableCountToTransferStoppingPoint(maxSell, "A<", ">A"));
            return list;
        }
    }
}