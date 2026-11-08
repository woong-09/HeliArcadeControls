using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace HeliArcadeControls
{
    /// <summary>
    /// Component attached to the helicopter that applies the arcade flight every physics step (0.02 seconds).
    ///
    /// Why not apply forces directly in the Prefix?
    ///   simulate() is only called once every 4 physics steps. A force applied there acts for one step,
    ///   and gravity acts alone for the other three, so only a quarter of the force gets through. (This was
    ///   one reason Space did not lift the helicopter in an earlier version.) So the Prefix only stores the input,
    ///   and the flight is handled here in FixedUpdate every step.
    ///
    /// When the driver gets out or the input stops for more than 0.3 seconds, this does nothing, so the
    /// original game physics (gravity and so on) takes over again. It also does nothing without fuel,
    /// with the engine off, or underwater.
    /// </summary>
    public sealed class ArcadeHeliController : MonoBehaviour
    {
        private const float InputTimeout = 0.3f;

        private InteractableVehicle vehicle;
        private Rigidbody body;

        private float inputForward;   // W = +1, S = -1
        private float inputYaw;       // A = -1, D = +1
        private bool inputUp;         // Space
        private bool inputDown;       // Shift
        private float lastInputTime = -100f;

        private float currentYawRate;               // Degrees per second, ramped so turning starts and stops smoothly
        private Vector3 lastFlatForward = Vector3.forward;
        private float groundDistance = float.PositiveInfinity;
        private bool announced;
        private float lastStatusLogTime = -100f;
        private float lastHeadlightsOffRequest = -100f;

        // While on the ground, do nothing until Space is held (prevents the jump when you first get in)
        private bool landed = true;
        private bool wasFlying;
        private float groundBaseline = 1.5f;   // Distance from the reference point to the ground while parked
        private bool baselineMeasured;
        private float lastVertical;            // Used to stop upward bouncing while Space is not held

        // The root axis of California 2 helicopters is tilted relative to the model, so making the root level
        // makes the helicopter look tilted. Instead, the plane formed by the wheels (the pose when standing on the ground)
        // is used as the true level.
        // Visible attitude = root rotation * levelCorrection
        private Quaternion levelCorrection = Quaternion.identity;
        private bool levelCorrectionReady;

        internal static ArcadeHeliController GetOrAdd(InteractableVehicle v)
        {
            ArcadeHeliController c = v.GetComponent<ArcadeHeliController>();
            if (c == null)
            {
                c = v.gameObject.AddComponent<ArcadeHeliController>();
            }
            c.vehicle = v;
            if (c.body == null)
            {
                c.body = v.GetComponent<Rigidbody>();
            }
            return c;
        }

        internal void ReceiveInput(int forward, int yaw, bool up, bool down)
        {
            inputForward = Mathf.Clamp(forward, -1, 1);
            inputYaw = Mathf.Clamp(yaw, -1, 1);
            inputUp = up;
            inputDown = down;
            lastInputTime = Time.time;

            UpdateGroundDistance();

            if (!baselineMeasured)
            {
                baselineMeasured = true;
                if (groundDistance < 3f)
                {
                    groundBaseline = groundDistance;
                }
            }

            if (vehicle == null || vehicle.asset == null)
            {
                return;
            }

            // Turn off the hover mode (headlights) that right click turns on. At most one request per second
            if (ModConfig.BlockHeadlightsToggle && vehicle.headlightsOn && Time.time - lastHeadlightsOffRequest > 1f)
            {
                lastHeadlightsOffRequest = Time.time;
                VehicleManager.sendVehicleHeadlights();
                ModLog.Info("Turned off the headlights (hover mode) that were on.");
            }

            if (!announced)
            {
                announced = true;
                ModLog.Info("Arcade controls started: " + HeliFilter.Describe(vehicle.asset)
                    + (vehicle.isEnginePowered ? "" : " (engine off or no fuel - add fuel to fly)"));
            }

            // Diagnostics: logs the state every 3 seconds while flying
            if (Time.time - lastStatusLogTime >= 3f)
            {
                lastStatusLogTime = Time.time;
                Vector3 e = (transform.rotation * levelCorrection).eulerAngles;
                float pitchNow = e.x > 180f ? e.x - 360f : e.x;
                float rollNow = e.z > 180f ? e.z - 360f : e.z;
                ModLog.Info("State: " + (landed ? "landed" : "flying")
                    + ", attitude pitch=" + pitchNow.ToString("F0") + " deg roll=" + rollNow.ToString("F0") + " deg"
                    + ", flight control=" + IsFlying
                    + ", engine powered=" + vehicle.isEnginePowered
                    + ", fuel=" + vehicle.fuel
                    + ", input W/S=" + inputForward + " A/D=" + inputYaw
                    + " Space=" + inputUp + " Shift=" + inputDown
                    + ", speed=" + (body != null ? body.velocity.magnitude.ToString("F1") : "?") + "m/s"
                    + ", to ground=" + (float.IsInfinity(groundDistance) ? "40m+" : groundDistance.ToString("F1") + "m"));
            }
        }

        internal bool IsFlying
        {
            get
            {
                if (vehicle == null || body == null)
                {
                    return false;
                }
                if (Time.time - lastInputTime > InputTimeout)
                {
                    return false;
                }
                if (body.isKinematic || vehicle.isDead || vehicle.isExploded || vehicle.isUnderwater)
                {
                    return false;
                }
                return vehicle.isEnginePowered;
            }
        }

        /// <summary>Value used to display rotor spin and engine sound. Spins faster while there is input.</summary>
        internal float GetRotorDisplayValue()
        {
            float baseValue = 20f;
            if (vehicle != null && vehicle.asset != null && vehicle.asset.TargetForwardVelocity > 0f)
            {
                baseValue = vehicle.asset.TargetForwardVelocity;
            }
            bool anyInput = inputForward != 0f || inputYaw != 0f || inputUp || inputDown;
            return baseValue * (anyInput ? 1f : 0.8f);
        }

        private void FixedUpdate()
        {
            if (!IsFlying)
            {
                currentYawRate = 0f;
                wasFlying = false;
                return;
            }

            bool onGround = IsOnGround();
            if (!wasFlying)
            {
                wasFlying = true;
                landed = onGround;
                lastVertical = 0f;
                if (!levelCorrectionReady)
                {
                    ComputeLevelCorrection(onGround);
                }
            }

            if (landed)
            {
                // On the ground: leave the original game physics (gravity, wheels) alone until Space is held
                if (!inputUp || inputDown)
                {
                    currentYawRate = 0f;
                    return;
                }
                landed = false;
                lastVertical = 0f;
                ModLog.Info("Takeoff");
            }
            else if (onGround && !inputUp && body.velocity.y <= 0.5f)
            {
                landed = true;
                currentYawRate = 0f;
                ModLog.Info("Landed");
                return;
            }

            float dt = Time.fixedDeltaTime;

            // 1) Cancel gravity: with no keys pressed, the helicopter stays in place (hover)
            if (body.useGravity)
            {
                body.AddForce(-Physics.gravity, ForceMode.Acceleration);
            }

            // 2) Movement: always move forward and backward horizontally, even while the helicopter is tilted
            Quaternion visual = body.rotation * levelCorrection;
            Vector3 flatForward = Vector3.ProjectOnPlane(visual * Vector3.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                lastFlatForward = flatForward.normalized;
            }

            float maxHorizontal = inputForward >= 0f ? GetForwardSpeed() : ModConfig.BackwardSpeed;
            Vector3 targetHorizontal = lastFlatForward * (inputForward * maxHorizontal);

            float targetVertical = 0f;
            if (inputUp && !inputDown)
            {
                targetVertical = ModConfig.ClimbSpeed;
            }
            else if (inputDown && !inputUp)
            {
                float descend = ModConfig.DescendSpeed;
                if (groundDistance < ModConfig.LandingHeight)
                {
                    descend = Mathf.Min(descend, ModConfig.LandingSpeed);
                }
                targetVertical = -descend;
            }

            Vector3 velocity = body.velocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float horizontalAccel = inputForward != 0f ? ModConfig.HorizontalAcceleration : ModConfig.HorizontalBrake;
            horizontal = Vector3.MoveTowards(horizontal, targetHorizontal, horizontalAccel * dt);
            float vertical = Mathf.MoveTowards(velocity.y, targetVertical, ModConfig.VerticalAcceleration * dt);

            // While Space is not held, upward speed can only decrease, never increase (prevents bouncing from wheel springs and so on)
            float upLimit = (inputUp && !inputDown) ? ModConfig.ClimbSpeed : Mathf.Max(lastVertical, 0f);
            if (vertical > upLimit)
            {
                vertical = upLimit;
            }
            lastVertical = vertical;

            body.velocity = new Vector3(horizontal.x, vertical, horizontal.z);

            // 3) Rotation: A/D turns left and right (yaw), the attitude levels itself automatically, plus a slight visual tilt
            currentYawRate = Mathf.MoveTowards(currentYawRate, inputYaw * ModConfig.YawSpeed, ModConfig.YawAcceleration * dt);

            bool landing = groundDistance < groundBaseline + 1f && !inputUp;
            float pitch = landing ? 0f : inputForward * ModConfig.TiltAngle;   // Positive dips the nose
            float roll = landing ? 0f : -inputYaw * ModConfig.BankAngle;       // Banks toward the turn

            float heading = Mathf.Atan2(lastFlatForward.x, lastFlatForward.z) * Mathf.Rad2Deg;
            Quaternion targetVisual = Quaternion.Euler(pitch, heading, roll);
            Quaternion error = targetVisual * Quaternion.Inverse(visual);

            float angle;
            Vector3 axis;
            error.ToAngleAxis(out angle, out axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            Vector3 levelingVelocity = Vector3.zero;
            if (Mathf.Abs(angle) > 0.01f && !float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
            {
                levelingVelocity = axis.normalized * (angle * Mathf.Deg2Rad * ModConfig.LevelingStrength);
            }

            body.angularVelocity = levelingVelocity + Vector3.up * (currentYawRate * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Finds the helicopter's true level reference.
        /// First choice: the plane formed by the bottom points of 3 or more wheels (same result wherever it is parked)
        /// Second choice: the pose it was parked in on the ground
        /// </summary>
        private void ComputeLevelCorrection(bool onGround)
        {
            levelCorrectionReady = true;
            string source = null;

            List<Vector3> points = new List<Vector3>();
            Wheel[] wheels = vehicle.tires;
            if (wheels != null)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    WheelCollider wc = wheels[i] != null ? wheels[i].wheel : null;
                    if (wc == null)
                    {
                        continue;
                    }
                    Vector3 center;
                    Quaternion rot;
                    wc.GetWorldPose(out center, out rot);
                    Vector3 bottom = center - wc.transform.up * (wc.radius * wc.transform.lossyScale.y);
                    points.Add(transform.InverseTransformPoint(bottom));
                }
            }

            if (points.Count >= 3)
            {
                Vector3 bestNormal = Vector3.zero;
                float bestArea = 0f;
                for (int a = 0; a < points.Count; a++)
                {
                    for (int b = a + 1; b < points.Count; b++)
                    {
                        for (int c = b + 1; c < points.Count; c++)
                        {
                            Vector3 cross = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                            if (cross.magnitude > bestArea)
                            {
                                bestArea = cross.magnitude;
                                bestNormal = cross;
                            }
                        }
                    }
                }

                if (bestArea > 0.05f)
                {
                    Vector3 normal = bestNormal.normalized;
                    Vector3 centroid = Vector3.zero;
                    for (int i = 0; i < points.Count; i++)
                    {
                        centroid += points[i];
                    }
                    centroid /= points.Count;
                    if (Vector3.Dot(normal, body.centerOfMass - centroid) < 0f)
                    {
                        normal = -normal; // Point up from the wheels toward the body
                    }
                    if (Vector3.Angle(normal, Vector3.up) < 45f)
                    {
                        levelCorrection = Quaternion.FromToRotation(Vector3.up, normal);
                        source = points.Count + " wheels";
                    }
                }
            }

            if (source == null && onGround)
            {
                Vector3 flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (flat.sqrMagnitude > 0.0001f)
                {
                    Quaternion headingOnly = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    levelCorrection = Quaternion.Inverse(transform.rotation) * headingOnly;
                    source = "parked pose";
                }
            }

            Vector3 e = levelCorrection.eulerAngles;
            float p = e.x > 180f ? e.x - 360f : e.x;
            float r = e.z > 180f ? e.z - 360f : e.z;
            ModLog.Info("Level correction: pitch " + p.ToString("F1") + " deg, roll " + r.ToString("F1") + " deg (reference: " + (source ?? "none, using the vehicle root axis") + ")");
        }

        /// <summary>On the ground if any wheel touches the ground, or if the height is close to the parked height.</summary>
        private bool IsOnGround()
        {
            Wheel[] wheels = vehicle.tires;
            if (wheels != null)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null && wheels[i].isGrounded)
                    {
                        return true;
                    }
                }
            }
            return groundDistance <= groundBaseline + 0.3f;
        }

        /// <summary>If the setting is 0 or less, uses the helicopter's original top speed (Speed_Max).</summary>
        private float GetForwardSpeed()
        {
            float configured = ModConfig.ForwardSpeed;
            if (configured > 0f)
            {
                return configured;
            }
            if (vehicle != null && vehicle.asset != null && vehicle.asset.TargetForwardVelocity > 0f)
            {
                return vehicle.asset.TargetForwardVelocity;
            }
            return 30f;
        }

        /// <summary>Rough distance to the ground directly below. Used to slow down when landing.</summary>
        private void UpdateGroundDistance()
        {
            groundDistance = float.PositiveInfinity;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || col.transform.IsChildOf(transform))
                {
                    continue; // Ignore the helicopter itself (body, wheels, passengers)
                }
                float d = hits[i].distance - 0.5f;
                if (d < groundDistance)
                {
                    groundDistance = d;
                }
            }
        }
    }
}
