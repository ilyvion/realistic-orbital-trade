using HarmonyLib;
using Verse;
using System;
using UnityEngine;
using System.Reflection;

namespace RealisticOrbitalTrade;

internal class RealisticOrbitalTradeMod : Mod
{
    private readonly ModContentPack content;

    public RealisticOrbitalTradeMod(ModContentPack content) : base(content)
    {
        this.content = content;

        // if (!ModsConfig.RoyaltyActive)
        // {
        //     Error("Realistic Orbital Trade requires the Royalty DLC to be active to work. The mod's functionality has therefore been disabled and trade will behave as in vanilla.");

        // }
        // else
        // {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());
        //}

        GetSettings<Settings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        Settings.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return content.Name;
    }

    public static void Message(string msg)
    {
        Log.Message("[Realistic Orbital Trade] " + msg);
    }

    public static void Dev(string msg)
    {
        if (Prefs.DevMode)
        {
            Log.Message("[Realistic Orbital Trade][DEV] " + msg);
        }
    }

    public static void Warning(string msg)
    {
        Log.Warning("[Realistic Orbital Trade] " + msg);
    }

    public static void WarningOnce(string msg, int key)
    {
        Log.WarningOnce("[Realistic Orbital Trade] " + msg, Constants.MessageKeyBase + key);
    }

    public static void Error(string msg)
    {
        Log.Error("[Realistic Orbital Trade] " + msg);
    }

    public static void Exception(string msg, Exception? e = null)
    {
        Message(msg);
        if (e != null)
        {
            Log.Error(e.ToString());
        }
    }
}
