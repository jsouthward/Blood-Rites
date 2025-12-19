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
        private static readonly Dictionary<BlockPos, double> recipeOkSinceMs = new();
        private static readonly Dictionary<BlockPos, string> recipeOkCode = new();
        private const double BubbleDelayMs = 5_000;
        private static readonly AssetLocation SoundBurn = new AssetLocation("game", "sounds/effect/extinguish1");
        private static readonly AssetLocation SoundSuccess = new AssetLocation("game", "sounds/tutorialstepsuccess");

        private static void PlaySound(IWorldAccessor world, AssetLocation sound, BlockPos pos)
        {
            world.PlaySoundAt(
                sound,
                pos.X + 0.5,
                pos.Y + 0.5,
                pos.Z + 0.5,
                null, 
                randomizePitch: true,
                range: 16f,
                volume: 1f
            );
        }
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
                InputLiquidCode = "game:waterportion",
                InputLiquidPortions = 300,
                OutputLiquidCode = "bloodrites:bloodportion",
                OutputLitres = 300
            }
        };

        // --------------------------------------------------------------------

        private static bool CauldronHasLiquid(ItemStack cauldronStack)
        {
            var tree = cauldronStack.Attributes?["contents"] as ITreeAttribute;
            if (tree == null || !tree.HasAttribute("0")) return false;

            var s = tree.GetItemstack("0");
            return s != null && s.StackSize > 0;
        }

        public void OnHeated(BlockEntityFirepit firepit, float temperature)
        {
            if (firepit?.Api == null) return;

            var vesselSlot = firepit.Inventory?[1];
            var cauldronStack = vesselSlot?.Itemstack;

            // CLIENT: visuals only
            if (firepit.Api.Side == EnumAppSide.Client)
            {
                if (temperature > 500 && cauldronStack != null && CauldronHasLiquid(cauldronStack))
                {
                    double now = firepit.Api.World.ElapsedMilliseconds;
                    if (!lastBoilFxByPos.TryGetValue(firepit.Pos, out double last) || now - last >= 80)
                    {
                        lastBoilFxByPos[firepit.Pos] = now;
                        SpawnBoilEffect(firepit);
                    }
                }

                return;
            }

            // SERVER: gameplay only
            if (temperature <= 500) return;

            if (cauldronStack == null) return;

            if (!absorbedItems.TryGetValue(firepit.Pos, out var list))
            {
                list = new List<string>();
                absorbedItems[firepit.Pos] = list;
            }

            bool absorbedAnything = false;

            var entities = firepit.Api.World.GetEntitiesAround(
                firepit.Pos.ToVec3d().Add(0.5, 0, 0.5), 1f, 1.0f
            );

            foreach (var ent in entities)
            {
                if (ent is not EntityItem item || item.Itemstack == null) continue;

                string code = item.Itemstack.Collectible.Code.ToString();
                list.Add(code);
                absorbedAnything = true;

                firepit.Api.Logger.Notification($"[BloodRites] Cauldron absorbed: {code}");
                item.Die();

                PlaySound(firepit.Api.World, SoundBurn, firepit.Pos);
                SpawnBubbleEffect(firepit); // server-side
            }

            if (absorbedAnything)
            {
                if (TryApplyRecipe(firepit, vesselSlot!, list))
                {
                    firepit.MarkDirty(true);
                }
            }

            // Always evaluate recipes while heated, so the timer can progress
            if (TryApplyRecipe(firepit, vesselSlot!, list))
            {
                firepit.MarkDirty(true);
            }
        }

        // --------------------------------------------------------------------

        private static bool TryApplyRecipe(BlockEntityFirepit firepit, ItemSlot vesselSlot, List<string> absorbedList)
        {
            var api = firepit.Api;
            double now = api.World.ElapsedMilliseconds;

            // Count absorbed items
            var counts = new Dictionary<string, int>();
            foreach (var c in absorbedList)
            {
                if (!counts.TryAdd(c, 1)) counts[c]++;
            }

            AlchemyRecipe? matched = null;

            foreach (var r in Recipes)
            {
                // Ingredient check
                bool ok = true;
                foreach (var req in r.Ingredients)
                {
                    if (!counts.TryGetValue(req.Key, out int have) || have < req.Value) { ok = false; break; }
                }
                if (!ok) continue;

                // Input liquid check (contents tree slot "0")
                if (!string.IsNullOrEmpty(r.InputLiquidCode) && r.InputLiquidPortions > 0)
                {
                    var stack = vesselSlot.Itemstack;
                    var contentsTree = stack?.Attributes?["contents"] as ITreeAttribute;
                    if (contentsTree == null || !contentsTree.HasAttribute("0")) continue;

                    ItemStack inLiquidStack = contentsTree.GetItemstack("0");
                    if (inLiquidStack == null) continue;

                    inLiquidStack.ResolveBlockOrItem(api.World);

                    string? inCode = inLiquidStack.Collectible?.Code?.ToString();
                    int inPortions = inLiquidStack.StackSize;

                    if (inCode != r.InputLiquidCode) continue;
                    if (inPortions < r.InputLiquidPortions) continue;
                }

                matched = r;
                break;
            }

            // If no recipe matches right now -> cancel any pending timer
            if (matched == null)
            {
                recipeOkSinceMs.Remove(firepit.Pos);
                recipeOkCode.Remove(firepit.Pos);
                return false;
            }

            // Recipe matches right now -> bubble while "correct"
            SpawnBubbleEffect(firepit);

            // Start timer if not already started (or if different recipe became correct)
            if (!recipeOkSinceMs.TryGetValue(firepit.Pos, out double startMs) ||
                !recipeOkCode.TryGetValue(firepit.Pos, out string code) ||
                code != matched.Code)
            {
                recipeOkSinceMs[firepit.Pos] = now;
                recipeOkCode[firepit.Pos] = matched.Code;
                return false;
            }

            // If 10 seconds have not yet passed, keep bubbling but do not craft
            if (now - startMs < BubbleDelayMs) return false;

            // 10 seconds passed with recipe continuously correct -> apply output
            var oldStack = vesselSlot.Itemstack;
            PlaySound(firepit.Api.World, SoundSuccess, firepit.Pos);
            if (oldStack == null) return false;

            var newStack = oldStack.Clone();
            if (!SetContainerContents(api, newStack, matched.OutputLiquidCode, matched.OutputLitres))
                return false;

            vesselSlot.Itemstack = newStack;
            vesselSlot.MarkDirty();

            firepit.MarkDirty(true);
            api.World.BlockAccessor.MarkBlockEntityDirty(firepit.Pos);

            absorbedList.Clear();

            recipeOkSinceMs.Remove(firepit.Pos);
            recipeOkCode.Remove(firepit.Pos);

            api.Logger.Notification($"[BloodRites] Recipe '{matched.Code}' -> '{matched.OutputLiquidCode}' after 10s");
            return true;
        }

        // --------------------------------------------------------------------
        // IMPORTANT: For BlockBucket/BlockContainer, use "contents" + "quantity" (int portions)
        // Typical convention: 100 portions = 1 litre. So 3L => 300 portions.
        private static bool SetContainerContents(ICoreAPI api, ItemStack vesselStack, string liquidCode, float litres)
        {
            vesselStack.Attributes ??= new TreeAttribute();

            var loc = new AssetLocation(liquidCode);

            CollectibleObject? coll = api.World.GetItem(loc) as CollectibleObject;
            if (coll == null)
            {
                coll = api.World.GetBlock(loc) as CollectibleObject;
            }

            if (coll == null)
            {
                api.Logger.Warning("[BloodRites] Liquid collectible not found: {0}", liquidCode);
                return false;
            }

            int portions = (int)GameMath.Clamp(litres * 100f, 0f, 300f);

            var liquidStack = new ItemStack(coll, portions);
            liquidStack.ResolveBlockOrItem(api.World);

            var contentsTree = new TreeAttribute();
            contentsTree.SetItemstack("0", liquidStack);

            vesselStack.Attributes["contents"] = contentsTree;

            // optional safety for bucket-style logic
            vesselStack.Attributes.SetInt("quantity", portions);

            api.Logger.Notification($"[BloodRites] Set contents tree slot0={liquidCode}, portions={portions}");
            return true;
        }

        private static void SpawnBubbleEffect(BlockEntityFirepit firepit)
        {
            //if (firepit?.Api?.Side != EnumAppSide.Client) return;
            var world = firepit.Api.World;
            var basePos = firepit.Pos.ToVec3d().Add(0.35, 0.2, 0.4);

            var p = new SimpleParticleProperties()
            {
                MinQuantity = 8,
                AddQuantity = 6,
                Color = ColorUtil.ToRgba(140, 140, 140, 60),
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

        private static readonly Dictionary<BlockPos, double> lastBoilFxByPos = new();

        private static void SpawnBoilEffect(BlockEntityFirepit firepit)
        {
            if (firepit?.Api?.Side != EnumAppSide.Client) return;
            var world = firepit.Api.World;
            var basePos = firepit.Pos.ToVec3d().Add(0.35, 0.45, 0.35);

            var p = new SimpleParticleProperties()
            {
                MinQuantity = 2,
                AddQuantity = 3,
                Color = ColorUtil.ToRgba(200, 225, 225, 30),

                // Spread across the cauldron surface
                MinPos = basePos,
                AddPos = new Vec3d(0.26, 0.01, 0.26),

                // No rise = “forming in place”
                MinVelocity = new Vec3f(0f, 0f, 0f),
                AddVelocity = new Vec3f(0f, 0f, 0f),
                GravityEffect = 0f,

                // Short life = pop
                LifeLength = 0.35f,

                // Start small...
                MinSize = 0.03f,
                MaxSize = 0.06f,

                // ...then grow and fade quickly (bubble pop)
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.18f),
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -255f),

                ParticleModel = EnumParticleModel.Quad
            };

            world.SpawnParticles(p);
        }
    }
}