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
    // Minimal working BlockEntity for 1.21.x
    public class BlockEntityAlchemyCauldron : BlockEntity
    {
        public InventoryGeneric? Inventory { get; private set; }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            Inventory ??= new InventoryGeneric(5, "alchemycauldron-" + Pos, api);
        }

        public bool OnInteract(IPlayer player)
        {
            if (Api == null) return false;
            if (Api.Side == EnumAppSide.Client) return true;

            player.InventoryManager.OpenInventory(Inventory);
            return true;
        }

        // Example hook for recipe processing later
        public void CompleteAlchemy(AlchemyRecipeSystem sys)
        {
            var recipe = sys.FindMatchingRecipe(Inventory!);
            if (recipe == null) return;

            Inventory[4].Itemstack = recipe.Output.Clone();
            MarkDirty(true);
            Inventory?.MarkSlotDirty(0);
        }
    }
}