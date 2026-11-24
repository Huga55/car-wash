namespace LittleGarage
{
    /// <summary>
    /// Single source of truth for the runtime UGUI layout contract.
    /// Values are shared by UIManager and by tests; no gameplay logic lives here.
    /// </summary>
    public static class UiLayoutMetrics
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        // A single documented Match Width Or Height value is used in every orientation.
        // With a 1080x1920 portrait reference this keeps a landscape canvas at roughly
        // 1920x1080 reference units (instead of shrinking the usable height to ~700).
        public const float ScalerMatch = 0.5f;

        public const float TopBarHeight01 = 0.12f;
        public const float InstructionMinY01 = 0.78f;
        public const float InstructionMaxY01 = 0.845f;
        public const float HintMinY01 = 0.20f;
        public const float HintMaxY01 = 0.255f;
        public const float BottomBarHeight01 = 0.17f;

        public const float GarageHeaderHeight01 = 0.12f;
        public const float GarageContentSideInsetPortrait01 = 0.06f;
        public const float GarageContentSideInsetLandscape01 = 0.14f;

        public const float MinTouchTargetHeight = 96f;
        public const float ToolButtonPreferredHeight = 104f;

        public static UnityEngine.Vector2 ReferenceResolution
        {
            get { return new UnityEngine.Vector2(ReferenceWidth, ReferenceHeight); }
        }

        /// <summary>Pure function so tests can document the policy without a Canvas.</summary>
        public static float ScalerMatchForAspect(float aspect)
        {
            return ScalerMatch;
        }

        public static float GarageContentSideInset01(float aspect)
        {
            return aspect >= 1f ? GarageContentSideInsetLandscape01 : GarageContentSideInsetPortrait01;
        }

        /// <summary>True when instruction/hint bands leave a gap around the HUD regions.</summary>
        public static bool HudBandsAreSeparated()
        {
            bool topToInstruction = InstructionMaxY01 < 1f - TopBarHeight01;
            bool hintToBottom = HintMinY01 > BottomBarHeight01;
            bool instructionToHint = InstructionMinY01 > HintMaxY01;
            return topToInstruction && hintToBottom && instructionToHint;
        }
    }
}
