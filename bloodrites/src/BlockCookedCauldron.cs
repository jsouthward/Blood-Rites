using System;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    /// <summary>
    /// A custom cook vessel for firepits (replaces BlockCookedContainer for the Alchemy Cauldron).
    /// </summary>
    public class BlockCookedCauldron : Block
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Optional: If you want to open a GUI or do something when interacted directly in-world
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        /// <summary>
        /// This is called by the firepit to get the visual representation
        /// of the vessel when attached to the firepit.
        /// </summary>
        public virtual MeshData GetCauldronMesh(ICoreClientAPI capi)
        {
            var shape = Vintagestory.API.Common.Shape.TryGet(capi, new AssetLocation("bloodrites:shapes/block/cauldron.json"));
            if (shape == null)
            {
                capi.World.Logger.Warning("Missing cauldron shape!");
                return null;
            }

            capi.Tesselator.TesselateShape(this, shape, out var mesh);
            return mesh;
        }

        /// <summary>
        /// Called by firepit to determine whether this block is a valid cooking vessel.
        /// </summary>
        public virtual bool IsCookVessel()
        {
            return true;
        }
    }
}