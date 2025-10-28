using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    public class BlockCookingCauldron : Block, IInFirepitRendererSupplier
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            api.Logger.Notification("[BloodRites] Loaded Alchemy Cauldron block class successfully");
        }

        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            ICoreClientAPI capi = firepit.Api as ICoreClientAPI;
            capi.Logger.Notification("[BloodRites] Creating custom CauldronInFirepitRenderer!");
            return new CauldronInFirepitRenderer(capi, stack, firepit.Pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide; // Matches the shape of your cauldron
        }

    }
}