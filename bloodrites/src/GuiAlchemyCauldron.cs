using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System;

namespace bloodrites
{
    public class GuiAlchemyCauldron : GuiDialogBlockEntity
    {
        public GuiAlchemyCauldron(InventoryBase inventory, BlockPos pos, ICoreClientAPI capi)
            : base("Alchemy Cauldron", inventory, pos, capi)
        {
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle);

            ElementBounds bg = ElementBounds.Fill.WithFixedPadding(10);
            bg.BothSizing = ElementSizing.FitToChildren;

            ElementBounds titleBounds = ElementBounds.Fixed(0, 0, 220, 20);
            ElementBounds closeBtnBounds = ElementBounds.Fixed(230, 0, 20, 20);
            ElementBounds slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, 5, 1);
            ElementBounds statusBounds = ElementBounds.Fixed(0, 90, 220, 20);
        }

        private bool OnCloseClicked()
        {
            TryClose();
            return true;
        }
    }
}