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
        private readonly Dictionary<int, int> VbufferMap = new Dictionary<int, int>();

        public int FPS { get => player.FPS; set { player.FPS = value; if (animUpdateTimer != null) animUpdateTimer.Interval = (int)Math.Floor(1.0 / FPS * 1000); } }
        public bool Loop { get => player.Loop; set => player.Loop = value; }
        public bool Playing { get => player.Playing; set => player.Playing = value; }
        public bool Finished { get => player.Finished; }

        private Timer animUpdateTimer;
        

        private int bskinEndIndex = 0;
        private int skinEndIndex = 0;

        public AnimationViewer()
        {
            player = new AnimationPlayer();
        }

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
            foreach(var pair in graphicsInfo.Data.ModelIDs)
            {
                foreach (RigidModel mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).Records)
                {
                    if (mod.ID == pair.Value.ModelID)
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
            DrawSkeleton();

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

        private void DrawSkeleton()
        {
            GL.Begin(PrimitiveType.Lines);
            GL.LineWidth(10f);
            GL.Color3(Color.White);
            var skeleton = graphicsInfo.Data.Skeleton;
            DrawJoint(skeleton.Root, Matrix4.Identity);
            GL.End();
        }

        private void DrawJoint(GraphicsInfo.JointNode joint, Matrix4 parentTransform)
        {
            var transforms = player.Play((int)joint.Joint.JointIndex);
            var localTransform = Matrix4.CreateFromQuaternion(new Quaternion(transforms.Row2.Xyz))
                * Matrix4.CreateScale(transforms.Row1.Xyz)
                * Matrix4.CreateTranslation(transforms.Row0.Xyz);
            /*var localTransform = Matrix4.CreateFromQuaternion(new Quaternion(
                graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[2].X,
                graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[2].Y,
                graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[2].Z,
                graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[2].W));*/
            var childTransform = localTransform * parentTransform;
            /*var childTranslation = childTransform * parentTranslation + childTransform * (new Vector4(
                    graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[0].X,
                    graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[0].Y,
                    graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[0].Z,
                    1.0f
                ));*/
            /*var childTranslation = childTransform * parentTranslation + childTransform * transforms.Row0;
            childTranslation.W = 1.0f;*/
            foreach (var c in joint.Children)
            {
                DrawJoint(c, childTransform);
            }

            var models = graphicsInfo.Data.ModelIDs.Where((v) => v.Value.JointIndex == joint.Joint.JointIndex);

            var jointTransform = childTransform;

            Matrix4 tempRot = Matrix4.Identity;
            /*tempRot *= Matrix4.CreateFromQuaternion(new Quaternion(joint.Joint.Matrix[2].X, joint.Joint.Matrix[2].Y, joint.Joint.Matrix[2].Z));
            tempRot *= Matrix4.CreateTranslation(new Vector3(joint.Joint.Matrix[0].X, joint.Joint.Matrix[0].Y, joint.Joint.Matrix[0].Z));*/

            // Rotation
            tempRot.M11 = -jointTransform.M11;
            tempRot.M12 = -jointTransform.M12;
            tempRot.M13 = -jointTransform.M13;

            tempRot.M21 = jointTransform.M21;
            tempRot.M22 = jointTransform.M22;
            tempRot.M23 = jointTransform.M23;

            tempRot.M31 = jointTransform.M31;
            tempRot.M32 = jointTransform.M32;
            tempRot.M33 = jointTransform.M33;

            tempRot.M14 = jointTransform.M14;
            tempRot.M24 = jointTransform.M24;
            tempRot.M34 = jointTransform.M34;

            // Position
            //tempRot.M41 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].X;
            //tempRot.M42 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].Y;
            //tempRot.M43 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].Z;
            //tempRot.M44 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].W;
            tempRot.Row3 = jointTransform.Row3;

            //tempRot.Row3 += transforms.Row0;

            //tempRot *= transMat;
            tempRot *= Matrix4.CreateScale(-1, 1, 1);
            GL.Vertex3(tempRot.ExtractTranslation());

        }

        private void UpdateAnimation(float deltaTime)
        {
            player.AdvanceClock(deltaTime);

            AnimateSkeleton(graphicsInfo.Data);
        }

        private void AnimateSkeleton(GraphicsInfo ogi)
        {
            var skeleton = ogi.Skeleton;
            var transforms = player.Play((int)skeleton.Root.Joint.JointIndex);
            var localTransform = Matrix4.CreateFromQuaternion(new Quaternion(transforms.Row2.Xyz));
            AnimateJoint(skeleton.Root, localTransform);
        }

        private void AnimateJoint(GraphicsInfo.JointNode joint, Matrix4 parentTransform)
        {
            //Matrix4 tempRot = Matrix4.Identity;

            // Rotation
            /*tempRot.M11 = -graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[0].X;
            tempRot.M12 = -graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[1].X;
            tempRot.M13 = -graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[2].X;

            tempRot.M21 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[0].Y;
            tempRot.M22 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[1].Y;
            tempRot.M23 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[2].Y;

            tempRot.M31 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[0].Z;
            tempRot.M32 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[1].Z;
            tempRot.M33 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[2].Z;

            tempRot.M14 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[0].W;
            tempRot.M24 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[1].W;
            tempRot.M34 = graphicsInfo.Data.Type3[joint.Joint.JointIndex].Matrix[2].W;

            // Position
            tempRot.M41 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].X;
            tempRot.M42 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].Y;
            tempRot.M43 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].Z;
            tempRot.M44 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].W;*/

            var transforms = player.Play((int)joint.Joint.JointIndex);
            var localTransform = Matrix4.CreateFromQuaternion(new Quaternion(transforms.Row2.Xyz))
                * Matrix4.CreateScale(transforms.Row1.Xyz)
                * Matrix4.CreateTranslation(transforms.Row0.Xyz);
            /*var localTransform = Matrix4.CreateFromQuaternion(new Quaternion(
                graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[2].X,
                graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[2].Y,
                graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[2].Z,
                graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[2].W));*/
            var childTransform = localTransform * parentTransform;
            /*var childTranslation = childTransform * parentTranslation + childTransform * (new Vector4(
                    graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[0].X,
                    graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[0].Y,
                    graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[0].Z,
                    1.0f
                ));*/
            /*var childTranslation = childTransform * parentTranslation + childTransform * transforms.Row0;
            childTranslation.W = 1.0f;*/
            foreach (var c in joint.Children)
            {
                AnimateJoint(c, childTransform);
            }

            var models = graphicsInfo.Data.ModelIDs.Where((v) => v.Value.JointIndex == joint.Joint.JointIndex);

            var jointTransform = childTransform;

            Matrix4 tempRot = Matrix4.Identity;
            /*tempRot *= Matrix4.CreateFromQuaternion(new Quaternion(joint.Joint.Matrix[2].X, joint.Joint.Matrix[2].Y, joint.Joint.Matrix[2].Z));
            tempRot *= Matrix4.CreateTranslation(new Vector3(joint.Joint.Matrix[0].X, joint.Joint.Matrix[0].Y, joint.Joint.Matrix[0].Z));*/

            // Rotation
            tempRot.M11 = -jointTransform.M11;
            tempRot.M12 = -jointTransform.M12;
            tempRot.M13 = -jointTransform.M13;

            tempRot.M21 = jointTransform.M21;
            tempRot.M22 = jointTransform.M22;
            tempRot.M23 = jointTransform.M23;

            tempRot.M31 = jointTransform.M31;
            tempRot.M32 = jointTransform.M32;
            tempRot.M33 = jointTransform.M33;

            tempRot.M14 = jointTransform.M14;
            tempRot.M24 = jointTransform.M24;
            tempRot.M34 = jointTransform.M34;

            // Position
            //tempRot.M41 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].X;
            //tempRot.M42 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].Y;
            //tempRot.M43 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].Z;
            //tempRot.M44 = graphicsInfo.Data.Joints[joint.Joint.JointIndex].Matrix[1].W;
            tempRot.Row3 = jointTransform.Row3;
            
            //tempRot.Row3 += transforms.Row0;

            //tempRot *= transMat;
            tempRot *= Matrix4.CreateScale(-1, 1, 1);

            foreach (var model in models)
            {
                SectionController mesh_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(2);
                SectionController rigid_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(3);
                foreach (RigidModel mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).Records)
                {
                    if (mod.ID == model.Value.ModelID)
                    {
                        uint meshID = targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).GetItem<RigidModel>(mod.ID).MeshID;
                        mesh = mesh_sec.GetItem<ModelController>(meshID);
                    }
                }

                var vtxIndex = VbufferMap[model.Key];
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
            foreach (var pair in graphicsInfo.Data.ModelIDs)
            {
                SectionController mesh_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(2);
                SectionController rigid_sec = targetFile.GetItem<SectionController>(11).GetItem<SectionController>(3);
                RigidModelController rigid = null;
                foreach (RigidModel mod in targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).Records)
                {
                    if (mod.ID == pair.Value.ModelID)
                    {
                        uint meshID = targetFile.Data.GetItem<TwinsSection>(11).GetItem<TwinsSection>(3).GetItem<RigidModel>(mod.ID).MeshID;
                        mesh = mesh_sec.GetItem<ModelController>(meshID);
                        rigid = rigid_sec.GetItem<RigidModelController>(mod.ID);
                    }
                }

                Matrix4 tempRot = Matrix4.Identity;

                // Rotation
                tempRot.M11 = -graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[0].X;
                tempRot.M12 = -graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[1].X;
                tempRot.M13 = -graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[2].X;

                tempRot.M21 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[0].Y;
                tempRot.M22 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[1].Y;
                tempRot.M23 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[2].Y;

                tempRot.M31 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[0].Z;
                tempRot.M32 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[1].Z;
                tempRot.M33 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[2].Z;

                tempRot.M14 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[0].W;
                tempRot.M24 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[1].W;
                tempRot.M34 = graphicsInfo.Data.JointToWorldTransforms[pair.Value.JointIndex].Matrix[2].W;

                // Position
                tempRot.M41 = graphicsInfo.Data.Joints[pair.Value.JointIndex].Matrix[1].X;
                tempRot.M42 = graphicsInfo.Data.Joints[pair.Value.JointIndex].Matrix[1].Y;
                tempRot.M43 = graphicsInfo.Data.Joints[pair.Value.JointIndex].Matrix[1].Z;
                tempRot.M44 = graphicsInfo.Data.Joints[pair.Value.JointIndex].Matrix[1].W;

                // Adjusted for OpenTK
                tempRot *= Matrix4.CreateScale(-1, 1, 1);
                mesh.LoadMeshData();


                VbufferMap.Add(pair.Key, vtxIndex);
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
            VbufferMap.Clear();
            Utils.TextUtils.ClearTextureCache();
            SetupVBORender();
        }

        protected override void Dispose(bool disposing)
        {
            if (animUpdateTimer != null)
            {
                animUpdateTimer.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
}
