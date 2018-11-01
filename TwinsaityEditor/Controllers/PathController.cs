﻿using Twinsanity;

namespace TwinsaityEditor
{
    public class PathController : ItemController
    {
        public new Path Data { get; set; }

        public PathController(Path item) : base(item)
        {
            Data = item;
        }

        protected override string GetName()
        {
            return "Path [ID " + Data.ID + "]";
        }

        protected override void GenText()
        {
            TextPrev = new string[4 + Data.Positions.Count + Data.Params.Count];
            TextPrev[0] = "ID: " + Data.ID;
            TextPrev[1] = "Offset: " + Data.Offset + " Size: " + Data.Size;
            TextPrev[2] = "Points: " + Data.Positions.Count;
            for (int i = 0; i < Data.Positions.Count; ++i)
            {
                TextPrev[3 + i] = "Point" + i + ": " + Data.Positions[i].X + ", " + Data.Positions[i].Y + ", " + Data.Positions[i].Z;
            }
            TextPrev[3 + Data.Positions.Count] = "Params: " + Data.Positions.Count;
            for (int i = 0; i < Data.Params.Count; ++i)
            {
                TextPrev[4 + Data.Positions.Count + i] = "Param" + i + ": " + Data.Params[i].Int1 + ", " + Data.Params[i].Int2;
            }
        }
    }
}
