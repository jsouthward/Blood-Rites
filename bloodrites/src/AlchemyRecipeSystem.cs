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
using System.Linq;

namespace bloodrites
{
    public class AlchemyRecipeSystem : ModSystem
    {
        private List<AlchemyRecipe> recipes = new();

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            // Convert the array returned by GetMany() to a List<IAsset>
            var assets = new List<IAsset>(api.Assets.GetMany("bloodrites:recipes/alchemy"));

            foreach (var asset in assets)
            {
                var json = asset.ToObject<JsonObject>();
                recipes.Add(new AlchemyRecipe
                {
                    Code = json["code"].AsString(),
                    // Convert the ingredients array to a List<AssetLocation>
                    Ingredients = json["ingredients"].AsArray<AssetLocation>().ToList(),
                    Output = new ItemStack(api.World.GetItem(new AssetLocation(json["output"].AsString()))),
                    CookTime = json["cookTime"].AsFloat(200)
                });
            }
        }

        public AlchemyRecipe? FindMatchingRecipe(InventoryBase inv)
        {
            var inputs = inv.Where(slot => slot?.Itemstack != null && !slot.Empty)
                            .Select(slot => slot.Itemstack.Collectible.Code)
                            .ToList();

            foreach (var r in recipes)
            {
                if (r.Ingredients.All(i => inputs.Contains(i)))
                    return r;
            }

            return null;
        }
    }

    public class AlchemyRecipe
    {
        public string Code { get; set; } = "";
        public List<AssetLocation> Ingredients { get; set; } = new();
        public ItemStack Output { get; set; } = null!;
        public float CookTime { get; set; } = 200;
    }
}