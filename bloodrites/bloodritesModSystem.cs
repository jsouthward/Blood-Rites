using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace bloodrites
{
    public class bloodritesModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterBlockClass("BlockCookingCauldron", typeof(BlockCookingCauldron));
            //api.Logger.Notification("[BloodRites] Registered Alchemy Firepit + Cauldron");

            var harmony = new Harmony("bloodrites.firepitpatch");
            harmony.PatchAll();
            //api.Logger.Notification("[BloodRites] Harmony patch applied to BlockEntityFirepit.OnBurnTick()");
            
            LoadAlchemyRecipes(api);
            api.Logger.Notification("[BloodRites] Loading Alchemy Recipes...");


        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("bloodrites:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("bloodrites:hello"));
        }

        private void LoadAlchemyRecipes(ICoreAPI api)
        {
            
        }

    }
}
