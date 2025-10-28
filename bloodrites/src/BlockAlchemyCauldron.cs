using Vintagestory.API.Server;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace bloodrites
{
    // Inherits all vanilla pot behavior — inventory slots, firepit compatibility, etc.
    public class BlockAlchemyCauldron : BlockCookingContainer
    {
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            return "Alchemy Cauldron";
        }

        /// <summary>
        /// Called when the player interacts with the cauldron directly in-world.
        /// This usually just defers to the base implementation so firepits open their normal GUI.
        /// </summary>
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        /// <summary>
        /// Called by the firepit to get the renderer for this vessel.
        /// This is where you swap the visual from the pot to your custom cauldron.
        /// </summary>
        public new IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            ICoreClientAPI capi = firepit.Api as ICoreClientAPI;
            return new CauldronInFirepitRenderer(capi, stack, firepit.Pos, forOutputSlot);
        }
    }
}