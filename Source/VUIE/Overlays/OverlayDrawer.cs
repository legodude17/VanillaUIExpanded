using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace VUIE;

public abstract class OverlayDrawer
{
    protected Map Map;
    protected OverlayWorker Overlay;
    public abstract void OnCellChange(IntVec3 loc);
    public abstract void Draw();

    public virtual void Init(OverlayWorker overlay)
    {
        Overlay = overlay;
    }

    public virtual void Init(Map map)
    {
        Map = map;
    }

    public virtual void Deinit()
    {
        Map = null;
    }
}

[StaticConstructorOnStartup]
public class OverlayDrawer_Instanced : OverlayDrawer
{
    private static readonly Material mat;
    private readonly Dictionary<IntVec3, int> indexes = new();
    private List<Vector4>[,] colors;
    private int colorsProp;
    private List<Matrix4x4>[,] matrices;
    private MaterialPropertyBlock propertyBlock;

    static OverlayDrawer_Instanced()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, new Color(1, 1, 1, 0.5f));
        tex.Apply();
        mat = MaterialPool.MatFrom(new MaterialRequest
        {
            mainTex = tex,
            shader = ShaderDatabase.Mote,
            color = Color.white,
            colorTwo = Color.white,
            renderQueue = 0,
            shaderParameters = null
        });
        mat.enableInstancing = true;
    }

    public override void Init(OverlayWorker overlay)
    {
        base.Init(overlay);
        propertyBlock = new MaterialPropertyBlock();
        colorsProp = Shader.PropertyToID("_Color");
    }

    public override void Init(Map map)
    {
        base.Init(map);
        var sectionCount = map.mapDrawer.SectionCount;
        matrices = new List<Matrix4x4>[sectionCount.x, sectionCount.z];
        colors = new List<Vector4>[sectionCount.x, sectionCount.z];
        for (var x = 0; x < sectionCount.x; x++)
        for (var z = 0; z < sectionCount.z; z++)
        {
            matrices[x, z] = new List<Matrix4x4>();
            colors[x, z] = new List<Vector4>();
        }
    }

    public override void Deinit()
    {
        base.Deinit();
        matrices = null;
        colors = null;
        indexes.Clear();
    }

    public override void OnCellChange(IntVec3 loc)
    {
        var coords = Map.mapDrawer.SectionCoordsAt(loc);
        var ind = Map.cellIndices.CellToIndex(loc);
        var color = Overlay.Grid[ind];
        var matrixList = matrices[coords.x, coords.z];
        var colorList = colors[coords.x, coords.z];
        if (color == null)
        {
            if (indexes.TryGetValue(loc, out var index))
            {
                matrixList.RemoveAt(index);
                colorList.RemoveAt(index);
                indexes.Remove(loc);
                foreach (var kv in indexes)
                    if (kv.Value > index && Map.mapDrawer.SectionCoordsAt(kv.Key) == coords)
                        indexes[kv.Key]--;
            }
        }
        else
        {
            if (indexes.TryGetValue(loc, out var index))
                colorList[index] = color.Value;
            else
            {
                indexes[loc] = matrixList.Count;
                matrixList.Add(Matrix4x4.TRS(loc.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays), Quaternion.AngleAxis(0f, Vector3.up),
                    Vector3.one));
                colorList.Add(color.Value);
            }
        }
    }

    public override void Draw()
    {
        foreach (var section in Map.mapDrawer.VisibleSections)
        {
            var colorArray = colors[section.x, section.z];
            var matrix = matrices[section.x, section.z];
            if (colorArray.Count == 0 || matrix.Count == 0) continue;
            propertyBlock.Clear();
            propertyBlock.SetVectorArray(colorsProp, colors[section.x, section.z]);
            Graphics.DrawMeshInstanced(MeshPool.plane10, 0, mat, matrices[section.x, section.z], propertyBlock, ShadowCastingMode.Off, false, 0);
        }
    }
}

public class OverlayDrawer_NotInstanced : OverlayDrawer, ICellBoolGiver
{
    private CellBoolDrawer drawer;

    public bool GetCellBool(int index) => Overlay.Grid[index] != null;

    public Color GetCellExtraColor(int index) => Overlay.Grid[index].GetValueOrDefault();
    public Color Color => Color.white;

    public override void Init(Map map)
    {
        base.Init(map);
        drawer = new CellBoolDrawer(this, map.Size.x, map.Size.z);
    }

    public override void Deinit()
    {
        base.Deinit();
        drawer = null;
    }

    public override void OnCellChange(IntVec3 loc)
    {
        drawer.SetDirty();
    }

    public override void Draw()
    {
        drawer.ActuallyDraw();
    }
}