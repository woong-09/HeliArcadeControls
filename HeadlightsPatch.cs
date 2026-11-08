using System;
using HarmonyLib;
using SDG.Unturned;

namespace HeliArcadeControls
{
    /// <summary>
    /// In a vehicle, right click toggles the headlights in the game code
    /// (PlayerMovement.Update → VehicleManager.sendVehicleHeadlights).
    /// California 2 helicopters appear to switch into a hover mode by changing the vehicle's settings when the
    /// headlights turn on, and in that mode the arcade controls cannot move forward or backward.
    /// So while you fly a supported helicopter, requests to turn the headlights on are blocked. (Turning them off is allowed.)
    /// </summary>
    [HarmonyPatch(typeof(VehicleManager), "sendVehicleHeadlights")]
    internal static class HeadlightsPatch
    {
        private static bool blockLogged;

        private static bool Prefix()
        {
            try
            {
                if (!ModConfig.Enabled || !ModConfig.BlockHeadlightsToggle)
                {
                    return true;
                }

                Player player = Player.LocalPlayer;
                if (player == null || player.movement == null)
                {
                    return true;
                }

                InteractableVehicle vehicle = player.movement.getVehicle();
                if (vehicle == null || vehicle.asset == null || !vehicle.isDriver)
                {
                    return true;
                }
                if (!HeliFilter.IsArcadeTarget(vehicle.asset))
                {
                    return true;
                }
                if (vehicle.headlightsOn)
                {
                    return true; // Turning the headlights off is allowed
                }

                if (!blockLogged)
                {
                    blockLogged = true;
                    ModLog.Info("Blocked right click (headlights / hover mode toggle). Set BlockHeadlightsToggle = false in the settings file to allow it.");
                }
                return false;
            }
            catch (Exception e)
            {
                ModLog.Error("Headlights patch error: " + e.Message);
                return true;
            }
        }
    }
}
