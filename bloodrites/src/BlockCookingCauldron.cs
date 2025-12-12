using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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
            return new CauldronInFirepitRenderer(capi, stack, firepit.Pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide;
        }

        // --------------------------------------------------------------------

        private static readonly Dictionary<BlockPos, List<string>> absorbedItems = new();

        private static readonly List<AlchemyRecipe> Recipes = new()
        {
            new AlchemyRecipe {
                Code = "bloodbroth",
                Ingredients = new Dictionary<string,int> {
                    { "game:redmeat-raw", 1 },
                    { "game:salt", 1 }
                },
                OutputLiquidCode = "bloodrites:bloodportion",
                OutputLitres = 3f
            }
        };

        // --------------------------------------------------------------------

        public void OnHeated(BlockEntityFirepit firepit, float temperature)
        {
            if (firepit?.Api?.Side != EnumAppSide.Server) return;
            if (temperature <= 200) return;

            var vesselSlot = firepit.Inventory?[1];
            var cauldronStack = vesselSlot?.Itemstack;
            if (cauldronStack == null) return;

            if (!absorbedItems.TryGetValue(firepit.Pos, out var list))
            {
                list = new List<string>();
                absorbedItems[firepit.Pos] = list;
            }

            bool absorbedAnything = false;

            var entities = firepit.Api.World.GetEntitiesAround(
                firepit.Pos.ToVec3d().Add(0.5, 0, 0.5), 1.5f, 1.0f
            );

            foreach (var ent in entities)
            {
                if (ent is not EntityItem item || item.Itemstack == null) continue;

                string code = item.Itemstack.Collectible.Code.ToString();
                list.Add(code);
                absorbedAnything = true;

                firepit.Api.Logger.Notification($"[BloodRites] Cauldron absorbed: {code}");
                item.Die();

                SpawnBubbleEffect(firepit);
            }

            if (absorbedAnything)
            {
                if (TryApplyRecipe(firepit, vesselSlot!, list))
                {
                    firepit.MarkDirty(true);
                }
            }
        }

        // --------------------------------------------------------------------

        private static bool TryApplyRecipe(BlockEntityFirepit firepit, ItemSlot vesselSlot, List<string> absorbedList)
        {
            var counts = new Dictionary<string, int>();
            foreach (var c in absorbedList)
            {
                if (!counts.TryAdd(c, 1)) counts[c]++;
            }

            foreach (var r in Recipes)
            {
                bool ok = true;
                foreach (var req in r.Ingredients)
                {
                    if (!counts.TryGetValue(req.Key, out int have) || have < req.Value) { ok = false; break; }
                }
                if (!ok) continue;

                // --- IMPORTANT: clone + replace stack so it syncs ---
                var oldStack = vesselSlot.Itemstack;
                if (oldStack == null) return false;

                var newStack = oldStack.Clone();                 // new instance -> network notices
                if (!SetContainerContents(firepit.Api, newStack, r.OutputLiquidCode, r.OutputLitres))
                    return false;

                vesselSlot.Itemstack = newStack;
                vesselSlot.MarkDirty();

                // also mark the BE dirty (belt & braces)
                firepit.MarkDirty(true);
                firepit.Api.World.BlockAccessor.MarkBlockEntityDirty(firepit.Pos);

                absorbedList.Clear();

                firepit.Api.Logger.Notification(
                    $"[BloodRites] Recipe '{r.Code}' -> liquid '{r.OutputLiquidCode}' ({r.OutputLitres}L)"
                );

                return true;
            }

            return false;
        }

        // --------------------------------------------------------------------
        // IMPORTANT: For BlockBucket/BlockContainer, use "contents" + "quantity" (int portions)
        // Typical convention: 100 portions = 1 litre. So 3L => 300 portions.
        private static bool SetContainerContents(ICoreAPI api, ItemStack vesselStack, string liquidCode, float litres)
        {
            vesselStack.Attributes ??= new Vintagestory.API.Datastructures.TreeAttribute();

            // 1) Resolve liquid collectible (usually an Item: e.g. game:waterportion)
            var loc = new AssetLocation(liquidCode);

            CollectibleObject? coll = api.World.GetItem(loc);
            coll ??= api.World.GetBlock(loc);

            if (coll == null)
            {
                api.Logger.Warning("[BloodRites] Liquid collectible not found: {0}", liquidCode);
                return false;
            }

            // 2) Build the stored liquid stack (type only; quantity is stored separately as "quantity")
            var liquidStack = new ItemStack(coll, 1);
            liquidStack.ResolveBlockOrItem(api.World);

            // 3) IMPORTANT: store "contents" as an ItemstackAttribute (SetItemstack does this)
            vesselStack.Attributes.SetItemstack("contents", liquidStack);

            // 4) Buckets use "quantity" as portions (100 portions per litre is a common pattern)
            int portions = (int)GameMath.Clamp(litres * 100f, 0f, 300f);
            vesselStack.Attributes.SetInt("quantity", portions);

            // Optional: if you want to debug what’s actually stored
            api.Logger.Notification($"[BloodRites] Set contents={liquidCode}, quantity={portions}");

            return true;
        }

        private static void SpawnBubbleEffect(BlockEntityFirepit firepit)
        {
            var world = firepit.Api.World;
            var basePos = firepit.Pos.ToVec3d().Add(0.35, 0.2, 0.4);

            var p = new SimpleParticleProperties()
            {
                MinQuantity = 8,
                AddQuantity = 6,
                Color = ColorUtil.ToRgba(180, 200, 50, 60),
                MinPos = basePos,
                AddPos = new Vec3d(0.18, 0.08, 0.18),
                MinVelocity = new Vec3f(0f, 0.12f, 0f),
                AddVelocity = new Vec3f(0.03f, 0.12f, 0.03f),
                LifeLength = 1.5f,
                GravityEffect = 0f,
                MinSize = 0.1f,
                MaxSize = 0.32f,
                ParticleModel = EnumParticleModel.Quad
            };

            world.SpawnParticles(p);
        }
    }
}