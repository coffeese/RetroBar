using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroBar.Utilities
{
    internal static class MonitorSelection
    {
        // Keep selection independent of screen enumeration and UI, and never change the saved target.
        public static IReadOnlyList<T> Select<T>(IReadOnlyList<T> screens, bool showMultiMon,
            string targetDeviceName, Func<T, string> deviceName, Func<T, bool> isPrimary)
        {
            if (showMultiMon)
            {
                return screens;
            }

            if (!string.IsNullOrEmpty(targetDeviceName))
            {
                foreach (T screen in screens)
                {
                    if (string.Equals(deviceName(screen), targetDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new[] { screen };
                    }
                }
            }

            return new[] { screens.First(isPrimary) };
        }
    }
}
