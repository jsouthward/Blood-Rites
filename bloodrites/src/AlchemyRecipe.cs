using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace bloodrites
{
    public class AlchemyRecipe
    {
        public string Code = "";
        public Dictionary<string, int> Ingredients = new(); // itemcode -> count
        public string InputLiquidCode = "";     // e.g. "game:waterportion"
        public int InputLiquidPortions = 0;     // e.g. 300
        public bool ConsumeInputLiquid = true;  // usually true
        public string OutputLiquidCode = "";                // e.g. "bloodrites:bloodportion"
        public float OutputLitres = 1f;                     // how much it fills to (or adds)
        public string OutputVesselCode { get; set; } = "";         // e.g. "bloodrites:alchemycauldron-blood"

    }
}