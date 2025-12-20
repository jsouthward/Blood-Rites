#nullable enable
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    public class BlockEntityRitualVessel : BlockEntityBucket
    {
        private MeshData? liquidMesh;
        private float lastFillFrac = -1f;

        // liquid full cube shape (kept, but we now load via Shape.TryGet)
        private static readonly AssetLocation LiquidFullShapeLoc =
            new AssetLocation("bloodrites", "shapes/block/vesselLiquidFull.json");

        // Match shape's "from" Y (in pixels/16). You said your liquid starts at y=0.5 in JSON.
        private const float LiquidBottomY = 1f / 16f;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                RebuildLiquidMesh(force: true);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            if (Api?.Side == EnumAppSide.Client)
            {
                RebuildLiquidMesh(force: false);
            }
        }

        private void RebuildLiquidMesh(bool force)
        {
            var capi = Api as ICoreClientAPI;
            if (capi == null) return;

            float fill = GetFillFractionSafe();

            // While debugging, keep this VERY lenient so it doesn't "stick" and look like nothing is happening
            if (!force && Math.Abs(fill - lastFillFrac) < 0.0001f) return;
            lastFillFrac = fill;

            capi.Logger.Notification("[BloodRites] RitualVessel rebuild @ {0} fill={1:0.###}", Pos, fill);

            if (fill <= 0.001f)
            {
                liquidMesh = null;
                MarkDirty(true);
                return;
            }

            // --- NEW: Shape.TryGet style load ---
            const string shapePath = "bloodrites:shapes/block/vesselLiquidFull.json";
            Shape? shape = Shape.TryGet(capi, shapePath);
            if (shape == null)
            {
                capi.World.Logger.Warning("[BloodRites] Missing cauldron shape at {0}", shapePath);
                liquidMesh = null;
                MarkDirty(true);
                return;
            }

            // Important: use THIS block's texture source so your #... texture refs resolve correctly
            var block = Api.World.BlockAccessor.GetBlock(Pos);
            ITexPositionSource texSource = capi.Tesselator.GetTextureSource(block);

            capi.Tesselator.TesselateShape("ritualvessel-liquid", shape, out MeshData mesh, texSource);

            // Scale around the bottom so it grows upward (NOT translate up/down)
            mesh.Scale(new Vec3f(0.5f, LiquidBottomY, 0.5f), 1f, fill, 1f);

            liquidMesh = mesh;

            capi.Logger.Notification("[BloodRites] RitualVessel liquid mesh verts={0}", liquidMesh.VerticesCount);

            MarkDirty(true);
        }

        public float GetFillFractionSafe()
        {
            float cap = Block?.Attributes?["liquidContainerProps"]?["capacityLitres"]?.AsFloat(0) ?? 0;
            if (cap <= 0.0001f) return 0f;

            ItemStack? stack = TryGetContentStack();
            if (stack == null) return 0f;

            // 100 portions per litre (your log: 300 portions = 3L)
            float litres = stack.StackSize / 100f;

            float frac = litres / cap;
            if (frac < 0f) frac = 0f;
            if (frac > 1f) frac = 1f;
            return frac;
        }

        private ItemStack? TryGetContentStack()
        {
            // Try the common patterns first
            try
            {
                // BlockEntityBucket often has inventory slot 0 as contents
                var inv = GetInventoryViaReflection();
                if (inv != null && inv.Count > 0)
                {
                    return inv[0].Itemstack;
                }
            }
            catch
            {
                // swallow; fallback below
            }

            return null;
        }

        private InventoryBase? GetInventoryViaReflection()
        {
            // Look on this type and base types for fields/properties like inventory/inv/Inventory
            object? invObj =
                GetMember(this, "inventory") ??
                GetMember(this, "inv") ??
                GetMember(this, "Inventory");

            return invObj as InventoryBase;
        }

        private static object? GetMember(object obj, string name)
        {
            var t = obj.GetType();

            // field
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(obj);

            // property
            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null) return p.GetValue(obj);

            return null;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            bool ok = base.OnTesselation(mesher, tesselator);

            if (liquidMesh != null)
            {
                mesher.AddMeshData(liquidMesh);
            }

            return ok;
        }
    }
}