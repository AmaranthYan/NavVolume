using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NavVolume.Editor
{
    public class ObstacleTriangle
    {
        public Vector3[] Vertices;
        // min, max
        public Vector3[] AABB;
        public Vector3 Normal;
        public Vector3[] Axes = new Vector3[9];

        public ObstacleTriangle(Vector3[] vert, params int[] idx)
        {
            Vertices = new Vector3[3]
            {
                vert[idx[0]], vert[idx[1]], vert[idx[2]]
            };
            
            AABB = new Vector3[2]
            {
                Vector3.positiveInfinity,Vector3.negativeInfinity
            };
            
            for (int i = 0; i < 3; i++)
            {
                AABB[0] = Vector3.Min(Vertices[i], AABB[0]);
                AABB[1] = Vector3.Max(Vertices[i], AABB[1]);
                
                Vector3 edge = Vertices[(i + 1) % 3] - Vertices[i];

                Axes[i] = new Vector3(0, -edge.z, edge.y);
                Axes[i + 3] = new Vector3(edge.z, 0, -edge.x);
                Axes[i + 6] = new Vector3(-edge.y, edge.x, 0);
            }            

            Normal = Vector3.Cross(Vertices[0], Vertices[1]);
        }
    }
}
