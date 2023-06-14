using Bep = BepInEx;
using MMDetour = MonoMod.RuntimeDetour;
using MMCil = MonoMod.Cil;
using Cil = Mono.Cecil.Cil;
using static MonoMod.Cil.ILPatternMatchingExt;
using static MonoMod.Utils.Extensions;
using Reflection = System.Reflection;
using UE = UnityEngine;
using USM = UnityEngine.SceneManagement;
using static DG.Tweening.ShortcutExtensions;
using static DG.Tweening.TweenSettingsExtensions;
using Coll = System.Collections;

namespace Haiku.BossOrderRando;

[Bep.BepInPlugin("haiku.bossorderrando", "Haiku Boss Order Rando", "1.0.0.0")]
[Bep.BepInDependency("haiku.mapi", "1.0")]
public class BossOrderRandoPlugin : Bep.BaseUnityPlugin
{
    public void Start()
    {
        modSettings = new(Config);

        //new MMDetour.Hook(typeof(BossRushVisuals).GetMethod("UpdateSelectedVisuals", Reflection.BindingFlags.Public | Reflection.BindingFlags.Instance), RandomizeBossOrder);
        On.EnterRoomTrigger.LoadNextLevel += RandomizeBossOrder;

        var flags = Reflection.BindingFlags.NonPublic | Reflection.BindingFlags.Instance;
        var pubflags = Reflection.BindingFlags.Public | Reflection.BindingFlags.Instance;

        new MMDetour.Hook(typeof(SentientDefeated).GetMethod("BossRushEndingSequence", flags),
            NonterminalTrioBossRushEndingSequence);
        new MMDetour.ILHook(System.Type.GetType("UncorruptVirusDefeated+<BossRushEndingSequence>d__21, Assembly-CSharp").GetMethod("MoveNext", flags),
            NonterminalAtomBossRushEndingSequence);
        new MMDetour.ILHook(typeof(UncorruptVirusBoss).GetMethod("TakeDamage", pubflags), NonterminalAtomIframes);
        new MMDetour.ILHook(typeof(UncorruptVirusBoss).GetMethod("TakeNonSwordDamage", pubflags), NonterminalAtomIframes);
        new MMDetour.ILHook(System.Type.GetType("ReactorCoreDeath+<DeathSequence>d__15, Assembly-CSharp").GetMethod("MoveNext", flags), NonterminalElegyEnding);
        new MMDetour.ILHook(typeof(TheVirus).GetMethod("TakeNonSwordDamage", pubflags), NonterminalAtomIframes);
        IL.TheVirus.TakeDamage += NonterminalAtomIframes;
        IL.FightManager.CheckForDefeatAnimation += NonterminalAtomIframes;
    }

    private Settings? modSettings;

    private BossRushMode.BossScene[]? origOrderRing1;
    private BossRushMode.BossScene[]? origOrderRing2;
    private BossRushMode.BossScene[]? origOrderRing3;

    private Coll.IEnumerator RandomizeBossOrder(On.EnterRoomTrigger.orig_LoadNextLevel orig, EnterRoomTrigger self)
    {
        try
        {
            if (!(modSettings!.Enable.Value && USM.SceneManager.GetActiveScene().buildIndex == 249))
            {
                return orig(self);
            }

            var seed = modSettings!.Seed.Value;
            if (seed == "")
            {
                seed = System.DateTime.Now.Ticks.ToString();
            }
            var brm = BossRushMode.instance;
            var ring = brm.GetRing();
            Logger.LogInfo($"randomizing boss rush order of ring {ring} with seed {seed}");
            switch (ring)
            {
                case 1:
                    RestoreOriginalOrder(ref origOrderRing1, brm.ring1FightSequence);
                    ShuffleBosses(brm.ring1FightSequence, seed);
                    self.levelToLoad = brm.ring1FightSequence[0].sceneIndex;
                    break;
                case 2:
                    RestoreOriginalOrder(ref origOrderRing2, brm.ring2FightSequence);
                    ShuffleBosses(brm.ring2FightSequence, seed);
                    self.levelToLoad = brm.ring2FightSequence[0].sceneIndex;
                    break;
                case 3:
                    RestoreOriginalOrder(ref origOrderRing3, brm.ring3FightSequence);
                    ShuffleBosses(brm.ring3FightSequence, seed);
                    self.levelToLoad = brm.ring3FightSequence[0].sceneIndex;
                    break;
                default:
                    Logger.LogWarning($"unknown boss rush ring {ring} starting; not randomizing");
                    break;
            }
        }
        catch (System.Exception err)
        {
            Logger.LogError(err.ToString());
        }

        return orig(self);
    }

    private static void RestoreOriginalOrder(ref BossRushMode.BossScene[]? slot, BossRushMode.BossScene[] order)
    {
        if (slot == null)
        {
            slot = (BossRushMode.BossScene[])order.Clone();
        }
        else
        {
            System.Array.Copy(slot, order, slot.Length);
        }
    }

    private static void ShuffleBosses(BossRushMode.BossScene[] order, string seed)
    {
        var rng = new RNG(seed);
        for (var i = 0; i < order.Length - 1; i++)
        {
            var j = i + (int)rng.NextBounded((ulong)(order.Length - i));
            var x = order[i];
            order[i] = order[j];
            order[j] = x;
        }
    }

    private Coll.IEnumerator NonterminalTrioBossRushEndingSequence(
        System.Func<SentientDefeated, Coll.IEnumerator> orig, SentientDefeated self
    )
    {
        if (IsEndingVanilla(origOrderRing2, BossRushMode.instance.ring2FightSequence))
        {
            return orig(self);
        }
        return self.BossRushShortEndingSequence();
    }

    private void NonterminalAtomBossRushEndingSequence(MMCil.ILContext il)
    {
        System.Func<bool> IsTerminal = () => IsEndingVanilla(origOrderRing3, BossRushMode.instance.ring3FightSequence);

        var c = new MMCil.ILCursor(il);

        // skip over calls to DisableBossMusic and ChangeBackgroundMusic
        c.GotoNext(MMCil.MoveType.Before, i => i.MatchLdsfld(typeof(SoundManager), "instance"));
        c.EmitDelegate(IsTerminal);
        var label = c.DefineLabel();
        c.Emit(Cil.OpCodes.Brfalse, label);
        c.GotoNext(MMCil.MoveType.After, i => i.MatchCallvirt(typeof(SoundManager), "ChangeBackgroundMusic"));
        c.MarkLabel(label);

        // skip over calls to DisableAllFiveLayers and StopLowHealth
        c.GotoNext(MMCil.MoveType.Before, i => i.MatchLdsfld(typeof(SoundManager), "instance"));
        c.EmitDelegate(IsTerminal);
        label = c.DefineLabel();
        c.Emit(Cil.OpCodes.Brfalse, label);
        c.GotoNext(MMCil.MoveType.After, i => i.MatchCallvirt(typeof(SoundManager), "StopLowHealth"));
        c.MarkLabel(label);

        // skip over call to ShowUICanvas
        c.GotoNext(MMCil.MoveType.Before, i => i.MatchLdsfld(typeof(CameraBehavior), "instance"));
        c.EmitDelegate(IsTerminal);
        label = c.DefineLabel();
        c.Emit(Cil.OpCodes.Brfalse, label);
        c.GotoNext(MMCil.MoveType.After, i => i.MatchCallvirt(typeof(CameraBehavior), "ShowUICanvas"));
        c.MarkLabel(label);
    }

    private void NonterminalAtomIframes(MMCil.ILContext il)
    {
        var c = new MMCil.ILCursor(il);
        c.GotoNext(
            MMCil.MoveType.Before,
            i => i.MatchLdcR4(60),
            i => i.MatchCallvirt(typeof(PlayerScript), "InvulnerableFor"));
        c.Index++;
        c.EmitDelegate((System.Func<float, float>)
            (orig => IsEndingOfCurrentRingVanilla() ? orig : 10));
    }

    private void NonterminalElegyEnding(MMCil.ILContext il)
    {
        // Skip the part of this method that disables the music if in a boss rush and not the last
        // boss.
        var c = new MMCil.ILCursor(il);
        c.GotoNext(MMCil.MoveType.Before, 
            i => i.MatchLdsfld(typeof(SoundManager), "instance"),
            i => i.MatchLdcR4(3),
            i => i.MatchCallvirt(typeof(SoundManager), "DisableBossMusic"));
        var label = c.DefineLabel();
        c.EmitDelegate((System.Func<bool>)ShouldSoundEndAfterElegy);
        c.Emit(Cil.OpCodes.Brfalse, label);
        c.GotoNext(MMCil.MoveType.After,
            i => i.MatchCallvirt(typeof(SoundManager), "DisableAllFiveLayers"));
        c.MarkLabel(label);
    }

    private static bool IsEndingVanilla(BossRushMode.BossScene[]? orig, BossRushMode.BossScene[] actual) =>
        orig == null || orig[orig.Length - 1].sceneIndex == actual[actual.Length - 1].sceneIndex;

    private bool IsEndingOfCurrentRingVanilla()
    {
        var brm = BossRushMode.instance;
        if (!brm.bossRushIsActive)
        {
            return true;
        }
        return brm.GetRing() switch
        {
            1 => IsEndingVanilla(origOrderRing1, brm.ring1FightSequence),
            2 => IsEndingVanilla(origOrderRing2, brm.ring2FightSequence),
            3 => IsEndingVanilla(origOrderRing3, brm.ring3FightSequence),
            _ => true
        };
    }

    private const int ElegyBRRoom = 268;

    private static bool ShouldSoundEndAfterElegy()
    {
        var brm = BossRushMode.instance;
        if (!brm.bossRushIsActive)
        {
            return true;
        }
        return brm.GetRing() switch
        {
            1 => Last(brm.ring1FightSequence).sceneIndex == ElegyBRRoom,
            3 => Last(brm.ring3FightSequence).sceneIndex == ElegyBRRoom,
            _ => true
        };
    }

    private static T Last<T>(T[] xs) => xs[xs.Length - 1];
}