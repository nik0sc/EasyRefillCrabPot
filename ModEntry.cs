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

        public static bool checkForAction_prefix(Farmer who, ref CrabPot __instance, ref bool __result, bool justCheckingForActivity = false)
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
            // when pickaxe used on placed crab pot with no catch, and the inventory can accept a crab pot, the crab pot is 
            // automatically moved to the inventory (not dropped into the environment then picked up by player) and the bait is 
            // added back into the inventory (either a bait stack in rod or inventory)
            if (t is Pickaxe && !__instance.readyForHarvest)
            {
                Farmer who = Game1.player;
                bool baitRemoved = false;

                if (__instance.bait.Value != null)
                {

                    // Restore bait to stack or rod (inverse of improved baiting action)
                    // Prefer adding to the rod
                    Item baitInPot = __instance.bait.Value;
                    string baitName = baitInPot.Name;
                    foreach (FishingRod rod in who.items.Where(item => item is FishingRod && item.attachmentSlots() > 0))
                    {
                        Item baitSlot = rod.attachments[0] ?? new StardewValley.Object();
                        // TODO: make the bait from crab pot add to the fishing rod bait slot even if it is empty 
                        if (baitSlot.Category == BaitCategory && baitSlot.Stack > 0 && baitSlot.Name == baitInPot.Name)
                        {
                            __instance.bait.Value = null;
                            baitSlot.Stack++;
                            ModEntry.EMonitor?.Log($"Added {baitName} to rod {rod.Name}");
                            baitRemoved = true;
                            break;
                        }
                    }

                    if (!baitRemoved)
                    {
                        // Didn't find a suitable rod
                        if (who.addItemToInventoryBool(baitInPot))
                        {
                            __instance.bait.Value = null;
                            baitRemoved = true;
                            ModEntry.EMonitor?.Log($"Added {baitName} to inventory");
                        }
                        else
                        {
                            //who.dropItem(__instance.bait.Value);
                            //__instance.bait.Value = null;
                            //ModEntry.EMonitor?.Log($"Dropping {baitName} at player's feet");
                            ModEntry.EMonitor?.Log($"No space for {baitName}");
                            Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Crop.cs.588"));
                            return false;
                        }
                    }

                }

                if (baitRemoved)
                {
                    Game1.playSound("coin");
                }

                if (who.addItemToInventoryBool(__instance.getOne(), false))
                {
                    // ripped from CrabPot::checkForAction
                    if (who.isMoving())
                    {
                        Game1.haltAfterCheck = false;
                    }
                    if (!baitRemoved)
                    {
                        // play sound if no bait removed but the pot was removed
                        Game1.playSound("coin");
                    }
                    Game1.currentLocation.objects.Remove(__instance.tileLocation);
                }
                else
                {
                    Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Crop.cs.588"));
                }
            
                return false;
            }

            // Some other tool, let base Object handle it
            return true;
        }
    }
}
