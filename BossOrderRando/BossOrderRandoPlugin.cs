using Bep = BepInEx;
using MMDetour = MonoMod.RuntimeDetour;
using Reflection = System.Reflection;
using USM = UnityEngine.SceneManagement;

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
    }

    private Settings? modSettings;

    private BossRushMode.BossScene[]? origOrderRing1;
    private BossRushMode.BossScene[]? origOrderRing2;
    private BossRushMode.BossScene[]? origOrderRing3;

    private System.Collections.IEnumerator RandomizeBossOrder(On.EnterRoomTrigger.orig_LoadNextLevel orig, EnterRoomTrigger self)
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
}