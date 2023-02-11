﻿using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using TwinsaityEditor.Animations;
using TwinsaityEditor.Controllers;
using Twinsanity;

namespace TwinsaityEditor.Viewers
{
    public class AnimationViewer : ThreeDViewer
    {
        private GraphicsInfoController graphicsInfo;
        private SkinController skin;
        private BlendSkinController bskin;
        private ModelController mesh;
        private FileController targetFile;
        private FileController file;
        private AnimationController animation;
        private AnimationPlayer player;

        public int FPS { get => player.FPS; set { player.FPS = value; if (animUpdateTimer != null) animUpdateTimer.Interval = (int)Math.Floor(1.0 / FPS * 1000); } }
        public bool Loop { get => player.Loop; set => player.Loop = value; }
        public bool Playing { get => player.Playing; set => player.Playing = value; }
        public bool Finished { get => player.Finished; }

        private Timer animUpdateTimer;
        

        private int bskinEndIndex = 0;
        private int skinEndIndex = 0;

        public AnimationViewer()
        { }

        public AnimationViewer(GraphicsInfoController mesh, AnimationController animation, FileController tFile)
        {
            targetFile = tFile;
            file = mesh.MainFile;
            graphicsInfo = mesh;
            this.animation = animation;
            player = new AnimationPlayer(animation);
            zFar = 50F;
            SetupVBORender();

            animUpdateTimer = new Timer
            {
                Interval = (int)Math.Floor(1.0 / 60 * 1000), //Set to ~60fps by default, TODO: Add to Preferences later
                Enabled = true
            };

            animUpdateTimer.Tick += delegate (object sender, EventArgs e)
            {
                UpdateAnimation(1.0f / FPS);
            };
        }

        private void SetupVBORender()
        {
            ModelController m;
            var vbos = 0;
            SectionController mesh_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(2);
            for (var i = 0; i < graphicsInfo.Data.ModelIDs.Length; i++)
            {
                foreach (RigidModel mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).Records)
                {
                    if (mod.ID == graphicsInfo.Data.ModelIDs[i].ModelID)
                    {
                        uint meshID = targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).GetItem<RigidModel>(mod.ID).MeshID;
                        m = mesh_sec.GetItem<ModelController>(meshID);
                        vbos += m.Data.SubModels.Count;
                        break;
                    }
                }
            }

            if (graphicsInfo.Data.SkinID != 0)
            {
                SectionController skin_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(4);
                foreach (Skin mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(4).Records)
                {
                    if (mod.ID == graphicsInfo.Data.SkinID)
                    {
                        skin = skin_sec.GetItem<SkinController>(mod.ID);
                        vbos += skin.Data.SubModels.Count;
                    }
                }
            }
            if (graphicsInfo.Data.BlendSkinID != 0)
            {
                SectionController blend_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(5);
                foreach (BlendSkin mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(5).Records)
                {
                    if (mod.ID == graphicsInfo.Data.BlendSkinID)
                    {
                        bskin = blend_sec.GetItem<BlendSkinController>(mod.ID);
                        vbos += bskin.Data.Models.Length;
                    }
                }
            }

            InitVBO(vbos, true);

            if (targetFile.Data.Type == TwinsFile.FileType.RM2)
            {
                LoadOGI_PS2();
            }
        }

        protected override void RenderHUD()
        {
            base.RenderHUD();
        }

        protected override void RenderObjects()
        {
            if (vtx == null) return;


            GL.PushMatrix();
            for (int i = 0; i < bskinEndIndex; i++)
            {
                vtx[i]?.DrawAllElements(PrimitiveType.Triangles, BufferPointerFlags.Normal);
            }
            for (int i = bskinEndIndex; i < skinEndIndex; i++)
            {
                vtx[i]?.DrawAllElements(PrimitiveType.Triangles, BufferPointerFlags.Normal);
            }
            for (int i = skinEndIndex; i < vtx.Count; i++)
            {
                vtx[i]?.DrawAllElements(PrimitiveType.Triangles, BufferPointerFlags.Normal);
            }
            GL.PopMatrix();
        }

        private void UpdateAnimation(float deltaTime)
        {
            player.AdvanceClock(deltaTime);

            var vtxIndex = skinEndIndex;
            for (int i = 0; i < graphicsInfo.Data.ModelIDs.Length; i++)
            {
                var transMat = player.Play((int)graphicsInfo.Data.ModelIDs[i].ID);

                SectionController mesh_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(2);
                SectionController rigid_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(3);
                foreach (RigidModel mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).Records)
                {
                    if (mod.ID == graphicsInfo.Data.ModelIDs[i].ModelID)
                    {
                        uint meshID = targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).GetItem<RigidModel>(mod.ID).MeshID;
                        mesh = mesh_sec.GetItem<ModelController>(meshID);
                    }
                }

                for (int v = 0; v < mesh.Vertices.Count; v++)
                {
                    Vertex[] vbuffer = new Vertex[mesh.Vertices[v].Length];
                    for (int k = 0; k < mesh.Vertices[v].Length; k++)
                    {
                        vbuffer[k] = mesh.Vertices[v][k];
                        Vector4 targetPos = new Vector4(mesh.Vertices[v][k].Pos.X, mesh.Vertices[v][k].Pos.Y, mesh.Vertices[v][k].Pos.Z, 1);
                        targetPos *= transMat;
                        mesh.Vertices[v][k].Pos = new Vector3(targetPos.X, targetPos.Y, targetPos.Z);
                    }

                    vtx[vtxIndex].Vtx = mesh.Vertices[v];
                    vtx[vtxIndex].VtxInd = mesh.Indices[v];
                    mesh.Vertices[v] = vbuffer;
                    UpdateVBO(vtxIndex);
                    vtxIndex++;
                }
            }
        }

        public void LoadOGI_PS2()
        {
            float min_x = float.MaxValue, min_y = float.MaxValue, min_z = float.MaxValue, max_x = float.MinValue, max_y = float.MinValue, max_z = float.MinValue;

            var vtxIndex = 0;
            if (graphicsInfo.Data.BlendSkinID != 0)
            {
                SectionController mesh_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(5);
                foreach (BlendSkin mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(5).Records)
                {
                    if (mod.ID == graphicsInfo.Data.BlendSkinID)
                    {
                        bskin = mesh_sec.GetItem<BlendSkinController>(mod.ID);
                    }
                }

                bskin.LoadMeshData();
                foreach (var list in bskin.Vertices)
                {
                    foreach (var v in list)
                    {
                        min_x = Math.Min(min_x, v.Pos.X);
                        min_y = Math.Min(min_y, v.Pos.Y);
                        min_z = Math.Min(min_z, v.Pos.Z);
                        max_x = Math.Max(max_x, v.Pos.X);
                        max_y = Math.Max(max_y, v.Pos.Y);
                        max_z = Math.Max(max_z, v.Pos.Z);
                    }
                }
                for (int i = 0; i < bskin.Vertices.Count; i++)
                {
                    vtx[vtxIndex].Vtx = bskin.Vertices[i];
                    vtx[vtxIndex].VtxInd = bskin.Indices[i];
                    Utils.TextUtils.LoadTexture(bskin.Data.Models.Select((subModel) =>
                    {
                        return subModel.MaterialID;
                    }).ToArray(), file, vtx[vtxIndex], i);
                    UpdateVBO(vtxIndex);
                    vtxIndex++;
                }
            }
            bskinEndIndex = vtxIndex;
            if (graphicsInfo.Data.SkinID != 0)
            {
                SectionController mesh_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(4);
                foreach (Skin mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(4).Records)
                {
                    if (mod.ID == graphicsInfo.Data.SkinID)
                    {
                        skin = mesh_sec.GetItem<SkinController>(mod.ID);
                    }
                }

                skin.LoadMeshData();
                foreach (var list in skin.Vertices)
                {
                    foreach (var v in list)
                    {
                        min_x = Math.Min(min_x, v.Pos.X);
                        min_y = Math.Min(min_y, v.Pos.Y);
                        min_z = Math.Min(min_z, v.Pos.Z);
                        max_x = Math.Max(max_x, v.Pos.X);
                        max_y = Math.Max(max_y, v.Pos.Y);
                        max_z = Math.Max(max_z, v.Pos.Z);
                    }
                }
                for (int i = 0; i < skin.Vertices.Count; i++)
                {
                    vtx[vtxIndex].Vtx = skin.Vertices[i];
                    vtx[vtxIndex].VtxInd = skin.Indices[i];
                    Utils.TextUtils.LoadTexture(skin.Data.SubModels.Select((subModel) =>
                    {
                        return subModel.MaterialID;
                    }).ToArray(), file, vtx[vtxIndex], i);
                    UpdateVBO(vtxIndex);
                    vtxIndex++;
                }
            }
            skinEndIndex = vtxIndex;
            for (int i = 0; i < graphicsInfo.Data.ModelIDs.Length; i++)
            {
                SectionController mesh_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(2);
                SectionController rigid_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(3);
                RigidModelController rigid = null;
                foreach (RigidModel mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).Records)
                {
                    if (mod.ID == graphicsInfo.Data.ModelIDs[i].ModelID)
                    {
                        uint meshID = targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).GetItem<RigidModel>(mod.ID).MeshID;
                        mesh = mesh_sec.GetItem<ModelController>(meshID);
                        rigid = rigid_sec.GetItem<RigidModelController>(mod.ID);
                    }
                }

                Matrix4 tempRot = Matrix4.Identity;

                // Rotation
                tempRot.M11 = -graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[0].X;
                tempRot.M12 = -graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[1].X;
                tempRot.M13 = -graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[2].X;

                tempRot.M21 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[0].Y;
                tempRot.M22 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[1].Y;
                tempRot.M23 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[2].Y;

                tempRot.M31 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[0].Z;
                tempRot.M32 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[1].Z;
                tempRot.M33 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[2].Z;

                tempRot.M14 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[0].W;
                tempRot.M24 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[1].W;
                tempRot.M34 = graphicsInfo.Data.Type3[graphicsInfo.Data.ModelIDs[i].ID].Matrix[2].W;

                // Position
                tempRot.M41 = graphicsInfo.Data.Joints[graphicsInfo.Data.ModelIDs[i].ID].Matrix[1].X;
                tempRot.M42 = graphicsInfo.Data.Joints[graphicsInfo.Data.ModelIDs[i].ID].Matrix[1].Y;
                tempRot.M43 = graphicsInfo.Data.Joints[graphicsInfo.Data.ModelIDs[i].ID].Matrix[1].Z;
                tempRot.M44 = graphicsInfo.Data.Joints[graphicsInfo.Data.ModelIDs[i].ID].Matrix[1].W;

                // Adjusted for OpenTK
                tempRot *= Matrix4.CreateScale(-1, 1, 1);
                mesh.LoadMeshData();



                for (int v = 0; v < mesh.Vertices.Count; v++)
                {
                    Vertex[] vbuffer = new Vertex[mesh.Vertices[v].Length];
                    for (int k = 0; k < mesh.Vertices[v].Length; k++)
                    {
                        vbuffer[k] = mesh.Vertices[v][k];
                        Vector4 targetPos = new Vector4(mesh.Vertices[v][k].Pos.X, mesh.Vertices[v][k].Pos.Y, mesh.Vertices[v][k].Pos.Z, 1);
                        targetPos *= tempRot;
                        mesh.Vertices[v][k].Pos = new Vector3(targetPos.X, targetPos.Y, targetPos.Z);
                    }


                    foreach (var p in mesh.Vertices[v])
                    {
                        min_x = Math.Min(min_x, p.Pos.X);
                        min_y = Math.Min(min_y, p.Pos.Y);
                        min_z = Math.Min(min_z, p.Pos.Z);
                        max_x = Math.Max(max_x, p.Pos.X);
                        max_y = Math.Max(max_y, p.Pos.Y);
                        max_z = Math.Max(max_z, p.Pos.Z);
                    }
                    if (rigid != null)
                    {
                        Utils.TextUtils.LoadTexture(rigid.Data.MaterialIDs, file, vtx[vtxIndex], v);
                    }
                    vtx[vtxIndex].Vtx = mesh.Vertices[v];
                    vtx[vtxIndex].VtxInd = mesh.Indices[v];
                    mesh.Vertices[v] = vbuffer;
                    UpdateVBO(vtxIndex);
                    vtxIndex++;
                }



            }

            zFar = Math.Max(zFar, Math.Max(max_x - min_x, Math.Max(max_y - min_y, max_z - min_z)));
        }


        public void ChangeGraphicsInfo(GraphicsInfoController mesh)
        {
            graphicsInfo = mesh;
            Utils.TextUtils.ClearTextureCache();
            SetupVBORender();
        }
    }
}
