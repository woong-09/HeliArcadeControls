using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using SDG.Framework.Modules;
using SDG.Unturned;
using UnityEngine;

namespace HeliArcadeControls
{
    /// <summary>
    /// Entry point of the Unturned module (Modules folder).
    ///
    /// Why a module instead of BepInEx: in testing, patches applied through BepInEx were registered
    /// but never actually ran in the game (checked in Client.log), while patches applied from an
    /// official Unturned module did run.
    /// </summary>
    public class Nexus : IModuleNexus
    {
        internal const string HarmonyId = "mods.unturned.heliarcadecontrols";
        internal const string Version = "3.3.0";

        private Harmony harmony;

        public void initialize()
        {
            string folder = GetModuleFolder();
            try
            {
                ModConfig.Load(Path.Combine(folder, "HeliArcadeControls.cfg"));
            }
            catch (Exception e)
            {
                ModLog.Error("Could not read the settings file, using default values: " + e.Message);
                HeliFilter.Load(ModConfig.ExtraHelicopters);
            }

            harmony = new Harmony(HarmonyId);
            try
            {
                harmony.PatchAll(typeof(Nexus).Assembly);
            }
            catch (Exception e)
            {
                ModLog.Error("An error occurred while applying the Harmony patches. Helicopter controls will not be changed.\n" + e);
                return;
            }

            VerifyPatch();
        }

        public void shutdown()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyId);
            }
        }

        private static string GetModuleFolder()
        {
            string location = typeof(Nexus).Assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                return Path.GetDirectoryName(location);
            }
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Modules/HeliArcadeControls"));
        }

        private static void VerifyPatch()
        {
            MethodBase target = AccessTools.Method(typeof(InteractableVehicle), "simulate", HeliSimulatePatch.TargetArgs);
            if (target == null)
            {
                ModLog.Error("Could not find InteractableVehicle.simulate (9 arguments). A game update may have changed it.");
                return;
            }

            Patches info = Harmony.GetPatchInfo(target);
            bool hasPrefix = false;
            if (info != null)
            {
                foreach (Patch p in info.Prefixes)
                {
                    if (p.owner == HarmonyId)
                    {
                        hasPrefix = true;
                    }
                }
            }

            if (hasPrefix)
            {
                ModLog.Info("Loaded (v" + Version + "). simulate patch registered. Supported helicopters: " + ModConfig.ExtraHelicopters);
            }
            else
            {
                ModLog.Error("The patch was not attached to InteractableVehicle.simulate.");
            }

            if (!ModConfig.Enabled)
            {
                ModLog.Info("Arcade controls are turned off because Enabled = false in the settings file.");
            }
        }
    }

    /// <summary>Writes to the game's own log (Unturned\Logs\Client.log). Each line is saved right away.</summary>
    internal static class ModLog
    {
        internal static void Info(string message)
        {
            try
            {
                UnturnedLog.info("[HeliArcadeControls] " + message);
            }
            catch
            {
            }
        }

        internal static void Error(string message)
        {
            try
            {
                UnturnedLog.error("[HeliArcadeControls] " + message);
            }
            catch
            {
            }
        }
    }
}
