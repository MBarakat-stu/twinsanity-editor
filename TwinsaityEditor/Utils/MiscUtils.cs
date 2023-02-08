﻿using Twinsanity;
using System;
using System.Drawing;
using OpenTK.Graphics.OpenGL;

namespace TwinsaityEditor.Utils
{
    static class TextUtils
    {

        public static bool Pref_TruncateObjectNames = true;
        public static bool Pref_EnableAnyObjectNames = true;

        public static string TruncateObjectName(string obj_name, ushort ObjectID, string prefix, string suffix)
        {
            if (Pref_TruncateObjectNames)
            {
                if (obj_name != string.Empty && obj_name.Split('|').Length > 1)
                {
                    obj_name = obj_name.Split('|')[obj_name.Split('|').Length - 1];
                }
                if (obj_name != string.Empty && obj_name.StartsWith("act_"))
                {
                    obj_name = obj_name.Substring(4);
                }
            }
            if (obj_name == string.Empty && Pref_EnableAnyObjectNames)
            {
                if (Enum.IsDefined(typeof(DefaultEnums.ObjectID), ObjectID))
                {
                    obj_name = prefix + (DefaultEnums.ObjectID)ObjectID + suffix;
                }
            }
            return obj_name;
        }

        public static int LoadTexture(ref Bitmap data, int quality = 0, bool flip_y = false)
        {
            var bitmapBits = data.LockBits(new Rectangle(0, 0, data.Width, data.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmapBits.Width, bitmapBits.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bitmapBits.Scan0);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            data.UnlockBits(bitmapBits);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            return texture;
        }

        public static void LoadTexture(uint[] materials, FileController file, VertexBufferData vtx, int index)
        {
            var material = materials[index];
            {
                var secId = 11U;
                if (file.Data.Type == TwinsFile.FileType.SM2)
                {
                    secId = 6U;
                }
                var matSec = file.Data.GetItem<TwinsSection>(secId).GetItem<TwinsSection>(1);
                var texSec = file.Data.GetItem<TwinsSection>(secId).GetItem<TwinsSection>(0);
                if (matSec.ContainsItem(material))
                {
                    var mat = matSec.GetItem<Material>(material);
                    Texture texture = null;
                    foreach (var shader in mat.Shaders)
                    {
                        if (shader.TxtMapping == TwinsShader.TextureMapping.ON)
                        {
                            texture = texSec.GetItem<Texture>(shader.TextureId);
                            break;
                        }
                    }
                    if (texture != null)
                    {
                        var bmp = texture.GetBmp();
                        var texId = LoadTexture(ref bmp);
                        vtx.Texture = texId;
                        vtx.Flags |= BufferPointerFlags.TexCoord;
                    }
                }
            }
        }
    }
}
