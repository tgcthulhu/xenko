﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;

using SiliconStudio.Core.Mathematics;

namespace SiliconStudio.Paradox.Effects.Images
{
    /// <summary>
    /// Applies a TripleRhombi blur to a texture. (Hexagonal bokeh)
    /// </summary>
    /// <remarks>
    /// This is a technique based on DICE's presentation at Siggraph 2011. 
    /// http://advances.realtimerendering.com/s2011/White,%20BarreBrisebois-%20Rendering%20in%20BF3%20%28Siggraph%202011%20Advances%20in%20Real-Time%20Rendering%20Course%29.pdf
    /// </remarks>
    public class TripleRhombiBokeh : BokehBlur
    {
        private ImageEffect directionalBlurEffect;
        private ImageEffect finalCombineEffect;

        private int tapCount;

        private float[] tapWeights;

        // Simple flag to switch between the debug version or the optimized version
        private bool useOptimizedPath = false;

        // Tap offset for each of the 3 rhombis
        private Vector2[] rhombiTapOffsets = new Vector2[3];
        // Dirty flag to tell if offsets need to be recalculated.
        private bool rhombiTapOffsetsDirty = true;

        /// <summary>
        /// Phase of the bokeh effect. (rotation angle in radian)
        /// </summary>
        public float Phase { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TripleRhombiBokeh"/> class.
        /// </summary>
        public TripleRhombiBokeh()
        {
            directionalBlurEffect = ToLoadAndUnload(new ImageEffectShader("DepthAwareDirectionalBlurEffect"));
            finalCombineEffect = ToLoadAndUnload(new ImageEffectShader("TripleRhombiCombineShader"));
            Phase = 0f;
        }

        public override float Radius
        {
            get
            {
                return base.Radius;
            }

            set
            {
                float oldRadius = Radius;
                base.Radius = value;

                // Our actual total number of tap
                tapCount = (int)Radius + 1;
                rhombiTapOffsetsDirty = (oldRadius != Radius);
            }
        }

        // Updates the texture tap offsets for the final combination pass
        private void calculateRhombiOffsets() 
        {
            rhombiTapOffsetsDirty = false;

            var texture = GetSafeInput(0);
            var textureSize = new Vector2( 1f/ (texture.Width), 1f / (texture.Height));
            // Half-radius of the hexagon
            float halfRadius = Radius * 0.5f; 
            // Half-width of an hexagon pointing up (altitude of an equilateral triangle)
            float hexagonalHalfWidth = Radius * (float)Math.Sqrt(3f) / 2f;

            // TODO Check potential different behavior with OGL where vertical addressing (V)
            // is swapped compared to D3D textures. 

            // TODO Add support for the Phase.

            // Shifts all rhombis so they share 3 common edges
            var rhombiPosition = new Vector2[3] 
            { 
                new Vector2( -hexagonalHalfWidth,   halfRadius), // top left rhombi
                new Vector2(  hexagonalHalfWidth,   halfRadius), // top right rhombi
                new Vector2(                  0f,  -Radius    )  // bottom rhombi
            };

            // Apply some bias to avoid the "upside-down" Y artifacts caused by rhombi overlapping.
            var biasStrength = 0.4f;
            var bias = new Vector2[3] 
            { 
                new Vector2( -biasStrength,   biasStrength), // top left rhombi
                new Vector2(  biasStrength,   biasStrength), // top right rhombi
                new Vector2(            0f,  -biasStrength)  // bottom rhombi
            };

            for (int i = 0; i < 3; i++)
            {
                rhombiTapOffsets[i] = (rhombiPosition[i] + bias[i]) * textureSize;
            }
             
        }

        protected override void DrawCore(RenderContext context)
        {
            // Make sure we keep our uniform weights in synchronization with the number of taps
            if (tapWeights == null || tapWeights.Length != tapCount)
            {
                tapWeights = DoFUtil.GetUniformWeightBlurArray(tapCount);
                calculateRhombiOffsets();
            }

            if (!useOptimizedPath)
            {
                DrawCoreNaive(context);
            }
            else
            {
                //TODO use MRT to speed-up the process
            }
        }

        // Naive approach: 6 passes
        protected void DrawCoreNaive(RenderContext context)
        {
            var originalTexture = GetSafeInput(0);
            var originalDepthBuffer = GetSafeInput(1);
            var outputTexture = GetSafeOutput(0);

            if (rhombiTapOffsetsDirty) calculateRhombiOffsets();

            var tapNumber = 2 * tapCount - 1;
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurKeys.Count, tapCount);
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurKeys.TotalTap, tapNumber);
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurKeys.ReferenceIndex, 0);
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurUtilKeys.Radius, Radius);
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurUtilKeys.TapWeights, tapWeights);

            // Vertical blur
            var blurAngle = MathUtil.PiOverTwo + Phase;
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurUtilKeys.Direction, new Vector2((float)Math.Cos(blurAngle), (float)Math.Sin(blurAngle)));

            var verticalBlurTexture = NewScopedRenderTarget2D(originalTexture.Description);
            directionalBlurEffect.SetInput(0, originalTexture);
            directionalBlurEffect.SetInput(1, originalDepthBuffer);
            directionalBlurEffect.SetOutput(verticalBlurTexture);
            directionalBlurEffect.Draw(context, "TripleRhombiBokeh_RhombiABVertical_tap{0}_radius{1}", tapNumber, (int)Radius);

            // Rhombi A (top left)
            blurAngle = 7f * MathUtil.Pi / 6f + Phase;
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurUtilKeys.Direction, new Vector2((float)Math.Cos(blurAngle), (float)Math.Sin(blurAngle)));

            var rhombiA = NewScopedRenderTarget2D(originalTexture.Description);
            directionalBlurEffect.SetInput(0, verticalBlurTexture);
            directionalBlurEffect.SetOutput(rhombiA);
            directionalBlurEffect.Draw(context, "TripleRhombiBokeh_RhombiA_tap{0}_radius{1}", tapNumber, (int)Radius);

            // Rhombi B (top right)
            blurAngle = -MathUtil.Pi / 6f + Phase;
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurUtilKeys.Direction, new Vector2((float)Math.Cos(blurAngle), (float)Math.Sin(blurAngle)));

            var rhombiB = NewScopedRenderTarget2D(originalTexture.Description);
            directionalBlurEffect.SetInput(0, verticalBlurTexture);
            directionalBlurEffect.SetOutput(rhombiB);
            directionalBlurEffect.Draw(context, "TripleRhombiBokeh_RhombiB_tap{0}_radius{1}", tapNumber, (int)Radius);

            //Rhombi C (bottom)
            blurAngle = 7f * MathUtil.Pi / 6f + Phase;
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurUtilKeys.Direction, new Vector2((float)Math.Cos(blurAngle), (float)Math.Sin(blurAngle)));

            var rhombiCTmp = NewScopedRenderTarget2D(originalTexture.Description);
            directionalBlurEffect.SetInput(0, originalTexture);
            directionalBlurEffect.SetOutput(rhombiCTmp);
            directionalBlurEffect.Draw(context, "TripleRhombiBokeh_RhombiCTmp_tap{0}_radius{1}", tapNumber, (int)Radius);

            blurAngle = -MathUtil.Pi / 6f + Phase;
            directionalBlurEffect.Parameters.Set(DepthAwareDirectionalBlurUtilKeys.Direction, new Vector2((float)Math.Cos(blurAngle), (float)Math.Sin(blurAngle)));

            var rhombiC = NewScopedRenderTarget2D(originalTexture.Description);
            directionalBlurEffect.SetInput(0, rhombiCTmp);
            directionalBlurEffect.SetOutput(rhombiC);
            directionalBlurEffect.Draw(context, "TripleRhombiBokeh_RhombiC_tap{0}_radius{1}", tapNumber, (int)Radius);

            // Final pass outputting the average of the 3 blurs
            finalCombineEffect.SetInput(0, rhombiA);
            finalCombineEffect.SetInput(1, rhombiB);
            finalCombineEffect.SetInput(2, rhombiC);
            finalCombineEffect.SetOutput(outputTexture);
            finalCombineEffect.Parameters.Set(TripleRhombiCombineShaderKeys.RhombiTapOffsets, rhombiTapOffsets);
            finalCombineEffect.Draw(context, name: "TripleRhombiBokehCombine");
        }
    }
}