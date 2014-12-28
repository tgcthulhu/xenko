// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.ComponentModel;

using SiliconStudio.Core;
using SiliconStudio.Core.Reflection;
using SiliconStudio.Paradox.Assets.Materials.ComputeColors;
using SiliconStudio.Paradox.Effects.Materials;
using SiliconStudio.Paradox.Shaders;

namespace SiliconStudio.Paradox.Assets.Materials
{
    /// <summary>
    /// A Metalness map for the specular material feature.
    /// </summary>
    [DataContract("MaterialMetalnessMapFeature")]
    [Display("Metalness Map")]
    [ObjectFactory(typeof(Factory))]
    public class MaterialMetalnessMapFeature : IMaterialSpecularFeature
    {
        /// <summary>
        /// Gets or sets the metalness map.
        /// </summary>
        /// <value>The metalness map.</value>
        [Display("Metalness Map")]
        [DefaultValue(null)]
        public MaterialComputeColor MetalnessMap { get; set; }

        private class Factory : IObjectFactory
        {
            public object New(Type type)
            {
                return new MaterialMetalnessMapFeature() { MetalnessMap = new MaterialTextureComputeColor() };
            }
        }

        public void Visit(MaterialGeneratorContext context)
        {
            if (MetalnessMap != null)
            {
                var computeColorSource = MetalnessMap.GenerateShaderSource(context, new MaterialComputeColorKeys(MaterialKeys.MetalnessMap, MaterialKeys.MetalnessValue));
                var mixin = new ShaderMixinSource();
                mixin.Mixins.Add(new ShaderClassSource("MaterialLayerMetalness"));
                mixin.AddComposition("metalnessMap", computeColorSource);
                context.UseStream("matSpecular");
                context.AddSurfaceShader(mixin);
            }
        }
    }
}