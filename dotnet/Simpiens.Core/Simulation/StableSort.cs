using System.Collections.Generic;

namespace Simpiens.Simulation
{
    internal static class StableSort
    {
        // Stable insertion sort: per-tick lists are small, it allocates nothing, and equal keys keep arrival order
        public static void Sort<T>(List<T> list, System.Comparison<T> compare)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var item = list[i];
                int j = i - 1;
                while (j >= 0 && compare(list[j], item) > 0)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = item;
            }
        }
    }
}
