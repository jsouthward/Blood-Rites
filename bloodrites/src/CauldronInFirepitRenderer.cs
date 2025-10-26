using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace bloodrites
{
    /// <summary>
    /// Renders the cauldron (and optional lid) when the block sits in a firepit.
    /// </summary>
    public class CauldronInFirepitRenderer : IInFirepitRenderer, IRenderer, IDisposable
    {
        ICoreClientAPI capi;
        BlockPos pos;
        bool isInOutputSlot;
        float temp;

        MultiTextureMeshRef cauldronRef;
        MultiTextureMeshRef lidRef;          // optional: if you have a separate lid shape
        MultiTextureMeshRef mealRef;         // if you want food/liquid shown when cooked

        Matrixf modelMat = new Matrixf();
        ILoadedSound cookingSound;

        public double RenderOrder => 0.5;
        public int RenderRange => 20;

        public CauldronInFirepitRenderer(ICoreClientAPI capi, ItemStack stack, BlockPos pos, bool isInOutputSlot)
        {
            this.capi = capi;
            this.pos = pos;
            this.isInOutputSlot = isInOutputSlot;

            // Get the cooked-variant block (same trick as the metal pot)
            var cookedBlock = capi.World.GetBlock(stack.Collectible.CodeWithVariant("type", "cooked")) as BlockCookedContainer;

            // 1) Cauldron body
            // Make sure you have: assets/bloodrites/shapes/block/cauldron.json
            if (Shape.TryGet(capi, "bloodrites:shapes/block/cauldron.json") is Shape cauldronShape)
            {
                capi.Tesselator.TesselateShape(cookedBlock, cauldronShape, out var cauldronMesh, null, null, null);
                cauldronRef = capi.Render.UploadMultiTextureMesh(cauldronMesh);
            }

            // 3) Optional meal/contents when in output slot (like vanilla pot)
            if (isInOutputSlot && cookedBlock != null)
            {
                var meshcache = capi.ModLoader.GetModSystem<MealMeshCache>(true);
                // Small Y-offset so contents sit inside the rim
                mealRef = meshcache.GetOrCreateMealInContainerMeshRef(
                    cookedBlock,
                    cookedBlock.GetCookingRecipe(capi.World, stack),
                    cookedBlock.GetNonEmptyContents(capi.World, stack),
                    new Vec3f(0f, 0.375f, 0f)
                );
            }
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (cauldronRef == null) return;

            var rpi = capi.Render;
            var cam = capi.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true, EnumBlendMode.Standard);

            var prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.AlphaTest = 0.05f;
            prog.NormalShaded = 1;

            // Base transform: position + a small Y raise so it sits correctly on the pit rim
            prog.ModelMatrix = modelMat.Identity()
                .Translate(pos.X - cam.X, pos.Y - cam.Y, pos.Z - cam.Z)
                .Translate(0f, 0.0625f, 0f)   // tweak this to sit on the firepit
                .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            // Body
            rpi.RenderMultiTextureMesh(cauldronRef, "tex", 0);

            // Contents if cooked
            if (mealRef != null)
            {
                rpi.RenderMultiTextureMesh(mealRef, "tex", 0);
            }

            // Lid wobble if heating and not yet cooked
            if (!isInOutputSlot && lidRef != null)
            {
                float heat = GameMath.Clamp((temp - 50f) / 50f, 0f, 1f);
                float ox = GameMath.Sin(capi.World.ElapsedMilliseconds / 300f) * (5f / 16f);
                float oz = GameMath.Cos(capi.World.ElapsedMilliseconds / 300f) * (5f / 16f);

                prog.ModelMatrix = modelMat.Identity()
                    .Translate(pos.X - cam.X, pos.Y - cam.Y, pos.Z - cam.Z)
                    .Translate(0f, 0.8125f, 0f)
                    .Translate(-ox, 0f, -oz)
                    .RotateX(heat * GameMath.Sin(capi.World.ElapsedMilliseconds / 50f) / 60f)
                    .RotateZ(heat * GameMath.Sin(capi.World.ElapsedMilliseconds / 50f) / 60f)
                    .Translate(ox, 0f, oz)
                    .Values;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMultiTextureMesh(lidRef, "tex", 0);
            }

            prog.Stop();
        }

        public void OnUpdate(float temperature)
        {
            temp = temperature;

            // Optional bubbling sound like the example
            float vol = GameMath.Clamp((temp - 50f) / 50f, 0f, 1f);
            SetCookingSoundVolume(isInOutputSlot ? 0f : vol);
        }

        public void OnCookingComplete()
        {
            isInOutputSlot = true;   // switch to meal/contents view
        }

        public void Dispose()
        {
            cauldronRef?.Dispose();
            lidRef?.Dispose();
            mealRef?.Dispose();

            if (cookingSound != null)
            {
                cookingSound.Stop();
                cookingSound.Dispose();
                cookingSound = null;
            }
        }

        public void SetCookingSoundVolume(float volume)
        {
            if (volume <= 0f)
            {
                if (cookingSound != null)
                {
                    cookingSound.Stop();
                    cookingSound.Dispose();
                    cookingSound = null;
                }
                return;
            }

            if (cookingSound == null)
            {
                cookingSound = capi.World.LoadSound(new SoundParams
                {
                    Location = new AssetLocation("sounds/effect/cooking.ogg"),
                    ShouldLoop = true,
                    Position = pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Range = 10f,
                    ReferenceDistance = 3f,
                    Volume = volume
                });
                cookingSound.Start();
                return;
            }

            cookingSound.SetVolume(volume);
        }
    }
}