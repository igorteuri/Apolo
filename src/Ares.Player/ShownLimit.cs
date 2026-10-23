/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The limit of buttons shown at once. A folder with a thousand buttons (or a mother folder with many subfolders) is slow to
 draw, so only the first ones are made; the groups which do not fit are only a heading with their number of buttons.
 Kept free of any window code so that it can be tested.
*/
using System;
using System.Collections.Generic;

namespace Ares.Player
{
    internal static class ShownLimit
    {
        /// <summary>
        /// "items" are the buttons in order; "startsGroup" says whether an item is the first one of a group (a folder or one of its
        /// subfolders). Returns the items to make; the groups which do not fit come back in "collapsed" (complete), and
        /// "hiddenInFirst" is the number of items of the first group which were left out when that group alone was over the limit.
        /// </summary>
        public static List<T> Apply<T>(List<T> items, int limit, Func<T, bool> startsGroup, out List<List<T>> collapsed, out int hiddenInFirst)
        {
            collapsed = new List<List<T>>();
            hiddenInFirst = 0;
            if (items.Count <= limit)
                return items;
            List<List<T>> groups = new List<List<T>>();
            foreach (T item in items)
            {
                if (groups.Count == 0 || startsGroup(item))
                    groups.Add(new List<T>());
                groups[groups.Count - 1].Add(item);
            }
            List<T> kept = new List<T>();
            bool full = false;
            foreach (List<T> group in groups)
            {
                if (!full && kept.Count == 0 && group.Count > limit)
                {
                    // the first group alone is too big: its first buttons, and the rest on demand
                    kept.AddRange(group.GetRange(0, limit));
                    hiddenInFirst = group.Count - limit;
                    full = true;
                }
                else if (!full && kept.Count + group.Count <= limit)
                {
                    kept.AddRange(group);
                }
                else
                {
                    full = true;
                    collapsed.Add(group);
                }
            }
            return kept;
        }
    }
}
