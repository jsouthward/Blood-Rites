#nullable enable
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    public class BlockRitualVessel : BlockBucket
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            api.Logger.Notification("[BloodRites] BlockRitualVessel.OnLoaded fired for " + Code);
        }

        private static readonly AssetLocation LiquidFullShapeLoc =
            new AssetLocation("bloodrites", "shapes/block/vesselLiquidFull");

        // must match vesselLiquidFull.json bottom (1 voxel)
        private const float yBottom = 0.5f / 16f;

        // IMPORTANT: this is the older signature your build is using
        public override void OnJsonTesselation(ref MeshData mesh, ref int[] chunkExtBlocks, BlockPos pos, Block[] chunkExt, int chunkExtBlocksLen)
        {
            // call base with the same signature
            base.OnJsonTesselation(ref mesh, ref chunkExtBlocks, pos, chunkExt, chunkExtBlocksLen);
            (api as ICoreClientAPI)?.Logger.Notification("RitualVessel OnJsonTesselation called at {0}", pos);

            if (api.Side != EnumAppSide.Client) return;

            var be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityRitualVessel;
            if (be == null) return;

            //float fill = be.GetFillFractionSafe();
            //if (fill <= 0.001f) return;
            float fill = 1f; // FORCE FULL for debug

            var capi = api as ICoreClientAPI;
            if (capi == null) return;

            var asset = capi.Assets.TryGet(LiquidFullShapeLoc);
            if (asset == null) return;

            Shape shape = asset.ToObject<Shape>();

            // New API (your warning says to use GetTextureSource)
            ITexPositionSource texSource = capi.Tesselator.GetTextureSource(this);

            capi.Tesselator.TesselateShape("ritualvessel-liquid", shape, out MeshData liquidMesh, texSource);

            // scale in Y around the bottom so it "fills upward"
            liquidMesh.Scale(new Vec3f(0.5f, yBottom, 0.5f), 1f, fill, 1f);

            mesh.AddMeshData(liquidMesh);
        }
    }
}
