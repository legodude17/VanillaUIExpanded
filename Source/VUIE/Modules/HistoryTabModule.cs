using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class HistoryTabModule : Module
    {
        private static readonly List<Thing> tmpThings = new();
        public static List<ThingWithWealth> WealthThings = new();
        public static List<ThingWealthGroup> Display = new();
        private static Vector2 scrollPos = Vector2.one;

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(MainTabWindow_History), nameof(MainTabWindow_History.PreOpen)),
                postfix: new HarmonyMethod(typeof(HistoryTabModule), nameof(AddTab)));
            harm.Patch(AccessTools.Method(typeof(MainTabWindow_History), nameof(MainTabWindow_History.DoWindowContents)),
                transpiler: new HarmonyMethod(typeof(HistoryTabModule), nameof(AddContents)));
        }

        public static void AddTab(MainTabWindow_History __instance)
        {
            __instance.tabs.Add(new TabRecord("VUIE.History.WealthList".Translate(), SetTab, CheckTab));
            RefreshThings();
        }

        private static void RefreshThings()
        {
            tmpThings.Clear();
            WealthThings.Clear();
            ThingOwnerUtility.GetAllThingsRecursively(Find.CurrentMap, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), tmpThings, false,
                x =>
                {
                    if (x is PassingShip or MapComponent) return false;
                    var pawn = x as Pawn;
                    return (pawn == null || pawn.Faction == Faction.OfPlayer) && (pawn == null || !pawn.IsQuestLodger());
                });
            tmpThings.RemoveAll(t => t.PositionHeld.Fogged(Find.CurrentMap));
            WealthThings.AddRange(tmpThings.Select(t => new ThingWithWealth
            {
                thing = t,
                wealth = t.MarketValue * t.stackCount
            }));
            tmpThings.Clear();
            tmpThings.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).Where(b => b.Faction == Faction.OfPlayer));
            WealthThings.AddRange(tmpThings.Select(t => new ThingWithWealth
            {
                thing = t,
                wealth = t.GetStatValue(StatDefOf.MarketValueIgnoreHp)
            }));
            tmpThings.Clear();
            tmpThings.AddRange(Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer).Where(p => !p.IsQuestLodger()));
            WealthThings.AddRange(tmpThings.Select(t => new ThingWithWealth
            {
                thing = t,
                wealth = t.MarketValue
            }));
            tmpThings.Clear();
            WealthThings.RemoveAll(tww => Mathf.Approximately(tww.wealth, 0));
            Display.Clear();
            Display.AddRange(WealthThings.GroupBy(t => t.thing.def).Select(group => new ThingWealthGroup
            {
                def = group.Key,
                count = group.Sum(t => t.thing.stackCount),
                wealth = group.Sum(t => t.wealth),
                example = group.RandomElement().thing,
                children = group.OrderByDescending(t => t.wealth).ToList()
            }));
            Display.SortStable((lhs, rhs) => rhs.wealth.CompareTo(lhs.wealth));
        }

        public static void DoWealthList(Rect inRect)
        {
            var listing = new Listing_Standard();
            var viewRect = new Rect(0, 0, inRect.width - 30f, Display.Count * 35f + Display.Where(item => item.expanded).SelectMany(item => item.children).Count() * 33f);
            var anchor = Text.Anchor;
            Widgets.BeginScrollView(inRect.ContractedBy(7f), ref scrollPos, viewRect);
            listing.Begin(viewRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (listing.ButtonTextLabeled("VUIE.History.AllThingsWealth".Translate() + ":", "VUIE.Refresh".Translate())) RefreshThings();
            listing.GapLine();
            var highlight = false;
            foreach (var item in Display)
            {
                var rect = listing.GetRect(30f);
                if (highlight) Widgets.DrawLightHighlight(rect);
                if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
                if (Widgets.ButtonImage(rect.LeftPartPixels(30f), item.expanded ? TexButton.Collapse : TexButton.Reveal)) item.expanded = !item.expanded;
                Widgets.ThingIcon(rect.LeftPartPixels(70f).RightPartPixels(30f), item.example);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rect.LeftHalf().RightPart(0.8f), item.def.LabelCap + " x" + item.count);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(rect.RightHalf().LeftPart(0.8f), item.wealth.ToStringMoney());
                if (Widgets.ButtonInvisible(rect))
                {
                    DefDatabase<MainButtonDef>.GetNamed("History").TabWindow.Close();
                    CameraJumper.TryJump(item.example);
                    foreach (var thing in WealthThings.Where(thing => thing.thing.def == item.def))
                        Find.Selector.Select(thing.thing);
                }

                highlight = !highlight;

                if (item.expanded)
                {
                    listing.Gap(3f);
                    listing.Indent();
                    listing.ColumnWidth -= 12f;
                    foreach (var thingWithWealth in item.children)
                    {
                        var rect2 = listing.GetRect(30f);
                        if (highlight) Widgets.DrawLightHighlight(rect2);
                        if (Mouse.IsOver(rect2)) Widgets.DrawHighlight(rect2);
                        Widgets.ThingIcon(rect2.LeftPartPixels(50f).RightPartPixels(30f), thingWithWealth.thing);
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Widgets.Label(rect2.LeftHalf().RightPart(0.8f), thingWithWealth.thing.LabelCap);
                        Text.Anchor = TextAnchor.MiddleRight;
                        Widgets.Label(rect2.RightHalf().LeftPart(0.8f), thingWithWealth.wealth.ToStringMoney());
                        if (Widgets.ButtonInvisible(rect2))
                        {
                            DefDatabase<MainButtonDef>.GetNamed("History").TabWindow.Close();
                            CameraJumper.TryJumpAndSelect(thingWithWealth.thing);
                        }

                        listing.Gap(2f);
                        highlight = !highlight;
                    }

                    listing.ColumnWidth += 12f;
                    listing.Outdent();
                }

                listing.Gap(5f);
            }

            Text.Anchor = anchor;
            listing.End();
            Widgets.EndScrollView();
        }

        public static IEnumerable<CodeInstruction> AddContents(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var list = instructions.ToList();
            var label1 = generator.DefineLabel();
            var info1 = AccessTools.Method(typeof(MainTabWindow_History), nameof(MainTabWindow_History.DoStatisticsPage));
            var idx1 = list.FindIndex(ins => ins.Calls(info1)) + 1;
            var swtch = list.Find(ins => ins.opcode == OpCodes.Switch);
            swtch.operand = ((Label[]) swtch.operand).Append(label1).ToArray();
            list.InsertRange(idx1, new[]
            {
                new CodeInstruction(OpCodes.Ret),
                new CodeInstruction(OpCodes.Ldloc_0).WithLabels(label1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HistoryTabModule), nameof(DoWealthList))),
                new CodeInstruction(OpCodes.Ret)
            });
            return list;
        }

        private static bool CheckTab() => MainTabWindow_History.curTab == (MainTabWindow_History.HistoryTab) 3;

        private static void SetTab()
        {
            MainTabWindow_History.curTab = (MainTabWindow_History.HistoryTab) 3;
        }

        public struct ThingWithWealth
        {
            public Thing thing;
            public float wealth;
        }

        public class ThingWealthGroup
        {
            public List<ThingWithWealth> children;
            public int count;
            public ThingDef def;
            public Thing example;
            public bool expanded;
            public float wealth;
        }
    }
}