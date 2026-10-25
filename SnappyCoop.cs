using System;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.Bridge;
using MelonLoader;
using SnappyCoop;
using UnityEngine;

[assembly: MelonInfo(typeof(SnappyCoopMod), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace SnappyCoop;

// Native co-op is lockstep: each action runs InputDelay seconds in the future, where
//   InputDelay = 0.5 * (maxRTT + k) + maxJitter + 0.25
// and 0.25 is the fixed period between "step signoff" broadcasts (NetworkedUnityToSimulation.Update).
// Signing off more often keeps our promise to peers fresher, so they are much less likely to stall waiting on us
// (with 4 players, everyone stalls whenever any one promise runs out). Same wire protocol, so unmodded players are fine.
// Optionally the freed-up padding can be cut from the delay instead, but that spends the jitter headroom; 0.1.0 did
// this by default and it lagged badly in a 4-player game.
public class SnappyCoopMod : BloonsTD6Mod
{
    public const double NativeSignoffPeriod = 0.25;

    public static readonly ModSettingBool Enabled = new(true)
    {
        description = "Sign off simulation steps more often, so other players stall less waiting on you."
    };

    public static readonly ModSettingBool TrimDelay = new(false)
    {
        description = "Also cut the input delay by what faster signoffs save (up to 0.2 s snappier actions). " +
                      "Spends jitter headroom: only for 2 players on stable connections."
    };

    public static readonly ModSettingDouble SignoffPeriod = new(0.05)
    {
        description = "Seconds between step signoffs (native: 0.25). Lower = more headroom against lag spikes " +
                      "(or, with Trim Delay, snappier actions), at the cost of a few more tiny network messages.",
        minValue = 0.02,
        maxValue = NativeSignoffPeriod,
        isSlider = true
    };

    public static readonly ModSettingHotkey StatsHotkey = new(KeyCode.F8)
    {
        description = "Log the current co-op input delay and network stats to the MelonLoader console."
    };

    internal static NetworkedUnityToSimulation Current;
    internal static int CurrentFrame;

    public static bool Active => Enabled && SignoffPeriod < NativeSignoffPeriod;

    // A throwing prefix aborts the game's original call, so patches swallow errors and report the first one.
    private static bool logged;

    internal static void LogOnce(Exception e)
    {
        if (logged) return;
        logged = true;
        MelonLogger.Error(e);
    }

    private static float nextAutoLog;

    public override void OnUpdate()
    {
        // Only while a co-op game is running (Current goes stale after the match).
        if (Current == null || Time.frameCount - CurrentFrame > 1) return;
        // Leave evidence in Latest.log during co-op, so lag can be diagnosed afterwards.
        var auto = Time.unscaledTime >= nextAutoLog;
        if (!auto && !StatsHotkey.JustPressed()) return;
        if (auto) nextAutoLog = Time.unscaledTime + 30;
        LoggerInstance.Msg($"InputDelay {Current.InputDelay * 1000:0} ms ({Current.InputDelayInSteps} steps), " +
                           $"signoff {Current.self.stepSignoff} @ step {Current.simulation.time.elapsed}");
        LoggerInstance.Msg(Current.GetMultiStats());
    }
}

[HarmonyPatch(typeof(NetworkedUnityToSimulation), nameof(NetworkedUnityToSimulation.Update))]
internal static class UpdatePatch
{
    // Reuse the game's own signoff branch (and its gating for pauses, rejoins, resyncs) by making its
    // timer expire early; StepSignoff resets it to 0.
    [HarmonyPrefix]
    private static void Prefix(NetworkedUnityToSimulation __instance)
    {
        SnappyCoopMod.Current = __instance;
        SnappyCoopMod.CurrentFrame = Time.frameCount;
        if (!SnappyCoopMod.Active) return;
        try
        {
            if (__instance.timeSinceLastSignoff + Time.unscaledDeltaTime >= SnappyCoopMod.SignoffPeriod)
                __instance.timeSinceLastSignoff = SnappyCoopMod.NativeSignoffPeriod;
        }
        catch (Exception e)
        {
            SnappyCoopMod.LogOnce(e);
        }
    }
}

[HarmonyPatch(typeof(NetworkedUnityToSimulation), nameof(NetworkedUnityToSimulation.StepSignoff))]
internal static class StepSignoffPatch
{
    private static double lastWritten = double.NaN;

    // StepSignoff (periodic, and on every one of our own actions) promises peers we have no actions before
    // current + InputDelayInSteps, so trim InputDelay right before it reads it.
    [HarmonyPrefix]
    private static void Prefix(NetworkedUnityToSimulation __instance)
    {
        if (!SnappyCoopMod.Active) return;

        try
        {
            var delay = __instance.InputDelay;
            // Update() recomputes InputDelay from latency; only trim a freshly computed value once.
            if (SnappyCoopMod.TrimDelay && delay != lastWritten)
                delay -= SnappyCoopMod.NativeSignoffPeriod - SnappyCoopMod.SignoffPeriod;

            // Never promise an earlier step than already promised (delay drops when latency or fast forward changes),
            // otherwise an action could land on a step a peer has already simulated.
            var stepsPerSecond = 60.0 * Time.timeScale;
            var needed = __instance.self.stepSignoff - __instance.simulation.time.elapsed;
            if (needed > 0) delay = Math.Max(delay, (needed + 0.5) / stepsPerSecond);

            __instance.InputDelay = lastWritten = delay;
        }
        catch (Exception e)
        {
            SnappyCoopMod.LogOnce(e);
        }
    }
}
