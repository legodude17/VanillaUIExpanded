using RimWorld;
using Verse;

namespace VUIE;

public class OverlayWorker_Power : OverlayWorker
{
    public override void OverlayDraw()
    {
        base.OverlayDraw();
        OverlayDrawHandler.DrawPowerGridOverlayThisFrame();
    }
}

public class OverlayWorker_Roof : OverlayWorker
{
    public override bool Visible
    {
        get => Current.Game.playSettings.showRoofOverlay;
        set => Current.Game.playSettings.showRoofOverlay = value;
    }

    public override string Label => "ShowRoofOverlayToggleButton".Translate();

    public override bool CanDisabled => false;

    public override void ExposeData()
    {
        if (Current.Game != null)
            base.ExposeData();
    }
}

public class OverlayWorker_Fertility : OverlayWorker
{
    public override bool Visible
    {
        get => Current.Game.playSettings.showFertilityOverlay;
        set => Current.Game.playSettings.showFertilityOverlay = value;
    }

    public override string Label => "ShowFertilityOverlayToggleButton".Translate();


    public override void ExposeData()
    {
        if (Current.Game != null)
            base.ExposeData();
    }
}

public class OverlayWorker_Terrain : OverlayWorker
{
    public override bool Visible
    {
        get => Current.Game.playSettings.showTerrainAffordanceOverlay;
        set => Current.Game.playSettings.showTerrainAffordanceOverlay = value;
    }

    public override string Label => "ShowTerrainAffordanceOverlayToggleButton".Translate();

    public override bool CanDisabled => false;

    public override void ExposeData()
    {
        if (Current.Game != null)
            base.ExposeData();
    }
}