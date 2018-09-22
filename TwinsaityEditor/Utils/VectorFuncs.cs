﻿using OpenTK;

namespace TwinsaityEditor.Utils
{
    public static class VectorFuncs
    {
        public static Vector3 CalcNormal(Vector3 Vert1, Vector3 Vert2, Vector3 Vert3)
        {
            float x1 = Vert1.X;
            float y1 = Vert1.Y;
            float z1 = Vert1.Z;
            float x2 = Vert2.X;
            float y2 = Vert2.Y;
            float z2 = Vert2.Z;
            float x3 = Vert3.X;
            float y3 = Vert3.Y;
            float z3 = Vert3.Z;
            float nx = ((y1 - y2) * (z1 - z3)) - ((z1 - z2) * (y1 - y3));
            float ny = ((z1 - z2) * (x1 - x3)) - ((x1 - x2) * (z1 - z3));
            float nz = ((x1 - x2) * (y1 - y3)) - ((y1 - y2) * (x1 - x3));
            return new Vector3(nx, ny, nz);
        }
    }
}