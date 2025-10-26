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
    public class BlockAlchemyCauldron : BlockCookedContainer
    {
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            return Lang.Get("Alchemy Cauldron");
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Open custom GUI later — for now, use normal cookpot GUI
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        // This is the key bit: tell the firepit how to render us
        public IInFirepitRenderer GetInFirepitRenderer(ICoreClientAPI capi, ItemStack stack, BlockPos pos, bool isInOutputSlot)
        {
            return new CauldronInFirepitRenderer(capi, stack, pos, isInOutputSlot);
        }
    }
}

