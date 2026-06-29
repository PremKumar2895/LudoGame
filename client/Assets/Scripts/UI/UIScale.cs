// Responsive IMGUI scaling for phone / tablet / desktop. Scales the GUI matrix off the screen's
// shorter side against a 720 reference (like a CanvasScaler set to "match shorter side"), so HUD
// elements stay readable and touch-friendly at any resolution or orientation.
using UnityEngine;

namespace LudoGame.UI
{
    public static class UIScale
    {
        private const float ReferenceShortSide = 720f;
        private const float Min = 0.85f, Max = 4f;

        /// <summary>Pixels-per-logical-unit. 1 ≈ a 720-tall window; ~1.5 on a 1080 phone; ~2+ on tablets.</summary>
        public static float Factor =>
            Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / ReferenceShortSide, Min, Max);

        /// <summary>Call once at the top of OnGUI; everything after lays out in logical (scaled) coordinates.</summary>
        public static void Apply()
        {
            float f = Factor;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(f, f, 1f));
        }

        /// <summary>Logical screen bounds to lay out against (after Apply()).</summary>
        public static float Width => Screen.width / Factor;
        public static float Height => Screen.height / Factor;
        public static bool Portrait => Screen.height >= Screen.width;
    }
}
