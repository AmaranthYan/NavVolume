using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NavVolume.Editor
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    public class Obstacle : MonoBehaviour
    {
        [SerializeField]
        private Mesh m_Mesh;

        [Header("Debug")]
        [SerializeField]
        private bool m_DebugDraw = false;
        [SerializeField]
        private Material m_DebugMaterial;
        
        private bool m_IsDirty = false;

        public Mesh Mesh
        {
            get
            {
                return m_Mesh;
            }
        }

        private void Update()
        {
            if (m_IsDirty)
            {
                this.GetComponent<MeshFilter>().sharedMesh = m_Mesh;
                var renderer = this.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = m_DebugMaterial;
                renderer.enabled = m_DebugDraw;
                m_IsDirty = false;
            }
        }

        private void OnValidate()
        {
            m_IsDirty = true;            
        }
    }
}
