#nullable enable
using System;
using System.Reflection;
using System.Collections.Generic;
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
                RebuildLiquidMesh(force: true);
            }
        }

        private void RebuildLiquidMesh(bool force)
        {
            var capi = Api as ICoreClientAPI;
            if (capi == null) return;

            float fill = GetFillFractionSafe();

            if (!force && Math.Abs(fill - lastFillFrac) < 0.0001f) return;
            lastFillFrac = fill;

            capi.Logger.Notification(
                "[BloodRites] RitualVessel rebuild @ {0} fill={1:0.###}",
                Pos, fill
            );

            if (fill <= 0.001f)
            {
                liquidMesh = null;
                MarkDirty(true);
                return;
            }

            // --- Load liquid shape ---
            const string shapePath = "bloodrites:shapes/block/vesselLiquidFull.json";
            Shape? shape = Shape.TryGet(capi, shapePath);
            if (shape == null)
            {
                capi.World.Logger.Warning(
                    "[BloodRites] Missing liquid shape at {0}",
                    shapePath
                );
                liquidMesh = null;
                MarkDirty(true);
                return;
            }

            // --- Resolve correct texture source (same logic as vanilla bucket) ---
            var block = Api.World.BlockAccessor.GetBlock(Pos);
            ItemStack? contentStack = TryGetContentStack();

            ITexPositionSource texSource;
            if (contentStack != null)
            {
                texSource = GetLiquidTextureSource(capi, contentStack, block);
            }
            else
            {
                texSource = capi.Tesselator.GetTextureSource(block);
            }

            capi.Tesselator.TesselateShape(
                "ritualvessel-liquid",
                shape,
                out MeshData mesh,
                texSource
            );

            // Scale upward from liquid bottom
            mesh.Scale(
                new Vec3f(0.5f, LiquidBottomY, 0.5f),
                1f,
                fill,
                1f
            );

            liquidMesh = mesh;

            capi.Logger.Notification(
                "[BloodRites] RitualVessel liquid mesh verts={0}",
                liquidMesh.VerticesCount
            );

            MarkDirty(true);
        }

        // --------------------------------------------------------
        // Vanilla bucket-style texture resolution (1.21 compatible)
        // --------------------------------------------------------
        private ITexPositionSource GetLiquidTextureSource(
    ICoreClientAPI capi,
    ItemStack contentStack,
    Block block
)
        {
            JsonObject wtProps =
                contentStack.Collectible.Attributes?["waterTightContainerProps"] ?? default;

            if (wtProps.Exists)
            {
                JsonObject texObj = wtProps["texture"];
                if (texObj.Exists)
                {
                    var srcTextures = texObj.AsObject<Dictionary<string, AssetLocation>>(
                        new Dictionary<string, AssetLocation>(),
                        contentStack.Collectible.Code.Domain
                    );

                    // 🔑 FORCE a "liquid" key (this is what your shape expects)
                    var finalTextures = new Dictionary<string, AssetLocation>();

                    // If the liquid already defines "liquid", use it
                    if (srcTextures.TryGetValue("liquid", out var liquidTex))
                    {
                        finalTextures["liquid"] = liquidTex;
                    }
                    else
                    {
                        // Otherwise take the FIRST texture entry and map it to "liquid"
                        foreach (var kvp in srcTextures)
                        {
                            finalTextures["liquid"] = kvp.Value;
                            break;
                        }
                    }

                    return new ContainedTextureSource(
                        capi,
                        capi.BlockTextureAtlas,
                        finalTextures,
                        contentStack.Collectible.Code.ToString()
                    );
                }
            }

            // Fallback (should never happen unless liquid is malformed)
            return capi.Tesselator.GetTextureSource(block);
        }

        // --------------------------------------------------------

        public float GetFillFractionSafe()
        {
            float cap =
                Block?.Attributes?["liquidContainerProps"]?["capacityLitres"]?.AsFloat(0)
                ?? 0;

            if (cap <= 0.0001f) return 0f;

            ItemStack? stack = TryGetContentStack();
            if (stack == null) return 0f;

            // 100 portions per litre
            float litres = stack.StackSize / 100f;

            float frac = litres / cap;
            return GameMath.Clamp(frac, 0f, 1f);
        }

        private ItemStack? TryGetContentStack()
        {
            try
            {
                var inv = GetInventoryViaReflection();
                if (inv != null && inv.Count > 0)
                {
                    return inv[0].Itemstack;
                }
            }
            catch { }

            return null;
        }

        private InventoryBase? GetInventoryViaReflection()
        {
            object? invObj =
                GetMember(this, "inventory") ??
                GetMember(this, "inv") ??
                GetMember(this, "Inventory");

            return invObj as InventoryBase;
        }

        private static object? GetMember(object obj, string name)
        {
            var t = obj.GetType();

            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(obj);

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
