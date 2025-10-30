using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    public class BlockCookingCauldron : BlockBucket, IInFirepitRendererSupplier
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

        private static readonly Dictionary<BlockPos, List<string>> absorbedItems = new();

        public void OnHeated(BlockEntityFirepit firepit, float temperature)
        {

            if (firepit == null || firepit.Api == null) return;

            // --- Simple one-time trigger logic using a static HashSet ---
            // This avoids needing Attributes, since those are no longer on the firepit entity.
            string key = firepit.Pos.ToString();

            if (!recentlyHeatedFirepits.Contains(key) && temperature > 200)
            {
                recentlyHeatedFirepits.Add(key);
                firepit.Api.Logger.Notification($"[BloodRites] Alchemy Cauldron {key} Hot! Temp: {temperature}");
            }

            // run logic only when temperature is high enough
            if (temperature > 200)
            {
                // Find all dropped items near the firepit
                var entities = firepit.Api.World.GetEntitiesAround(
                    firepit.Pos.ToVec3d().Add(0.5, 0, 0.5), 1.5f, 1.0f
                );

                // Get or create a local list of absorbed item codes for this firepit
                if (!absorbedItems.TryGetValue(firepit.Pos, out var list))
                {
                    list = new List<string>();
                    absorbedItems[firepit.Pos] = list;
                }

                foreach (var ent in entities)
                {
                    if (ent is EntityItem item && item.Itemstack != null && item.Itemstack.StackSize > 0)
                    {
                        string code = item.Itemstack.Collectible.Code.ToShortString();

                        // Add the item to our memory list
                        list.Add(code);
                        // Log for debugging
                        firepit.Api.Logger.Notification($"[BloodRites] Cauldron absorbed: {code}");
                        // Remove it from the world
                        item.Die();

                        // Bubble burst when an item is absorbed
                        var world = firepit.Api.World;
                        // where the bubbles start (centered above the pit a bit)
                        var basePos = firepit.Pos.ToVec3d().Add(0.35, 0.2, 0.4);
                        // define particles via fields (version-safe)
                        var p = new SimpleParticleProperties()
                        {
                            MinQuantity = 8,
                            AddQuantity = 6,
                            // soft blood-red bubbles
                            Color = ColorUtil.ToRgba(180, 200, 50, 60),
                            MinPos = basePos,
                            AddPos = new Vec3d(0.18, 0.08, 0.18),
                            // gentle upward motion
                            MinVelocity = new Vec3f(0f, 0.12f, 0f),
                            AddVelocity = new Vec3f(0.03f, 0.12f, 0.03f),
                            LifeLength = 1.5f,
                            // keep gravity simple to avoid API differences
                            GravityEffect = 0f,
                            MinSize = 0.1f,
                            MaxSize = 0.32f,
                            ParticleModel = EnumParticleModel.Quad
                        };
                        world.SpawnParticles(p);

                        world.PlaySoundAt(
                            new AssetLocation("game:sounds/effect/extinguish1.ogg"),
                            firepit.Pos.X + 0.5, firepit.Pos.Y + 0.5, firepit.Pos.Z + 0.5,
                            null, 0.4f, 8f
                        );

                    }
                }
            }

        }

        // Static memory to avoid logging spam
        private static readonly HashSet<string> recentlyHeatedFirepits = new();

    }

}