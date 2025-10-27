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
    public class BlockAlchemyCauldron : BlockCookedCauldron
    {
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            return "Alchemy Cauldron";
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Let the firepit handle it (this triggers the firepit GUI)
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}