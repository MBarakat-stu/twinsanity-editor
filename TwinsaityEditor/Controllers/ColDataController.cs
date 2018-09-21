﻿using Twinsanity;

namespace TwinsaityEditor
{
    public class ColDataController : ItemController
    {
        private ColData data;

        public ColDataController(ColData item) : base(item)
        {
            data = item;
            AddMenu("Open RMViewer", Menu_OpenRMViewer);
        }

        public override string GetName()
        {
            return "Collision Data [ID " + data.ID + "]";
        }

        public override void GenText()
        {
            TextPrev = new string[6 + data.Triggers.Count + data.Groups.Count + data.Tris.Count + data.Vertices.Count];
            TextPrev[0] = "ID: " + data.ID;
            TextPrev[1] = "Offset: " + data.Offset + " Size: " + data.Size;

            TextPrev[2] = "TriggerCount: " + data.Triggers.Count;
            for (int i = 0; i < data.Triggers.Count; ++i)
                TextPrev[3 + i] = "Trigger" + i + ": " + "(" + data.Triggers[i].X1 + ", " + data.Triggers[i].Y1 + ", " + data.Triggers[i].Z1 + ")~(" + data.Triggers[i].X2 + ", " + data.Triggers[i].Y2 + ", " + data.Triggers[i].Z2 + ") | Nodes: " + data.Triggers[i].Flag1 + "~" + data.Triggers[i].Flag2;

            TextPrev[3 + data.Triggers.Count] = "GroupCount: " + data.Groups.Count;
            for (int i = 0; i < data.Groups.Count; ++i)
                TextPrev[4 + data.Triggers.Count + i] = "Group" + i + ": " + "PolygonOffset: " + data.Groups[i].Offset + " PolygonCount: " + data.Groups[i].Size;

            TextPrev[4 + data.Triggers.Count + data.Groups.Count] = "PolyCount: " + data.Tris.Count;
            for (int i = 0; i < data.Tris.Count; ++i)
                TextPrev[5 + data.Triggers.Count + data.Groups.Count + i] = "Polygon" + i + ": " + data.Tris[i].Vert1 + "|" + data.Tris[i].Vert2 + "|" + data.Tris[i].Vert3 + "|" + data.Tris[i].Surface;

            TextPrev[5 + data.Triggers.Count + data.Groups.Count + data.Tris.Count] = "VertexCount: " + data.Vertices.Count;
            for (int i = 0; i < data.Vertices.Count; ++i)
                TextPrev[6 + data.Triggers.Count + data.Groups.Count + data.Tris.Count + i] = "Vertex" + i + ": (" + data.Vertices[i].X + ", " + data.Vertices[i].Y + ", " + data.Vertices[i].Z + ", " + data.Vertices[i].W + ")";
        }

        private void Menu_OpenRMViewer()
        {
            MainForm.OpenRMViewer();
        }
    }
}
