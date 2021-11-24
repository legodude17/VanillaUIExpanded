using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VUIE
{
    public class PawnColumnWorker_CopyPasteAssignment : PawnColumnWorker_CopyPaste
    {
        public static Assignment Clipboard;

        public override bool AnythingInClipboard => Clipboard.Valid;

        public override void CopyFrom(Pawn p)
        {
            Clipboard = new Assignment
            {
                Valid = true,
                HostilityResponse = p.playerSettings.hostilityResponse,
                MedicalCare = p.playerSettings.medCare,
                Outfit = p.outfits.CurrentOutfit,
                FoodRestriction = p.foodRestriction.CurrentFoodRestriction,
                DrugPolicy = p.drugs.CurrentPolicy,
                InventoryStock = p.inventoryStock.stockEntries.Clone()
            };
        }

        public override void PasteTo(Pawn p)
        {
            p.playerSettings.hostilityResponse = Clipboard.HostilityResponse;
            p.playerSettings.medCare = Clipboard.MedicalCare;
            p.outfits.CurrentOutfit = Clipboard.Outfit;
            p.foodRestriction.CurrentFoodRestriction = Clipboard.FoodRestriction;
            p.drugs.CurrentPolicy = Clipboard.DrugPolicy;
            p.inventoryStock.stockEntries.CopyFrom(Clipboard.InventoryStock);
        }

        public struct Assignment
        {
            public bool Valid;
            public MedicalCareCategory MedicalCare;
            public HostilityResponseMode HostilityResponse;
            public Outfit Outfit;
            public FoodRestriction FoodRestriction;
            public DrugPolicy DrugPolicy;
            public Dictionary<InventoryStockGroupDef, InventoryStockEntry> InventoryStock;
        }
    }
}