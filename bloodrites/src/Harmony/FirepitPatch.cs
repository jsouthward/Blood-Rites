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
        private static readonly Dictionary<BlockPos, double> lastServerCheckByFirepit = new();
        private static readonly Dictionary<BlockPos, double> lastClientFxByFirepit = new();

        [HarmonyPostfix]
        public static void Postfix_OnBurnTick(BlockEntityFirepit __instance, float dt)
        {
            if (__instance?.Api == null) return;
            if (!__instance.IsBurning) return;

            var vesselSlot = __instance.Inventory?[1];
            var stack = vesselSlot?.Itemstack;
            if (stack == null) return;

            if (stack.Collectible is not BlockCookingCauldron cauldron) return;

            float temp = __instance.furnaceTemperature;
            double now = __instance.Api.World.ElapsedMilliseconds;

            // --------------------
            // CLIENT: frequent FX
            // --------------------
            if (__instance.Api.Side == EnumAppSide.Client)
            {
                // ~10 times/sec for smooth bubbling
                if (lastClientFxByFirepit.TryGetValue(__instance.Pos, out double lastFx) && now - lastFx < 100)
                    return;

                lastClientFxByFirepit[__instance.Pos] = now;

                // OnHeated should spawn particles ONLY when Api.Side == Client
                cauldron.OnHeated(__instance, temp);
                return;
            }

            // --------------------
            // SERVER: slow logic
            // --------------------
            if (__instance.Api.Side == EnumAppSide.Server)
            {
                // keep your original 2s throttle to avoid expensive scans too often
                if (lastServerCheckByFirepit.TryGetValue(__instance.Pos, out double last) && now - last < 2000)
                    return;

                lastServerCheckByFirepit[__instance.Pos] = now;

                cauldron.OnHeated(__instance, temp);
            }
        }
    }
}
