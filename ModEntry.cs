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

namespace EasyRefillCrabPot
{
    public class ModEntry : Mod
    {
        private static readonly Random random = new Random();

        public override void Entry(IModHelper helper)
        {
            HarmonyInstance harmony = HarmonyInstance.Create(ModManifest.UniqueID);

            MethodInfo performToolAction_original = AccessTools.Method(typeof(CrabPot), nameof(CrabPot.performToolAction));
            MethodInfo checkForAction_original = AccessTools.Method(typeof(CrabPot), nameof(CrabPot.checkForAction));

            MethodInfo performToolAction_custom = AccessTools.Method(typeof(ModEntry), nameof(performToolAction));
            MethodInfo checkForAction_custom = AccessTools.Method(typeof(ModEntry), nameof(checkForAction));

            harmony.Patch(performToolAction_original, prefix: new HarmonyMethod(performToolAction_custom));
            harmony.Patch(checkForAction_original, prefix: new HarmonyMethod(checkForAction_custom));
        }

        public bool checkForAction(Farmer who, bool justCheckingForActivity = false)
        {
            // if right click on pot with no bait or catch and the inventory contains FishingRod
            // and there is at least one bait item (maybe not wild bait), fill the pot with that bait and remove it from FishingRod
            
            return false;
        }

        public bool performToolAction(Tool t, GameLocation location)
        {
            // when pickaxe used on placed crab pot with no bait or catch, and the inventory can accept a crab pot, the crab pot is 
            // automatically moved to the inventory (not dropped into the environment then picked up by player)
            return false;
        }
    }
}
