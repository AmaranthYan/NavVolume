using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NavVolume.Editor
{
    public struct ObstacleTriangle
    {
        public Vector3 Vertex0;
        public Vector3 Vertex1;
        public Vector3 Vertex2;
        
        public Vector3 AABB0; // min
        public Vector3 AABB1; // max

        public Vector3 Normal;

        public Vector3 Axes0;
        public Vector3 Axes1;
        public Vector3 Axes2;
        public Vector3 Axes3;
        public Vector3 Axes4;
        public Vector3 Axes5;
        public Vector3 Axes6;
        public Vector3 Axes7;
        public Vector3 Axes8;

        public ObstacleTriangle(Vector3[] vert, params int[] idx)
        {
            Vector3[] vertices = new Vector3[3]
            {
                vert[idx[0]],
                vert[idx[1]],
                vert[idx[2]]
            };

            Vector3[] edges = new Vector3[3]
            {
                vertices[1] - vertices[0],
                vertices[2] - vertices[1],
                vertices[0] - vertices[2]
            };

            Vertex0 = vertices[0];
            Vertex1 = vertices[1];
            Vertex2 = vertices[2];

            AABB0 = Vector3.Min(Vector3.Min(Vertex0, Vertex1), Vertex2);
            AABB1 = Vector3.Max(Vector3.Max(Vertex0, Vertex1), Vertex2);

            Normal = Vector3.Cross(edges[0], edges[1]);
            Normal.Normalize();

            Axes0 = new Vector3(0, -edges[0].z, edges[0].y);
            Axes3 = new Vector3(edges[0].z, 0, -edges[0].x);
            Axes6 = new Vector3(-edges[0].y, edges[0].x, 0);

            Axes1 = new Vector3(0, -edges[1].z, edges[1].y);
            Axes4 = new Vector3(edges[1].z, 0, -edges[1].x);
            Axes7 = new Vector3(-edges[1].y, edges[1].x, 0);

            Axes2 = new Vector3(0, -edges[2].z, edges[2].y);
            Axes5 = new Vector3(edges[2].z, 0, -edges[2].x);
            Axes8 = new Vector3(-edges[2].y, edges[2].x, 0);            
        }
    }
}
