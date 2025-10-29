using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    public class BlockCookingCauldron : Block, IInFirepitRendererSupplier
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            api.Logger.Notification("[BloodRites] Loaded Alchemy Cauldron block class successfully");
        }

        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            ICoreClientAPI capi = (ICoreClientAPI)firepit.Api;
            capi.Logger.Notification("[BloodRites] Creating custom CauldronInFirepitRenderer!");
            return new CauldronInFirepitRenderer(capi, stack, firepit.Pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide; // Matches the shape of your cauldron
        }

        public void OnHeated(BlockEntityFirepit firepit, float temperature)
        {
            if (firepit == null || firepit.Api == null) return;

            // --- Simple one-time trigger logic using a static HashSet ---
            // This avoids needing Attributes, since those are no longer on the firepit entity.
            string key = firepit.Pos.ToString();

            if (!recentlyHeatedFirepits.Contains(key) && temperature > 200)
            {
                recentlyHeatedFirepits.Add(key);
                firepit.Api.Logger.Notification($"[BloodRites] Alchemy Cauldron {key} Is Hot! Temp: {temperature}");
            }

            // Example: run logic only when temperature is high enough
            if (temperature > 200)
            {
                // TODO: alchemy logic here (item detection, bubbling, etc.)
                var entities = firepit.Api.World.GetEntitiesAround(firepit.Pos.ToVec3d().Add(0.5, 0, 0.5), 1.5f, 1.0f);
                foreach (var ent in entities)
                {
                    if (ent is EntityItem item)
                    {
                        firepit.Api.Logger.Notification($"[BloodRites] Item near cauldron: {item.Itemstack.Collectible.Code}");
                        // TODO: implement alchemy reaction
                    }
                }
            }
        }

        // Static memory to avoid logging spam
        private static readonly HashSet<string> recentlyHeatedFirepits = new();

    }

}