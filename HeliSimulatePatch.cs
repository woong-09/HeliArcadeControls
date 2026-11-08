using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SDG.Unturned;

namespace HeliArcadeControls
{
    /// <summary>
    /// Patch target (checked against the U3-SDK source):
    ///   InteractableVehicle.simulate(uint simulation, int recov, int input_x, int input_y,
    ///                                float look_x, float look_y, bool inputBrake,
    ///                                bool inputStamina, float delta)
    ///
    /// While you are driving, PlayerMovement.simulate calls this method every 0.08 seconds (every 4 physics steps).
    /// HeliFilter picks the supported vehicles (California 2 helicopters use the plane engine, so they are listed by name).
    /// Values the game passes in while you fly a helicopter or plane:
    ///   input_x      : A = -1, D = +1   (yawLeft / yawRight keys)
    ///   input_y      : W = +1, S = -1   (thrustIncrease / thrustDecrease keys)
    ///   look_x/look_y: tilting with the mouse or arrow keys (the core of the original controls)
    ///   inputBrake   : Space (jump key)
    ///   inputStamina : Shift (sprint key)
    ///
    /// How it works:
    ///   Prefix  - Passes the input to ArcadeHeliController and sets the movement and tilt input to 0
    ///             so the original code cannot move the helicopter. (The original method still runs,
    ///             so fuel use, position sync and other work keep working normally.)
    ///   Postfix - Overrides only the rotor display value so the rotor keeps spinning.
    ///   The actual flight is handled every physics step in ArcadeHeliController.FixedUpdate.
    /// </summary>
    [HarmonyPatch(typeof(InteractableVehicle), "simulate", new Type[]
    {
        typeof(uint), typeof(int), typeof(int), typeof(int),
        typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(float)
    })]
    internal static class HeliSimulatePatch
    {
        internal static readonly Type[] TargetArgs = new Type[]
        {
            typeof(uint), typeof(int), typeof(int), typeof(int),
            typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(float)
        };

        // The setter of ReplicatedVelocityInput is private, so it is accessed through reflection
        private static readonly MethodInfo RotorValueSetter =
            AccessTools.PropertySetter(typeof(InteractableVehicle), "ReplicatedVelocityInput");

        private static bool errorReported;
        private static bool firstCallLogged;
        private static readonly HashSet<int> seenVehicles = new HashSet<int>();

        private static void Prefix(InteractableVehicle __instance,
            ref int input_x, ref int input_y, ref float look_x, ref float look_y,
            bool inputBrake, bool inputStamina, out bool __state)
        {
            __state = false;
            try
            {
                if (!ModConfig.Enabled || __instance == null)
                {
                    return;
                }

                VehicleAsset asset = __instance.asset;

                // Diagnostics: logs once that the patch runs, and once per vehicle how it was recognized
                if (!firstCallLogged)
                {
                    firstCallLogged = true;
                    ModLog.Info("simulate patch is running (first call)");
                }
                if (seenVehicles.Add(__instance.GetInstanceID()))
                {
                    if (asset == null)
                    {
                        ModLog.Info("Driving detected: no asset");
                    }
                    else
                    {
                        ModLog.Info("Driving detected: " + HeliFilter.Describe(asset)
                            + ", driver=" + __instance.isDriver
                            + ", supported=" + HeliFilter.IsArcadeTarget(asset)
                            + ", engine powered=" + __instance.isEnginePowered
                            + ", fuel=" + __instance.fuel);
                    }
                }

                if (asset == null || !__instance.isDriver)
                {
                    return;
                }
                if (!HeliFilter.IsArcadeTarget(asset))
                {
                    HeliFilter.ReportSkippedAircraft(asset);
                    return;
                }

                ArcadeHeliController controller = ArcadeHeliController.GetOrAdd(__instance);
                if (controller == null)
                {
                    return;
                }

                controller.ReceiveInput(input_y, input_x, inputBrake, inputStamina);

                // Clear the input so the original helicopter physics does not move the vehicle
                input_x = 0;
                input_y = 0;
                look_x = 0f;
                look_y = 0f;
                __state = true;
            }
            catch (Exception e)
            {
                ReportError(e);
            }
        }

        private static void Postfix(InteractableVehicle __instance, bool __state)
        {
            if (!__state || RotorValueSetter == null)
            {
                return;
            }

            try
            {
                ArcadeHeliController controller = __instance.GetComponent<ArcadeHeliController>();
                if (controller != null && controller.IsFlying)
                {
                    RotorValueSetter.Invoke(__instance, new object[] { controller.GetRotorDisplayValue() });
                }
            }
            catch (Exception e)
            {
                ReportError(e);
            }
        }

        private static void ReportError(Exception e)
        {
            if (errorReported)
            {
                return;
            }
            errorReported = true;
            ModLog.Error("Error in the helicopter patch (shown only once):\n" + e);
        }
    }
}
