using System.Globalization;

namespace RealisticOrbitalTrade
{
    [HotSwappable]
    internal class Settings : ModSettings
    {
        internal static bool _printDevMessages;
        internal static bool _renegotiationWarningShown;
        internal static bool _activeTradePausesDepartureTimer;
        internal static int _minTicksUntilDepartureBeforeGraceTime = 20000;
        internal static int _departureGraceTimeTicks = 40000;
        internal static int _minimumTradeThreshold = 600;
        internal static bool _useMinimumTradeThreshold;
        internal static int _minimumTradeDeviation = 15;
        internal static bool _showIlyvionLaboratoryWarning = true;

        private static string _minimumTradeThresholdBuffer = string.Empty;

        public override void ExposeData()
        {
            base.ExposeData();

            // Meta
            Scribe_Values.Look(ref _printDevMessages, "printDevMessages", false);
            Scribe_Values.Look(ref _renegotiationWarningShown, "renegotiationWarningShown", false);

            // Mod
            Scribe_Values.Look(ref _activeTradePausesDepartureTimer, "activeTradePausesDepartureTimer", false);
            Scribe_Values.Look(ref _minTicksUntilDepartureBeforeGraceTime, "minTicksUntilDeparture", 20000);
            Scribe_Values.Look(ref _departureGraceTimeTicks, "departureGraceTimeTicks", 40000);
            Scribe_Values.Look(ref _minimumTradeThreshold, "minimumTradeThreshold", 600);
            Scribe_Values.Look(ref _useMinimumTradeThreshold, "useMinimumTradeThreshold", false);
            Scribe_Values.Look(ref _minimumTradeDeviation, "minimumTradeDeviation", 15);
            Scribe_Values.Look(
                ref _showIlyvionLaboratoryWarning, "showIlyvionLaboratoryWarning", true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _minimumTradeThresholdBuffer = _minimumTradeThreshold.ToString(CultureInfo.CurrentCulture);
            }
        }

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new();
            listingStandard.Begin(inRect);

            if (Prefs.DevMode)
            {
                listingStandard.CheckboxLabeled("DEV: Print dev log messages (rather verbose)", ref _printDevMessages);
                listingStandard.Gap(4);
            }

            listingStandard.CheckboxLabeled(
                "RealisticOrbitalTrade.UseMinimumTradeThresholdLabel".Translate(),
                ref _useMinimumTradeThreshold,
                "RealisticOrbitalTrade.UseMinimumTradeThresholdTooltip".Translate());

            listingStandard.Label("RealisticOrbitalTrade.MinimumTradeThresholdLabel".Translate());
            listingStandard.TextFieldNumeric(ref _minimumTradeThreshold, ref _minimumTradeThresholdBuffer);

            listingStandard.Label("RealisticOrbitalTrade.MinimumTradeVariationLabel".Translate(_minimumTradeDeviation), -1f, "RealisticOrbitalTrade.MinimumTradeVariationTooltip".Translate(_minimumTradeDeviation));
            _minimumTradeDeviation = (int)listingStandard.Slider(_minimumTradeDeviation, 0, 100);

            listingStandard.Gap(10);

            listingStandard.CheckboxLabeled(
                "RealisticOrbitalTrade.ActiveTradePausesDepartureTimerLabel".Translate(),
                ref _activeTradePausesDepartureTimer,
                "RealisticOrbitalTrade.ActiveTradePausesDepartureTimerTooltip".Translate());

            if (_activeTradePausesDepartureTimer)
            {
                listingStandard.Label("<color=#ffff00>" + "RealisticOrbitalTrade.NoEffectWhenActiveTradePausesDepartureIsEnabled".Translate() + "</color>");
            }

            listingStandard.Gap(4);

            var minHoursUntilDepartureBeforeGraceTime = _minTicksUntilDepartureBeforeGraceTime / 2500f;
            listingStandard.Label(string.Format(CultureInfo.CurrentCulture, "RealisticOrbitalTrade.MinTimeUntilDepartureBeforeGraceTimeLabel".Translate(), minHoursUntilDepartureBeforeGraceTime), -1f,
#if v1_6
                (TipSignal?)
#endif
                "RealisticOrbitalTrade.MinTimeUntilDepartureBeforeGraceTimeTooltip".Translate());
            minHoursUntilDepartureBeforeGraceTime = listingStandard.Slider(minHoursUntilDepartureBeforeGraceTime, 1, 16);
            _minTicksUntilDepartureBeforeGraceTime = (int)(minHoursUntilDepartureBeforeGraceTime * 2500f);

            listingStandard.Gap(4);

            var departureGraceTime = _departureGraceTimeTicks / 2500f;
            listingStandard.Label(string.Format(CultureInfo.CurrentCulture, "RealisticOrbitalTrade.DepartureGraceTimeLabel".Translate(), departureGraceTime), -1f,
#if v1_6
                (TipSignal?)
#endif
                "RealisticOrbitalTrade.DepartureGraceTimeTooltip".Translate());
            departureGraceTime = listingStandard.Slider(departureGraceTime, 4, 24);
            _departureGraceTimeTicks = (int)(departureGraceTime * 2500f);

            listingStandard.End();
        }

        public static int GenerateDeviatingMinimumTradeThreshold()
        {
            var baseline = _minimumTradeThreshold;
            var deviation = baseline * Rand.RangeInclusive(-_minimumTradeDeviation, _minimumTradeDeviation) / 100;

            return Math.Max(0, baseline + deviation);
        }
    }
}
