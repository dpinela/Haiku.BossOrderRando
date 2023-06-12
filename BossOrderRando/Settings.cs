using BepConfig = BepInEx.Configuration;

namespace Haiku.BossOrderRando;

internal class Settings
{
    public BepConfig.ConfigEntry<bool> Enable;
    public BepConfig.ConfigEntry<string> Seed;

    public Settings(BepConfig.ConfigFile config)
    {
        Enable = config.Bind("", "Randomize Boss Rush Order", false);
        Seed = config.Bind("", "Seed", "", "Randomization seed (if blank, a new random seed is picked each time the boss rush is entered)");
    }
}