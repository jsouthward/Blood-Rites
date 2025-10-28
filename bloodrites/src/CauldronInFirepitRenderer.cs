using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    public class CauldronInFirepitRenderer : IInFirepitRenderer, IRenderer
    {
        private readonly ICoreClientAPI capi;
        private readonly BlockPos pos;
        private MultiTextureMeshRef cauldronMesh;
        private MultiTextureMeshRef contentsMesh;
        private float temperature;
        private bool isOutputSlot;
        private readonly Matrixf modelMat = new Matrixf();

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public CauldronInFirepitRenderer(ICoreClientAPI capi, ItemStack stack, BlockPos pos, bool isOutputSlot)
        {
            this.capi = capi;
            this.pos = pos;
            this.isOutputSlot = isOutputSlot;

            // 1. Load your cauldron shape safely
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

            // 3. Optional contents — show bubbling effect if cooking something
           
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

            // Render contents if available
            if (contentsMesh != null)
            {
                rpi.RenderMultiTextureMesh(contentsMesh, "tex", 0);
            }

            prog.Stop();
        }

        public void Dispose()
        {
            cauldronMesh?.Dispose();
            contentsMesh?.Dispose();
        }
    }
}