using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace VUIE
{
    public static class ArchitectExportImport
    {
        public static string FolderPath => Path.Combine(GenFilePaths.ConfigFolderPath, nameof(UIMod));

        public static IEnumerable<string> AllExported => Directory.EnumerateFiles(FolderPath).Select(Path.GetFileNameWithoutExtension);

        public static string FullFilePath(string exportName) => SafeSaver.GetFileFullPath(Path.Combine(FolderPath, GenText.SanitizeFilename($"{exportName}.xml")));

        public static void Export(ArchitectSaved saved, string path)
        {
            if (File.Exists(path)) Log.Warning("[VUIE] Overwriting previously exported architect: " + saved.Name + " at " + path);
            Scribe.saver.InitSaving(path, "ArchitectTabs");
            try
            {
                ScribeMetaHeaderUtility.WriteMetaHeader();
                Scribe_Deep.Look(ref saved, "ArchitectTab");
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }
        }

        public static bool TryImport(string path, out ArchitectSaved saved)
        {
            saved = default;
            if (File.Exists(path))
            {
                Scribe.loader.InitLoading(path);
                try
                {
                    ScribeMetaHeaderUtility.LoadGameDataHeader(ScribeMetaHeaderUtility.ScribeHeaderMode.None, true);
                    Scribe_Deep.Look(ref saved, "ArchitectTab");
                    Scribe.loader.FinalizeLoading();
                    return true;
                }
                catch
                {
                    Scribe.loader.ForceStop();
                }
            }
            else Log.Error("[VUIE] Could not find exported architect at " + path);

            return false;
        }
    }

    public abstract class Dialog_ArchitectList : Dialog_FileList
    {
        public Vector2 _get_InitialSize()
        {
            throw new NotImplementedException("Harmony reverse-patch of Dialog_FileList.InitialSize failed.");
        }

        public override Vector2 InitialSize
        {
            get { return _get_InitialSize(); }
        }

        public void _DoWindowContents(Rect inRect)
        {
            throw new NotImplementedException("Harmony reverse-patch of Dialog_FileList.DoWindowContents failed.");
        }

        public override void DoWindowContents(Rect inRect)
        {
            _DoWindowContents(inRect);
        }

        public override void ReloadFiles()
        {
            files.Clear();
            var dir = new DirectoryInfo(ArchitectExportImport.FolderPath);
            if (!dir.Exists) dir.Create();
            foreach (var file in dir.GetFiles().Where(file => file.Extension == ".xml").OrderByDescending(file => file.LastWriteTime))
            {
                var info = new SaveFileInfo(file);
                info.LoadData();
                files.Add(info);
            }
        }
    }

    public class Dialog_ArchitectList_Export : Dialog_ArchitectList
    {
        private readonly ArchitectSaved exporting;

        public Dialog_ArchitectList_Export(ArchitectSaved saved)
        {
            exporting = saved;
            interactButLabel = "OverwriteButton".Translate();
            typingName = exporting.Name;
        }

        public override bool ShouldDoTypeInField => true;

        public override void DoFileInteraction(string fileName)
        {
            LongEventHandler.QueueLongEvent(delegate { ArchitectExportImport.Export(exporting, ArchitectExportImport.FullFilePath(fileName)); }, "SavingLongEvent", false, null);
            Messages.Message("SavedAs".Translate(fileName), MessageTypeDefOf.SilentInput, false);
            Close();
        }
    }

    public class Dialog_ArchitectList_Import : Dialog_ArchitectList
    {
        private readonly Action<ArchitectSaved> onLoad;

        public Dialog_ArchitectList_Import(Action<ArchitectSaved> action)
        {
            onLoad = action;
            interactButLabel = "LoadGameButton".Translate();
        }

        public override void DoFileInteraction(string fileName)
        {
            PreLoadUtility.CheckVersionAndLoad(ArchitectExportImport.FullFilePath(fileName), ScribeMetaHeaderUtility.ScribeHeaderMode.None, delegate
            {
                if (ArchitectExportImport.TryImport(ArchitectExportImport.FullFilePath(fileName), out var result)) onLoad(result);

                Close();
            });
        }
    }
}