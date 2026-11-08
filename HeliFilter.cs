using System;
using System.Collections.Generic;
using SDG.Unturned;

namespace HeliArcadeControls
{
    /// <summary>
    /// Decides which vehicles get the arcade controls.
    ///
    /// Important: the California 2 helicopters (Hummingbird, Chopper, Chopper PMC, Navajo) are defined
    /// with Engine Plane in their vehicle files (.dat), so the game treats them as planes.
    /// That is why the targets are vehicles with the HELICOPTER engine plus the vehicles listed in ExtraHelicopters.
    /// </summary>
    internal static class HeliFilter
    {
        private static readonly HashSet<string> extraNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<ushort> extraIds = new HashSet<ushort>();
        private static readonly HashSet<string> reportedSkipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Load(string csv)
        {
            extraNames.Clear();
            extraIds.Clear();
            if (string.IsNullOrEmpty(csv))
            {
                return;
            }

            foreach (string raw in csv.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string entry = raw.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                ushort id;
                if (ushort.TryParse(entry, out id))
                {
                    extraIds.Add(id);
                }
                else
                {
                    extraNames.Add(entry);
                }
            }
        }

        internal static bool IsArcadeTarget(VehicleAsset asset)
        {
            if (asset == null)
            {
                return false;
            }
            if (asset.engine == EEngine.HELICOPTER)
            {
                return true;
            }
            if (asset.engine != EEngine.PLANE && asset.engine != EEngine.BLIMP)
            {
                return false; // Cars, boats, trains and so on are never touched
            }
            if (asset.name != null && extraNames.Contains(asset.name.Trim()))
            {
                return true;
            }
            return asset.id != 0 && extraIds.Contains(asset.id);
        }

        /// <summary>
        /// Logs once when you get into a plane or blimp that is not in the list.
        /// If a helicopter from another map uses the plane engine, add the name shown here to the settings file.
        /// </summary>
        internal static void ReportSkippedAircraft(VehicleAsset asset)
        {
            if (asset == null || (asset.engine != EEngine.PLANE && asset.engine != EEngine.BLIMP))
            {
                return;
            }

            string key = asset.name ?? asset.id.ToString();
            if (!reportedSkipped.Add(key))
            {
                return;
            }

            ModLog.Info("Keeping the original controls: " + Describe(asset)
                + ". If this vehicle is a helicopter, add " + asset.name + " to ExtraHelicopters in the settings file.");
        }

        internal static string Describe(VehicleAsset asset)
        {
            return asset.FriendlyName + " [name " + asset.name + ", ID " + asset.id + ", engine " + asset.engine + "]";
        }
    }
}
