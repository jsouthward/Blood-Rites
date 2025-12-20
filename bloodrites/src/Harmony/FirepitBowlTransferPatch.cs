using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace bloodrites.HarmonyLib
{
    [HarmonyPatch(typeof(BlockFirepit), "OnBlockInteractStart")]
    public static class FirepitBowlTransferPatch
    {
        // Design knobs
        private const int BowlTransferPortions = 100;   // per-click transfer (1L)
        private const int CauldronMaxPortions = 300;    // 3L if 100 portions = 1L

        // Your vessel knobs
        private const int VesselTransferPortions = 300; // per-click transfer for your vessel
        private const int VesselMaxPortions = 300;      // capacity of your vessel (adjust if needed)

        // ✅ Your chosen sound: assets/survival/sounds/effect/water-pour.ogg
        // Use asset location (no .ogg, no Windows path)
        private static readonly AssetLocation SoundTakeLiquid = new AssetLocation("game", "sounds/effect/water-pour");
        private static readonly AssetLocation SoundPourLiquid = new AssetLocation("game", "sounds/effect/water-pour");

        [HarmonyPrefix]
        public static bool Prefix(BlockFirepit __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref bool __result)
        {
            if (world?.Api?.Side != EnumAppSide.Server) return true;
            if (blockSel?.Position == null) return true;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFirepit;
            if (be?.Inventory == null) return true;

            // Slot 1 = vessel slot (your cauldron sits here)
            if (be.Inventory.Count <= 1) return true;
            ItemSlot vesselSlot = be.Inventory[1];
            ItemStack? vesselStack = vesselSlot.Itemstack;
            if (vesselStack?.Collectible is not BlockCookingCauldron) return true;

            // Player held slot
            ItemSlot? heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack? heldStack = heldSlot?.Itemstack;
            if (heldStack == null) return true;

            // Only bowls OR your new vessel
            bool isBowl = heldStack.Collectible?.Code?.Path?.Contains("bowl") == true;
            bool isMyVessel = IsMyVessel(heldStack);
            if (!isBowl && !isMyVessel) return true;

            // Clean up weird leftovers so "empty" bowls behave
            if (isBowl) ForceClearNonLiquidBowlContents(heldStack, world);

            // Read cauldron portion stack (tree slot "0")
            ItemStack? cauldronPortionStack = GetCauldronPortionStack(vesselStack);
            cauldronPortionStack?.ResolveBlockOrItem(world);

            // Read held portion stack (bowl or your vessel)
            ItemStack? heldPortionStack = isBowl ? GetBowlPortionStack(heldStack) : GetVesselPortionStack(heldStack);
            heldPortionStack?.ResolveBlockOrItem(world);

            bool heldEmpty = heldPortionStack == null || heldPortionStack.StackSize <= 0 || heldPortionStack.Collectible == null;
            bool cauldronEmpty = cauldronPortionStack == null || cauldronPortionStack.StackSize <= 0 || cauldronPortionStack.Collectible == null;

            // Helpers to write back into held container
            void SetHeldPortionStack(ItemStack portion)
            {
                if (isBowl) SetBowlPortionStack(heldStack, portion);
                else SetVesselPortionStack(heldStack, portion);
            }

            void ClearHeldContents()
            {
                if (isBowl) ClearBowlContents(heldStack);
                else ClearVesselContents(heldStack);
            }

            int TransferPerClick() => isBowl ? BowlTransferPortions : VesselTransferPortions;
            int HeldCapacity() => isBowl ? BowlTransferPortions : VesselMaxPortions; // bowl "capacity" for this logic is exactly 1L

            // =========================
            // A) Empty held -> TAKE from cauldron
            // =========================
            if (heldEmpty)
            {
                if (cauldronPortionStack?.Collectible == null) return true; // cauldron empty

                int transfer = TransferPerClick();
                transfer = Math.Min(transfer, HeldCapacity());

                if (cauldronPortionStack.StackSize < transfer) return true; // not enough

                if (isBowl)
                {
                    // Build filled bowl (always size 1) and fill using tree-slot format
                    ItemStack filledBowl = heldStack.Clone();
                    filledBowl.StackSize = 1;
                    SetBowlLiquidAsTree(filledBowl, cauldronPortionStack.Collectible, transfer);

                    // Consume 1 empty bowl if stacked, give filled bowl separately
                    if (heldStack.StackSize > 1)
                    {
                        heldStack.StackSize -= 1;
                        heldSlot.MarkDirty();

                        if (!byPlayer.InventoryManager.TryGiveItemstack(filledBowl, true))
                        {
                            world.SpawnItemEntity(filledBowl, byPlayer.Entity.Pos.XYZ);
                        }
                    }
                    else
                    {
                        heldSlot.Itemstack = filledBowl;
                        heldSlot.MarkDirty();
                    }
                }
                else
                {
                    // Fill your vessel in-place (assumes it stores liquid as contents/0 + quantity)
                    ItemStack newHeldPortion = new ItemStack(cauldronPortionStack.Collectible, transfer);
                    newHeldPortion.ResolveBlockOrItem(world);
                    SetVesselPortionStack(heldStack, newHeldPortion);
                    heldSlot.MarkDirty();
                }

                // Subtract from cauldron
                cauldronPortionStack.StackSize -= transfer;
                if (cauldronPortionStack.StackSize <= 0)
                {
                    SetCauldronPortionStack(vesselStack, null);
                }
                else
                {
                    SetCauldronPortionStack(vesselStack, cauldronPortionStack);
                }

                vesselSlot.MarkDirty();
                be.MarkDirty(true);

                // 🔊 Sound: taking liquid into container
                PlayLiquidSound(world, SoundTakeLiquid,
                    blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                    byPlayer);

                __result = true;
                return false; // handled
            }

            // =========================
            // B) Filled held -> POUR into cauldron
            // Only if cauldron empty OR liquid matches
            // =========================
            if (heldPortionStack?.Collectible != null)
            {
                // If cauldron not empty, liquid must match
                if (!cauldronEmpty && cauldronPortionStack?.Collectible != null)
                {
                    if (cauldronPortionStack.Collectible.Code.ToString() != heldPortionStack.Collectible.Code.ToString())
                    {
                        return true; // different liquids; let vanilla handle
                    }
                }

                int cauldronQty = cauldronPortionStack?.StackSize ?? 0;
                int space = CauldronMaxPortions - cauldronQty;
                if (space <= 0) return true; // full

                int pour = TransferPerClick();
                if (pour > heldPortionStack.StackSize) pour = heldPortionStack.StackSize;
                if (pour > space) pour = space;
                if (pour <= 0) return true;

                // Add to cauldron (create stack if empty)
                ItemStack newCauldronStack = cauldronPortionStack ?? new ItemStack(heldPortionStack.Collectible, 0);
                newCauldronStack.ResolveBlockOrItem(world);
                newCauldronStack.StackSize = cauldronQty + pour;
                SetCauldronPortionStack(vesselStack, newCauldronStack);

                // Subtract from held
                heldPortionStack.StackSize -= pour;
                if (heldPortionStack.StackSize <= 0)
                {
                    ClearHeldContents();
                }
                else
                {
                    SetHeldPortionStack(heldPortionStack);
                }

                heldSlot.MarkDirty();
                vesselSlot.MarkDirty();
                be.MarkDirty(true);

                // 🔊 Sound: pouring liquid into cauldron
                PlayLiquidSound(world, SoundPourLiquid,
                    blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                    byPlayer);

                __result = true;
                return false; // handled
            }

            return true;
        }

        // ---- Identify your vessel ----
        // Adjust this to match your item/blockitem code.
        private static bool IsMyVessel(ItemStack stack)
        {
            // Example: bloodrites:ritualvessel
            // Change "ritualvessel" to your actual path.
            return stack?.Collectible?.Code?.Domain == "bloodrites"
                && stack.Collectible.Code.Path == "ritualvessel";
        }

        private static void PlayLiquidSound(IWorldAccessor world, AssetLocation sound, double x, double y, double z, IPlayer player)
        {
            // Passing null plays to everyone nearby (bucket/bowl style sounds)
            world.PlaySoundAt(sound, x, y, z, null, randomizePitch: true, range: 16f, volume: 1f);
        }

        // ---------------- Bowl helpers ----------------

        private static ItemStack? GetBowlPortionStack(ItemStack bowlStack)
        {
            var tree = bowlStack.Attributes?["contents"] as ITreeAttribute;
            if (tree == null || !tree.HasAttribute("0")) return null;
            return tree.GetItemstack("0");
        }

        private static void SetBowlPortionStack(ItemStack bowlStack, ItemStack portionStack)
        {
            bowlStack.Attributes ??= new TreeAttribute();

            var tree = bowlStack.Attributes["contents"] as ITreeAttribute ?? new TreeAttribute();
            tree.SetItemstack("0", portionStack);
            bowlStack.Attributes["contents"] = tree;

            bowlStack.Attributes.SetInt("quantity", portionStack.StackSize);
        }

        private static void ClearBowlContents(ItemStack bowlStack)
        {
            bowlStack.Attributes ??= new TreeAttribute();
            bowlStack.Attributes.RemoveAttribute("contents");
            bowlStack.Attributes.SetInt("quantity", 0);
        }

        private static void ForceClearNonLiquidBowlContents(ItemStack bowlStack, IWorldAccessor world)
        {
            ITreeAttribute? attrs = bowlStack.Attributes;
            if (attrs == null) return;

            if (attrs.HasAttribute("contents") && attrs["contents"] is not ITreeAttribute)
            {
                attrs.RemoveAttribute("contents");
                attrs.SetInt("quantity", 0);
                return;
            }

            if (attrs["contents"] is ITreeAttribute tree)
            {
                if (!tree.HasAttribute("0"))
                {
                    attrs.RemoveAttribute("contents");
                    attrs.SetInt("quantity", 0);
                    return;
                }

                ItemStack? s = tree.GetItemstack("0");
                if (s == null)
                {
                    attrs.RemoveAttribute("contents");
                    attrs.SetInt("quantity", 0);
                    return;
                }

                s.ResolveBlockOrItem(world);

                string code = s.Collectible?.Code?.ToString() ?? "";
                bool isLiquidPortion = code.EndsWith("portion");

                if (!isLiquidPortion || s.StackSize <= 0)
                {
                    attrs.RemoveAttribute("contents");
                    attrs.SetInt("quantity", 0);
                }
            }
        }

        private static void SetBowlLiquidAsTree(ItemStack bowlStack, CollectibleObject liquidCollectible, int portions)
        {
            bowlStack.Attributes ??= new TreeAttribute();

            if (portions <= 0)
            {
                bowlStack.Attributes.RemoveAttribute("contents");
                bowlStack.Attributes.SetInt("quantity", 0);
                return;
            }

            var portionStack = new ItemStack(liquidCollectible, portions);

            var tree = new TreeAttribute();
            tree.SetItemstack("0", portionStack);

            bowlStack.Attributes["contents"] = tree;
            bowlStack.Attributes.SetInt("quantity", portions);
        }

        // ---------------- Your vessel helpers ----------------

        private static ItemStack? GetVesselPortionStack(ItemStack vesselStack)
        {
            var tree = vesselStack.Attributes?["contents"] as ITreeAttribute;
            if (tree == null || !tree.HasAttribute("0")) return null;
            return tree.GetItemstack("0");
        }

        private static void SetVesselPortionStack(ItemStack vesselStack, ItemStack? portionStackOrNull)
        {
            vesselStack.Attributes ??= new TreeAttribute();

            if (portionStackOrNull == null || portionStackOrNull.StackSize <= 0)
            {
                vesselStack.Attributes.RemoveAttribute("contents");
                vesselStack.Attributes.SetInt("quantity", 0);
                return;
            }

            var tree = vesselStack.Attributes["contents"] as ITreeAttribute ?? new TreeAttribute();
            tree.SetItemstack("0", portionStackOrNull);
            vesselStack.Attributes["contents"] = tree;

            vesselStack.Attributes.SetInt("quantity", portionStackOrNull.StackSize);
        }

        private static void ClearVesselContents(ItemStack vesselStack)
        {
            vesselStack.Attributes ??= new TreeAttribute();
            vesselStack.Attributes.RemoveAttribute("contents");
            vesselStack.Attributes.SetInt("quantity", 0);
        }

        // ---------------- Cauldron helpers ----------------

        private static ItemStack? GetCauldronPortionStack(ItemStack cauldronStack)
        {
            var tree = cauldronStack.Attributes?["contents"] as ITreeAttribute;
            if (tree == null || !tree.HasAttribute("0")) return null;
            return tree.GetItemstack("0");
        }

        private static void SetCauldronPortionStack(ItemStack cauldronStack, ItemStack? portionStackOrNull)
        {
            cauldronStack.Attributes ??= new TreeAttribute();

            if (portionStackOrNull == null || portionStackOrNull.StackSize <= 0)
            {
                cauldronStack.Attributes.RemoveAttribute("contents");
                cauldronStack.Attributes.SetInt("quantity", 0);
                return;
            }

            var tree = cauldronStack.Attributes["contents"] as ITreeAttribute ?? new TreeAttribute();
            tree.SetItemstack("0", portionStackOrNull);
            cauldronStack.Attributes["contents"] = tree;

            cauldronStack.Attributes.SetInt("quantity", portionStackOrNull.StackSize);
        }
    }
}