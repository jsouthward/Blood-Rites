using HarmonyLib;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites.HarmonyLib
{
    [HarmonyPatch(typeof(BlockEntityFirepit), "OnBurnTick")]
    public static class FirepitPatch
    {
        private static readonly Dictionary<BlockPos, double> lastCheckByFirepit = new();

        [HarmonyPostfix]
        public static void Postfix_OnBurnTick(BlockEntityFirepit __instance, float dt)
        {
            if (__instance?.Api?.Side != EnumAppSide.Server) return;
            if (!__instance.IsBurning) return;

            double now = __instance.Api.World.ElapsedMilliseconds;
            if (lastCheckByFirepit.TryGetValue(__instance.Pos, out double last) && now - last < 2000)
                return; // skip until 2 seconds passed

            lastCheckByFirepit[__instance.Pos] = now;

            var vesselSlot = __instance.Inventory?[1];
            var stack = vesselSlot?.Itemstack;
            if (stack == null) return;

            if (stack.Collectible is BlockCookingCauldron cauldron)
            {
                float temp = __instance.furnaceTemperature;
                cauldron.OnHeated(__instance, temp);
            }
        }
    }
}