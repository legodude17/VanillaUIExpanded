using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VUIE
{
    public static class ArchitectDiffer
    {
        public static List<Diff> FindDiff(ArchitectSaved from, ArchitectSaved to)
        {
            var result = from.Tabs.Select(fromTab => new Diff {label = fromTab.label, defName = fromTab.defName, type = DiffType.Removed}).ToList();

            foreach (var toTab in to.Tabs)
            {
                var existing = result.FirstOrDefault(diff => diff.defName == toTab.defName);
                if (existing is not null) existing.type = DiffType.Same;
                else
                    result.Add(new Diff
                    {
                        label = toTab.label,
                        defName = toTab.defName,
                        type = DiffType.Added
                    });
            }

            foreach (var diff in result)
            {
                var fromTabIdx = from.Tabs.FindIndex(tab => tab.defName == diff.defName);
                var toTabIdx = to.Tabs.FindIndex(tab => tab.defName == diff.defName);
                if (fromTabIdx >= 0 && toTabIdx >= 0)
                    diff.children = FindDiff(from.Tabs[fromTabIdx].Designators, to.Tabs[toTabIdx].Designators);
                else if (fromTabIdx >= 0)
                    diff.children = from.Tabs[fromTabIdx].Designators.Select(Diff.MakeConverter(DiffType.Removed)).ToList();
                else if (toTabIdx >= 0)
                    diff.children = to.Tabs[toTabIdx].Designators.Select(Diff.MakeConverter(DiffType.Added)).ToList();
                else diff.children = new List<Diff>();
            }

            return result;
        }

        public static List<Diff> FindDiff(List<DesignatorSaved> from, List<DesignatorSaved> to)
        {
            var result = new List<Diff>();

            result.AddRange(from.Select(Diff.MakeConverter(DiffType.Removed)));

            foreach (var des in to)
            {
                var diff = result.FirstOrDefault(f => f.type == DiffType.Removed && f == des);
                if (diff is not null) diff.type = DiffType.Same;
                else result.Add(des.ToDiff(DiffType.Added));
            }

            foreach (var diff in result)
            {
                var from2 = from.FirstOrDefault(f => diff == f).Elements;
                var to2 = to.FirstOrDefault(f => diff == f).Elements;

                if (!from2.NullOrEmpty() && !to2.NullOrEmpty())
                    diff.children = FindDiff(from2, to2);
                else if (!from2.NullOrEmpty())
                    diff.children = from2.Select(Diff.MakeConverter(DiffType.Removed)).ToList();
                else if (!to2.NullOrEmpty())
                    diff.children = to2.Select(Diff.MakeConverter(DiffType.Added)).ToList();
                else diff.children = new List<Diff>();
            }

            return result;
        }
    }

    public class Diff : IExposable
    {
        protected bool Equals(Diff other) =>
            label == other.label && defName == other.defName && className == other.className && type == other.type && Equals(children, other.children);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Diff) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = label != null ? label.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (defName != null ? defName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (className != null ? className.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int) type;
                return hashCode;
            }
        } // ReSharper disable InconsistentNaming
        public ArchitectSaved ApplyTo(ArchitectSaved to)
        {
            var result = new ArchitectSaved
            {
                Name = to.Name,
                Vanilla = to.Vanilla,
                Tabs = new List<ArchitectTabSaved>()
            };

            foreach (var child in children)
                if (child.type == DiffType.Added)
                    result.Tabs.Add(new ArchitectTabSaved
                    {
                        label = child.label,
                        defName = child.defName,
                        Designators = child.children.Select(diff => (DesignatorSaved) diff).ToList()
                    });
                else if (child.type == DiffType.Same)
                {
                    var tab = to.Tabs.Find(t => t.defName == child.defName);
                    tab.Designators = ApplyOn(child.children, tab.Designators);
                    result.Tabs.Add(tab);
                }

            return result;
        }

        private static List<DesignatorSaved> ApplyOn(List<Diff> apply, List<DesignatorSaved> on)
        {
            if (on is null) return null;

            var result = new List<DesignatorSaved>();
            foreach (var diff in apply)
                if (diff.type == DiffType.Added)
                    result.Add(diff);
                else if (diff.type == DiffType.Same)
                {
                    var des = on.Find(d => diff == d);
                    des.Elements = ApplyOn(diff.children, des.Elements);
                    result.Add(des);
                }

            return result;
        }

        public string label;
        public string defName;
        public string className;
        public DiffType type;
        public List<Diff> children;
        public string Label => label ?? defName ?? className;
        public bool ChangesAnything => type is DiffType.Removed or DiffType.Added || children.Any(diff => diff.ChangesAnything);

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref className, "className");
            Scribe_Values.Look(ref type, "type");
            Scribe_Collections.Look(ref children, "children", LookMode.Deep);
        }

        public static implicit operator DesignatorSaved(Diff diff) => new()
        {
            Name = diff.label,
            Type = diff.className,
            AdditionalData = diff.defName,
            Elements = diff.children.Select(diff2 => (DesignatorSaved) diff2).ToList()
        };

        public static Func<DesignatorSaved, Diff> MakeConverter(DiffType type) => des => des.ToDiff(type);

        public static bool operator ==(Diff a, ArchitectTabSaved b) => a?.defName == b.defName;

        public static bool operator !=(Diff a, ArchitectTabSaved b) => !(a == b);

        public static bool operator ==(Diff a, DesignatorSaved b) => a?.className == b.Type && a?.label == b.Name && a?.defName == b.AdditionalData;

        public static bool operator !=(Diff a, DesignatorSaved b) => !(a == b);
    }

    public enum DiffType
    {
        None,
        Added,
        Removed,
        Same
    }
}