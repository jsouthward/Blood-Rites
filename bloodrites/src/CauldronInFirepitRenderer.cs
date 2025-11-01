using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    public class CauldronInFirepitRenderer : IInFirepitRenderer, IRenderer
    {

        private readonly ICoreClientAPI capi;
        private readonly BlockPos pos;
        private MultiTextureMeshRef cauldronMesh;
        private float temperature;
        private bool isOutputSlot;
        private ItemStack stack;
        private readonly Matrixf modelMat = new Matrixf();
        // Track liquid data
        private MultiTextureMeshRef liquidMesh;
        public double RenderOrder => 0.5;
        public int RenderRange => 24;
        //Cauldron liquid render 
        private const float CauldronMaxFillHeight = 0.36f;
        private const float CauldronMaxPortions = 300f;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public CauldronInFirepitRenderer(ICoreClientAPI capi, ItemStack stack, BlockPos pos, bool isOutputSlot)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        {
            this.capi = capi;
            this.pos = pos;
            this.isOutputSlot = isOutputSlot;
            this.stack = stack;

            // 1. Load Cauldron
            BuildCauldronMesh(stack);
            // 3. Add liquid to Cauldron
            BuildLiquidMesh(stack);

        }
        private void BuildCauldronMesh(ItemStack stack)
        {
            var shape = Shape.TryGet(capi, "bloodrites:shapes/block/cauldron.json");
            if (shape == null)
            {
                capi.World.Logger.Warning("[BloodRites] Missing cauldron shape at bloodrites:shapes/block/cauldron.json");
                return;
            }

            // 2. Tesselate the cauldron
            try
            {
                capi.Tesselator.TesselateShape(stack.Collectible as Block, shape, out var mesh);
                cauldronMesh = capi.Render.UploadMultiTextureMesh(mesh);
            }
            catch (Exception e)
            {
                capi.World.Logger.Error("[BloodRites] Failed to tesselate cauldron shape: {0}", e);
            }
        }
        private void BuildLiquidMesh(ItemStack stack)
        {
            var liquidShape = Shape.TryGet(capi, "bloodrites:shapes/block/cauldronLiquidContents.json");
            if (liquidShape != null)
            {
                try
                {
                    var contentsTree = stack.Attributes?["contents"] as ITreeAttribute;
                    if (contentsTree == null)
                    {
                        capi.World.Logger.Notification("[BloodRites] Cauldron Is Empty (has NO contents attribute yet)");
                        return;
                    }

                    if (!contentsTree.HasAttribute("0"))
                    {
                        capi.World.Logger.Notification("[BloodRites] Cauldron is empty (no slot 0)");
                        return;
                    }

                    ItemStack liquidStack = contentsTree.GetItemstack("0");
                    if (liquidStack == null)
                    {
                        capi.World.Logger.Notification("[BloodRites] Contents exists, but liquidStack is NULL");
                        return;
                    }

                    liquidStack.ResolveBlockOrItem(capi.World);

                    if (liquidStack.Collectible == null)
                    {
                        capi.World.Logger.Error("[BloodRites] ERROR: liquidStack collectible still NULL!");
                        return;
                    }

                    int portions = liquidStack.StackSize;
                    string liquidCode = liquidStack.Collectible.Code.ToString();

                    capi.World.Logger.Notification($"Liquid: {liquidCode}, portions = {portions}");


                    // Tesselate liquid shape
                    capi.Tesselator.TesselateShape(liquidStack.Collectible, liquidShape, out var lmesh);
                    // Move liqid shape up or down for fill level
                    float fillLevel = GameMath.Clamp(portions / CauldronMaxPortions, 0f, 1f);
                    lmesh.Translate(0f, fillLevel * CauldronMaxFillHeight, 0f);

                    liquidMesh = capi.Render.UploadMultiTextureMesh(lmesh);
                    capi.World.Logger.Notification("[BloodRites] ✅ Liquid mesh created");
                }
                catch (Exception e)
                {
                    capi.World.Logger.Error("[BloodRites] Liquid tesselation failure: " + e);
                }
            }
            else
            {
                capi.World.Logger.Error("[BloodRites] Could not load cauldron liquid shape!");
            }
        }

        public void OnUpdate(float temperature)
        {
            this.temperature = temperature;
        }

        public void OnCookingComplete()
        {
            isOutputSlot = true;
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            // Render Cauldron mesh
            if (cauldronMesh == null) return;

            var rpi = capi.Render;
            var cam = capi.World.Player.Entity.CameraPos;

            var prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.AlphaTest = 0.05f;
            prog.NormalShaded = 1;

            // Apply transform to position it on top of the firepit
            prog.ModelMatrix = modelMat.Identity()
                .Translate(pos.X - cam.X, pos.Y - cam.Y, pos.Z - cam.Z)
                .Translate(0f, 0.0625f, 0f)
                .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMultiTextureMesh(cauldronMesh, "tex", 0);
            prog.Stop();

            
            // --- Render liquid Mesh plane ---
            if (liquidMesh != null && stage == EnumRenderStage.Opaque)
            {
                var prog2 = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
                //prog2.RgbaTint = new Vec4f(1f, 0f, 0f, 1f); // MAKE LIQUID BRIGHT RED FOR DEBUG
                prog2.RgbaAmbientIn = rpi.AmbientColor;
                prog2.RgbaFogIn = rpi.FogColor;
                prog2.FogMinIn = rpi.FogMin;
                prog2.FogDensityIn = rpi.FogDensity;
                prog2.NormalShaded = 1;

                prog2.ModelMatrix = modelMat.Identity()
                    .Translate(pos.X - cam.X, pos.Y - cam.Y, pos.Z - cam.Z)
                    .Values;

                prog2.ViewMatrix = rpi.CameraMatrixOriginf;
                prog2.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMultiTextureMesh(liquidMesh, "tex", 0);
                prog2.Stop();
            }
        }


        public void Dispose()
        {
            cauldronMesh?.Dispose();
            liquidMesh?.Dispose();
        }
    }
}