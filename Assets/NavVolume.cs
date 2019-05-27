using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NavVolume
{
    public class Octree
    {
        public WeakReference<Octree> Parent { get; private set; }
        public Octree[] Subtrees { get; private set; }
        public bool Occupied { get; private set; }

        public Octree(Octree parent)
        {
            Parent = new WeakReference<Octree>(parent);
        }

        public Octree[] Subdivide()
        {
            Subtrees = new Octree[8] 
            {
                new Octree(this),
                new Octree(this),
                new Octree(this),
                new Octree(this),
                new Octree(this),
                new Octree(this),
                new Octree(this),
                new Octree(this),
            };
            return Subtrees;
        }

        public void Occupy()
        {
            Occupied = true;

            Octree parent = null;
            if (Parent.TryGetTarget(out parent))
            {
                parent.Occupy();
            }
        }
    }

    public class NavVolume
    {
        public Vector3 Center;
        public int Depth;
        public Octree Root;

        public NavVolume(Vector3 center, int depth)
        {
            Center = center;
            Depth = depth;
            Root = new Octree(null);
        }
    }
}
