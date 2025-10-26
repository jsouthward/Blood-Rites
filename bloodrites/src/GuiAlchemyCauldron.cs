/*using Vintagestory.API.Server;
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
    public class GuiAlchemyCauldron : GuiDialogBlockEntity
    {
        /public GuiAlchemyCauldron(string title, InventoryBase inv, BlockPos pos, ICoreClientAPI capi)
            : base(title, inv, pos, capi)
        {
            SetupDialog();
        }

        void SetupDialog()
        {
            ElementBounds bg = ElementBounds.Fill.WithFixedPadding(10);
            bg.BothSizing = ElementSizing.FitToChildren;

            SingleComposer = capi.Gui.CreateCompo("alchemycauldron", bg)
                .AddDialogTitleBar("Alchemy Cauldron", OnTitleBarClose)
                .AddItemSlotGrid(Inventory, true, 3, 2, 20, 40)
                .AddDynamicText("Brewing...", CairoFont.WhiteSmallText(), EnumTextOrientation.Center, 0, 110)
                .AddAutoSizeDialog()
                .Compose();
        }
    }
}*/