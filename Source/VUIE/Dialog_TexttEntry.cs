using System;
using Verse;

namespace VUIE
{
    public class Dialog_TextEntry : Dialog_Rename
    {
        private readonly Action<string> onName;

        public Dialog_TextEntry(Action<string> gotName)
        {
            onName = gotName;
        }

        public override void SetName(string name)
        {
            onName(name);
        }

        public static void GetString(Action<string> cb)
        {
            Find.WindowStack.Add(new Dialog_TextEntry(cb)
            {
                curName = ""
            });
        }
    }
}