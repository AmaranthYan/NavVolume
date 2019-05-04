using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NavVolume.Utility;

namespace NavVolume.Editor
{
    public class OctreeEditor : EditorWindow
    {
        public enum State
        {
            Initialized,
            Extracted,
            Generated,
            Constructed,
        }
        private State m_State;
        private bool m_IsProcessing = false;

        ThreadPool m_ThreadPool;
        private void OnEnable()
        {
            m_ThreadPool = new ThreadPool(8);
            m_State = State.Initialized;
        }

        private void OnDestroy()
        {
            m_ThreadPool.Dispose();
        }
        
        [MenuItem("NavVolume/Octree Editor")]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EditorWindow.GetWindow(typeof(OctreeEditor), true, "Octree Editor");
        }
        
        void GUIStateButton(State prev, string text, Action func)
        {
            bool enabled = GUI.enabled;
            GUI.enabled = m_State >= prev && !m_IsProcessing;

            if (GUILayout.Button(text))
            {
                m_State = prev;
                func();
            }

            GUI.enabled = enabled;
        }

        private void OnGUI()
        {
            m_IsProcessing = false;
            string state = "none";
            switch (m_State)
            {
                case State.Initialized:
                    if (m_BatchCount > 0)
                    {
                        if (m_BatchCount == m_BatchDone)
                        {
                            m_State = State.Extracted;
                        }
                        else
                        {
                            state = "extracting triangles";
                            Debug.Log(m_BatchCount + " " + m_BatchDone);

                            m_IsProcessing = true;
                        }
                    }
                    break;


                case State.Extracted:
                    state = "triangles extracted";
                    break;

            }
            
            
            GUILayout.Label(state);
            
            GUIStateButton(State.Initialized, "Extract Obstacle Triangles", ExtractObstacleTriangles);
            GUIStateButton(State.Extracted, "Generate Octree Data", () => { });
            GUIStateButton(State.Generated, "Construct NavVolume", () => { });            
        }
        
        private const int TRIANGLES_PER_BATCH = 10000;
        List<ObstacleTriangle> m_ObstacleTriangles = new List<ObstacleTriangle>();
        int m_BatchCount = 0;
        int m_BatchDone = 0;
        private void ExtractObstacleTriangles()
        {
            m_ObstacleTriangles.Clear();
            var obstacles = GameObject.FindObjectsOfType<Obstacle>();

            m_BatchCount = 0;
            m_BatchDone = 0;

            foreach (var o in obstacles)
            {
                var mesh = o.Mesh;

                if (mesh)
                {
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    var matrix = o.transform.localToWorldMatrix;
                    // transform vertices from object space to world space
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                    }
                    
                    int k = 0;
                    while (k < triangles.Length)
                    {
                        int idx = k;
                        int j = Mathf.Min(triangles.Length - idx, TRIANGLES_PER_BATCH * 3);

                        m_BatchCount++;
                        m_ThreadPool.QueueTask(() =>
                        {
                            List<ObstacleTriangle> tri = new List<ObstacleTriangle>();
                            for (int i = idx; i < idx + j; i += 3)
                            {
                                var tr = new ObstacleTriangle(vertices, triangles[i], triangles[i + 1], triangles[i + 2]);
                                tri.Add(tr);
                            }

                            lock (m_ObstacleTriangles)
                            {
                                m_ObstacleTriangles.AddRange(tri);
                                Debug.Log("total " + m_ObstacleTriangles.Count);

                                m_BatchDone++;
                            }
                        });

                        k += TRIANGLES_PER_BATCH * 3;
                    }
                }
                else
                {
                    Debug.LogWarning("Obstacle without mesh");
                }
            }
        }
    }
}