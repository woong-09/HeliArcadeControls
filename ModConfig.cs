using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HeliArcadeControls
{
    /// <summary>
    /// Reads HeliArcadeControls.cfg (key = value format) from the module folder.
    /// If the file does not exist, it is created with the default values. Changes apply after restarting the game.
    /// The comments in the created file are written in Korean, with English below.
    /// </summary>
    internal static class ModConfig
    {
        internal static bool Enabled = true;
        internal static string ExtraHelicopters = "CA_Hummingbird,CA_Chopper,CA_Chopper_PMC,CA_Navajo";
        internal static bool BlockHeadlightsToggle = true;

        internal static float ForwardSpeed = 0f;
        internal static float BackwardSpeed = 12f;
        internal static float ClimbSpeed = 10f;
        internal static float DescendSpeed = 10f;
        internal static float LandingSpeed = 3f;
        internal static float LandingHeight = 8f;

        internal static float HorizontalAcceleration = 15f;
        internal static float HorizontalBrake = 10f;
        internal static float VerticalAcceleration = 20f;
        internal static float YawSpeed = 75f;
        internal static float YawAcceleration = 240f;
        internal static float TiltAngle = 12f;
        internal static float BankAngle = 10f;
        internal static float LevelingStrength = 4f;

        internal static void Load(string path)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, DefaultFileText, new UTF8Encoding(false));
                ModLog.Info("Created a new settings file: " + path);
            }

            foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                Apply(key, value);
            }

            HeliFilter.Load(ExtraHelicopters);
        }

        private static void Apply(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "enabled": Enabled = !value.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                case "extrahelicopters": ExtraHelicopters = value; break;
                case "blockheadlightstoggle": BlockHeadlightsToggle = !value.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                case "forwardspeed": ForwardSpeed = ParseFloat(value, ForwardSpeed); break;
                case "backwardspeed": BackwardSpeed = ParseFloat(value, BackwardSpeed); break;
                case "climbspeed": ClimbSpeed = ParseFloat(value, ClimbSpeed); break;
                case "descendspeed": DescendSpeed = ParseFloat(value, DescendSpeed); break;
                case "landingspeed": LandingSpeed = ParseFloat(value, LandingSpeed); break;
                case "landingheight": LandingHeight = ParseFloat(value, LandingHeight); break;
                case "horizontalacceleration": HorizontalAcceleration = ParseFloat(value, HorizontalAcceleration); break;
                case "horizontalbrake": HorizontalBrake = ParseFloat(value, HorizontalBrake); break;
                case "verticalacceleration": VerticalAcceleration = ParseFloat(value, VerticalAcceleration); break;
                case "yawspeed": YawSpeed = ParseFloat(value, YawSpeed); break;
                case "yawacceleration": YawAcceleration = ParseFloat(value, YawAcceleration); break;
                case "tiltangle": TiltAngle = ParseFloat(value, TiltAngle); break;
                case "bankangle": BankAngle = ParseFloat(value, BankAngle); break;
                case "levelingstrength": LevelingStrength = ParseFloat(value, LevelingStrength); break;
            }
        }

        private static float ParseFloat(string value, float fallback)
        {
            float result;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            return fallback;
        }

        private const string DefaultFileText =
@"# HeliArcadeControls 설정
# HeliArcadeControls settings
# 숫자를 바꾸고 게임을 다시 켜면 적용됩니다. # 로 시작하는 줄은 설명입니다.
# Change a value and restart the game to apply it. Lines starting with # are comments.

# true면 아케이드 조종(Space 상승, Shift 하강, W/S 전후진, A/D 회전), false면 원래 조종
# true turns on the arcade controls (Space climb, Shift descend, W/S forward and backward, A/D turn). false uses the original controls.
Enabled = true

# 엔진이 Plane으로 설정된 헬기 목록 (차량 .dat 파일 이름 또는 ID, 쉼표로 구분)
# 캘리포니아2 헬기는 전부 Engine Plane이라 여기 있어야 적용됩니다.
# 엔진이 Helicopter인 차량은 이 목록과 상관없이 항상 적용됩니다.
# Helicopters that their map defines with the plane engine (vehicle .dat file name or ID, separated by commas).
# All California 2 helicopters use the plane engine, so they must be listed here.
# Vehicles that use the helicopter engine are always supported, whether or not they are listed.
ExtraHelicopters = CA_Hummingbird,CA_Chopper,CA_Chopper_PMC,CA_Navajo

# true면 헬기 운전 중 마우스 우클릭(헤드라이트 키)을 막음
# 캘리포니아2 헬기는 우클릭으로 호버링 모드가 켜지는데, 이 모드에서는 전진/후진이 안 됩니다.
# 탈 때 헤드라이트(호버링 모드)가 켜져 있으면 자동으로 끕니다. false면 원래대로 우클릭 사용 가능
# true blocks right click (the headlights key) while flying a helicopter.
# Right click turns on a hover mode on California 2 helicopters, and forward and backward movement does not work in that mode.
# If the headlights (hover mode) are on when you get in, they are turned off automatically. false allows right click again.
BlockHeadlightsToggle = true

# 최고 전진 속도 (m/s). 0이면 기체의 원래 최고속도 사용 (Hummingbird 50, Chopper/Navajo 37.5)
# Top forward speed (m/s). 0 uses each helicopter's original top speed (Hummingbird 50, Chopper and Navajo 37.5).
ForwardSpeed = 0
# 최고 후진 속도 (m/s)
# Top backward speed (m/s)
BackwardSpeed = 12
# Space 상승 속도 / Shift 하강 속도 (m/s)
# Climb speed with Space / descend speed with Shift (m/s)
ClimbSpeed = 10
DescendSpeed = 10
# LandingHeight(m) 아래에서는 LandingSpeed(m/s)로 천천히 하강
# Below LandingHeight (m), the helicopter descends slowly at LandingSpeed (m/s).
LandingSpeed = 3
LandingHeight = 8

# 전후진 가속도 / W,S를 뗐을 때 감속도 / 상승·하강 가속도 (m/s^2)
# Forward and backward acceleration / slowdown after releasing W or S / climb and descend acceleration (m/s^2)
HorizontalAcceleration = 15
HorizontalBrake = 10
VerticalAcceleration = 20
# A/D 회전 속도 (도/초) / 회전이 붙고 멈추는 빠르기 (도/초^2)
# Turn speed with A/D (degrees per second) / how quickly turning starts and stops (degrees per second^2)
YawSpeed = 75
YawAcceleration = 240
# 움직일 때 기체가 앞뒤 / 옆으로 기울어 보이는 각도 (도, 보기용)
# How far the helicopter tilts forward or backward / sideways while moving (degrees, visual only)
TiltAngle = 12
BankAngle = 10
# 기체를 수평으로 되돌리는 힘
# How strongly the helicopter returns to level
LevelingStrength = 4
";
    }
}
