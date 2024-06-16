using UnityEngine;
using Verse;

namespace RealisticOrbitalTrade
{
    internal class Settings : ModSettings
    {
        internal static bool _printDevMessages = false;
        internal static bool _renegotiationWarningShown = false;
        internal static bool _activeTradePausesDepartureTimer = false;
        internal static int _minTicksUntilDepartureBeforeGraceTime = 20000;
        internal static int _departureGraceTimeTicks = 40000;

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
        }

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new();
            listingStandard.Begin(inRect);

            if (Prefs.DevMode)
            {
                listingStandard.CheckboxLabeled("DEV: Print dev log messages (rather verbose)", ref _printDevMessages);
            }

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
            listingStandard.Label(string.Format("RealisticOrbitalTrade.MinTimeUntilDepartureBeforeGraceTimeLabel".Translate(), minHoursUntilDepartureBeforeGraceTime), -1f, "RealisticOrbitalTrade.MinTimeUntilDepartureBeforeGraceTimeTooltip".Translate());
            minHoursUntilDepartureBeforeGraceTime = listingStandard.Slider(minHoursUntilDepartureBeforeGraceTime, 1, 16);
            _minTicksUntilDepartureBeforeGraceTime = (int)(minHoursUntilDepartureBeforeGraceTime * 2500f);

            listingStandard.Gap(4);

            var departureGraceTime = _departureGraceTimeTicks / 2500f;
            listingStandard.Label(string.Format("RealisticOrbitalTrade.DepartureGraceTimeLabel".Translate(), departureGraceTime), -1f, "RealisticOrbitalTrade.DepartureGraceTimeTooltip".Translate());
            departureGraceTime = listingStandard.Slider(departureGraceTime, 4, 24);
            _departureGraceTimeTicks = (int)(departureGraceTime * 2500f);

            listingStandard.End();
        }
    }
}
