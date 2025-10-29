using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using HarmonyLib;


namespace bloodrites
{
    public class bloodritesModSystem : ModSystem
    {

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterBlockClass("BlockCookingCauldron", typeof(BlockCookingCauldron));
            api.Logger.Notification("[BloodRites] Registered Alchemy Firepit + Cauldron");

            var harmony = new Harmony("bloodrites.firepitpatch");
            harmony.PatchAll(); // Applies all [HarmonyPatch] attributes in your mod assembly
            api.Logger.Notification("[BloodRites] Harmony patch applied to BlockEntityFirepit.OnBurnTick()");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("bloodrites:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("bloodrites:hello"));
        }

    }
}
