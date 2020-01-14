using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;
using Harmony;
using Netcode;

namespace EasyRefillCrabPot
{
    public class ModEntry : Mod
    {
        public static IMonitor EMonitor = null;

        public override void Entry(IModHelper helper)
        {
            EMonitor = Monitor;

            var harmony = HarmonyInstance.Create(ModManifest.UniqueID);

            harmony.Patch(
                original: AccessTools.Method(typeof(CrabPot), nameof(CrabPot.checkForAction)),
                prefix: new HarmonyMethod(typeof(Patch), nameof(Patch.checkForAction_prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(CrabPot), nameof(CrabPot.performToolAction)),
                prefix: new HarmonyMethod(typeof(Patch), nameof(Patch.performToolAction_prefix))
            );

            Monitor.Log("Registered patch", LogLevel.Trace);
        }
    }

    class Patch
    {
        // weapon tile index (as of 1.4.3): 
        // 8 - bamboo pole - level 0 (nothing)
        // 9 - training rod - level 1 (nothing)
        // 10 - fiberglass rod - level 2 (bait)
        // 11 - iridium rod - level 3 (bait and tackle)
        //private static readonly int[] RodTileIndices = { 8, 9, 10, 11 };

        // Why in the world did ConcernedApe name the base SV object "Object"?? The mind boggles
        private static readonly int BaitCategory = StardewValley.Object.baitCategory;

        public static bool checkForAction_prefix(Farmer who, ref CrabPot __instance, bool justCheckingForActivity = false)
        {
            if (justCheckingForActivity)
                return true;

            if (!__instance.readyForHarvest.Value && __instance.bait.Value == null && !who.professions.Contains(Farmer.baitmaster))
            {
                // Nothing to harvest and no bait, not luremaster
                // check inventory for loose bait first, then bait attached to rod
                var resultBait = who.items.FirstOrDefault(item => item.Category == BaitCategory && item.Stack > 0);
                if (resultBait != null)
                {
                    // Get that sweet da-dunk sound
                    bool dropInResult = __instance.performObjectDropInAction(resultBait.getOne(), false, who);
                    if (!dropInResult)
                    {
                        ModEntry.EMonitor?.Log($"performObjectDropInAction failed with {resultBait.Name} x{resultBait.Stack}!", LogLevel.Error);
                        // Let the base code handle this
                        return true;
                    }

                    resultBait.Stack--;
                    ModEntry.EMonitor?.Log($"Took loose bait {resultBait.Name}");
                    if (resultBait.Stack <= 0)
                    {
                        who.removeItemFromInventory(resultBait);
                        ModEntry.EMonitor?.Log("Used last bait in stack, removing from inventory");
                    }
                    return false;
                }

                foreach (FishingRod rod in who.items.Where(item => item is FishingRod && item.attachmentSlots() > 0))
                {
                    // Bait goes in index 0, see FishingRod::attach
                    Item baitSlot = rod.attachments[0] ?? new StardewValley.Object();
                    if (baitSlot.Category == BaitCategory && baitSlot.Stack > 0)
                    {
                        // Take one bait and place in pot
                        bool dropInResult = __instance.performObjectDropInAction(baitSlot.getOne(), false, who);
                        if (!dropInResult)
                        {
                            ModEntry.EMonitor?.Log($"performObjectDropInAction failed with {baitSlot.Name} x{baitSlot.Stack}!", LogLevel.Error);
                            // Let the base code handle this
                            return true;
                        }

                        baitSlot.Stack--;
                        ModEntry.EMonitor?.Log($"Took bait {baitSlot.Name} from {rod.Name}");
                        if (baitSlot.Stack <= 0)
                        {
                            ModEntry.EMonitor?.Log("Used last bait in rod, removing from attachments");
                            rod.attachments[0] = null;
                            // Yoinked from FishingRod::doDoneFishing
                            Game1.showGlobalMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:FishingRod.cs.14085"));
                        }
                        return false;
                    }
                }
                ModEntry.EMonitor?.Log("Can't find loose bait or rod, continuing...");
                return true;
            }
            ModEntry.EMonitor?.Log("Pot doesn't need baiting, continuing...");
            return true;
        }

        public static bool performToolAction_prefix(Tool t, GameLocation location, ref CrabPot __instance)
        {
            // when pickaxe used on placed crab pot with no bait or catch, and the inventory can accept a crab pot, the crab pot is 
            // automatically moved to the inventory (not dropped into the environment then picked up by player)
            return false;
        }
    }
}
