using System;
using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using MonoMod.Utils;
using UnityEngine;
using Verse;

// ReSharper disable InconsistentNaming

namespace VUIE;

public class OverlayDef : Def
{
    public List<string> autoshowOn;
    public bool canAutoShow;
    public bool canShowGrid;
    public bool canShowNumbers;
    public MethodReference<Func<IntVec3, Map, Color?>> colorGetter;
    public bool enableByDefault = true;
    public string getter;
    [Unsaved] private Texture2D iconInt;

    public string iconPath;
    public MapMeshFlag relevantChangeTypes;
    public List<string> requiredMods;
    public MethodReference<Func<IntVec3, Map, float>> valueGetter;
    public Type workerClass;

    [Unsaved] private OverlayWorker workerInt;

    public Texture2D Icon
    {
        get => iconInt ??= iconPath.NullOrEmpty() ? TexButton.Add : ContentFinder<Texture2D>.Get(iconPath);
        set => iconInt = value;
    }

    public OverlayWorker Worker
    {
        get => workerInt ??= ((OverlayWorker)Activator.CreateInstance(workerClass ?? typeof(OverlayWorker))).Init(this);
        set => workerInt = value;
    }

    public override void PostLoad()
    {
        base.PostLoad();
        if (getter != null)
        {
            valueGetter ??= new MethodReference<Func<IntVec3, Map, float>>(getter);
            colorGetter ??= new MethodReference<Func<IntVec3, Map, Color?>>(getter + "Color");
        }
    }
}

public class MethodReference<T> where T : Delegate
{
    public T Call;

    public MethodReference()
    {
    }

    public MethodReference(string name) => Call = AccessTools.Method(name).CreateDelegate<T>();

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
        Call = AccessTools.Method(xmlRoot.InnerText).CreateDelegate<T>();
    }
}