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
    public class BlockEntityAlchemyCauldron : BlockEntity
    {
        public InventoryGeneric Inventory { get; private set; }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Create a simple 5-slot container for the cauldron
            Inventory ??= new InventoryGeneric(5, "alchemycauldron-" + Pos, api);
        }

        public InventoryBase GetInventory()
        {
            return Inventory;
        }

        public bool CanCook(IWorldAccessor world)
        {
            // For now, allow all cooking attempts
            return true;
        }

        public void OnCookingComplete()
        {
            // Placeholder: add your alchemy logic here later
            Api.Logger.Notification("Alchemy Cauldron finished cooking!");
        }
    }
}
