﻿using System;
using System.Collections.Generic;
using GUI.Utils;
using OpenTK.Graphics.OpenGL;
using ValveResourceFormat;
using ValveResourceFormat.Blocks;
using ValveResourceFormat.ResourceTypes;
using ValveResourceFormat.ResourceTypes.NTROSerialization;

namespace GUI.Types.Renderer
{
    internal class MaterialLoader
    {
        private readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private readonly Package CurrentPackage;
        private readonly string CurrentFileName;
        private int ErrorTextureID;

        public MaterialLoader(string currentFileName, Package currentPackage)
        {
            CurrentPackage = currentPackage;
            CurrentFileName = currentFileName;
        }

        public Material GetMaterial(string name, int maxTextureMaxAnisotropy)
        {
            if (!Materials.ContainsKey(name))
            {
                return LoadMaterial(name, maxTextureMaxAnisotropy);
            }

            return Materials[name];
        }

        private Material LoadMaterial(string name, int maxTextureMaxAnisotropy)
        {
            Console.WriteLine("\n>> Loading material " + name);

            var mat = new Material();
            var resource = new Resource();

            Materials.Add(name, mat);

            if (!FileExtensions.LoadFileByAnyMeansNecessary(resource, name + "_c", CurrentFileName, CurrentPackage))
            {
                Console.Error.WriteLine("File " + name + " not found");

                mat.TextureIDs.Add("g_tColor", GetErrorTexture());
                mat.TextureIDs.Add("g_tNormal", GetErrorTexture());

                return mat;
            }

            var matData = (NTRO)resource.Blocks[BlockType.DATA];
            mat.Name = ((NTROValue<string>)matData.Output["m_materialName"]).Value;
            mat.ShaderName = ((NTROValue<string>)matData.Output["m_shaderName"]).Value;
            //mat.renderAttributesUsed = ((ValveResourceFormat.ResourceTypes.NTROSerialization.NTROValue<string>)matData.Output["m_renderAttributesUsed"]).Value; //TODO: string array?
            var intParams = (NTROArray)matData.Output["m_intParams"];
            for (var i = 0; i < intParams.Count; i++)
            {
                var subStruct = ((NTROValue<NTROStruct>)intParams[i]).Value;
                mat.IntParams.Add(((NTROValue<string>)subStruct["m_name"]).Value, ((NTROValue<int>)subStruct["m_nValue"]).Value);
            }

            var floatParams = (NTROArray)matData.Output["m_floatParams"];
            for (var i = 0; i < floatParams.Count; i++)
            {
                var subStruct = ((NTROValue<NTROStruct>)floatParams[i]).Value;
                mat.FloatParams.Add(((NTROValue<string>)subStruct["m_name"]).Value, ((NTROValue<float>)subStruct["m_flValue"]).Value);
            }

            var vectorParams = (NTROArray)matData.Output["m_vectorParams"];
            for (var i = 0; i < vectorParams.Count; i++)
            {
                var subStruct = ((NTROValue<NTROStruct>)vectorParams[i]).Value;
                var ntroVector = ((NTROValue<Vector4>)subStruct["m_value"]).Value;
                mat.VectorParams.Add(((NTROValue<string>)subStruct["m_name"]).Value, new OpenTK.Vector4(ntroVector.X, ntroVector.Y, ntroVector.Z, ntroVector.W));
            }

            var textureParams = (NTROArray)matData.Output["m_textureParams"];
            for (var i = 0; i < textureParams.Count; i++)
            {
                var subStruct = ((NTROValue<NTROStruct>)textureParams[i]).Value;
                mat.TextureParams.Add(((NTROValue<string>)subStruct["m_name"]).Value, ((NTROValue<ResourceExtRefList.ResourceReferenceInfo>)subStruct["m_pValue"]).Value);
            }

            var dynamicParams = (NTROArray)matData.Output["m_dynamicParams"];
            var dynamicTextureParams = (NTROArray)matData.Output["m_dynamicTextureParams"];

            var intAttributes = (NTROArray)matData.Output["m_intAttributes"];
            for (var i = 0; i < intAttributes.Count; i++)
            {
                var subStruct = ((NTROValue<NTROStruct>)intAttributes[i]).Value;
                mat.IntAttributes.Add(((NTROValue<string>)subStruct["m_name"]).Value, ((NTROValue<int>)subStruct["m_nValue"]).Value);
            }

            var floatAttributes = (NTROArray)matData.Output["m_floatAttributes"];
            for (var i = 0; i < floatAttributes.Count; i++)
            {
                var subStruct = ((NTROValue<NTROStruct>)floatAttributes[i]).Value;
                mat.FloatAttributes.Add(((NTROValue<string>)subStruct["m_name"]).Value, ((NTROValue<float>)subStruct["m_flValue"]).Value);
            }

            var vectorAttributes = (NTROArray)matData.Output["m_vectorAttributes"];
            for (var i = 0; i < vectorAttributes.Count; i++)
            {
                var subStruct = ((NTROValue<NTROStruct>)vectorAttributes[i]).Value;
                var ntroVector = ((NTROValue<Vector4>)subStruct["m_value"]).Value;
                mat.VectorAttributes.Add(((NTROValue<string>)subStruct["m_name"]).Value, new OpenTK.Vector4(ntroVector.X, ntroVector.Y, ntroVector.Z, ntroVector.W));
            }

            var textureAttributes = (NTROArray)matData.Output["m_textureAttributes"];
            //TODO
            var stringAttributes = (NTROArray)matData.Output["m_stringAttributes"];
            for (var i = 0; i < stringAttributes.Count; i++)
            {
                var subStruct = ((NTROValue<NTROStruct>)stringAttributes[i]).Value;
                mat.StringAttributes.Add(((NTROValue<string>)subStruct["m_name"]).Value, ((NTROValue<string>)subStruct["m_value"]).Value);
            }

            foreach (var textureReference in mat.TextureParams)
            {
                var key = textureReference.Key;

                Console.WriteLine(">>> " + textureReference.Key + " - " + textureReference.Value.Name);

                // TODO: Investigate why some things have differently numbered textures, Doto's slark has both g_tMasks1 and g_tMasks2
                if (key == "g_tColor1" || key == "g_tColor2")
                {
                    Console.Error.WriteLine(">>> Found {0}, investigate me please, {1}", key, textureReference.Value.Name);
                    key = "g_tColor";
                }

                mat.TextureIDs.Add(key, LoadTexture(textureReference.Value.Name, maxTextureMaxAnisotropy));
            }

            return mat;
        }

        private int LoadTexture(string name, int maxTextureMaxAnisotropy)
        {
            var textureResource = new Resource();

            if (!FileExtensions.LoadFileByAnyMeansNecessary(textureResource, name + "_c", CurrentFileName, CurrentPackage))
            {
                Console.Error.WriteLine("File " + name + " not found");

                return GetErrorTexture();
            }

            var tex = (Texture)textureResource.Blocks[BlockType.DATA];

            Console.WriteLine(">>>> Loading texture " + name + " " + tex.Flags);

            var id = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, id);

            var textureReader = textureResource.Reader;
            textureReader.BaseStream.Position = tex.Offset + tex.Size;

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, tex.NumMipLevels - 1);

            var width = tex.Width / (int)Math.Pow(2.0, tex.NumMipLevels);
            var height = tex.Height / (int)Math.Pow(2.0, tex.NumMipLevels);

            int blockSize;
            PixelInternalFormat format;

            if (tex.Format.HasFlag(VTexFormat.DXT1))
            {
                blockSize = 8;
                format = PixelInternalFormat.CompressedRgbaS3tcDxt1Ext;
            }
            else if (tex.Format.HasFlag(VTexFormat.DXT5))
            {
                blockSize = 16;
                format = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
            }
            else if (tex.Format.HasFlag(VTexFormat.RGBA8888))
            {
                //blockSize = 4;
                //format = PixelInternalFormat.Rgba8i;
                Console.Error.WriteLine("Don't support RGBA8888 but don't want to crash either. Using error texture!");
                return GetErrorTexture();
            }
            else
            {
                throw new Exception("Unsupported texture format: " + tex.Format);
            }

            for (var i = tex.NumMipLevels - 1; i >= 0; i--)
            {
                if ((width *= 2) == 0)
                {
                    width = 1;
                }

                if ((height *= 2) == 0)
                {
                    height = 1;
                }

                var size = ((width + 3) / 4) * ((height + 3) / 4) * blockSize;

                GL.CompressedTexImage2D(TextureTarget.Texture2D, i, format, width, height, 0, size, textureReader.ReadBytes(size));
            }

            if (tex.NumMipLevels < 2)
            {
                Console.WriteLine("Texture only has " + tex.NumMipLevels + " mipmap levels, should probably generate");
            }

            if (maxTextureMaxAnisotropy > 0)
            {
                GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt, maxTextureMaxAnisotropy);
            }

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)(tex.Flags.HasFlag(VTexFlags.SUGGEST_CLAMPS) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)(tex.Flags.HasFlag(VTexFlags.SUGGEST_CLAMPT) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat));

            return id;
        }

        private int GetErrorTexture()
        {
            if (ErrorTextureID == 0)
            {
                ErrorTextureID = GL.GenTexture();

                var bytes = new byte[] { 173, 255, 47, 255 };

                GL.BindTexture(TextureTarget.Texture2D, ErrorTextureID);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, bytes);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            }

            return ErrorTextureID;
        }
    }
}
