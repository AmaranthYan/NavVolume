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
            None,
            Initialized,
            Extracted,
            Generated,
            Constructed,
        }
        private State m_State = State.None;
        private bool m_IsProcessing = false;
        
        ThreadPool m_ThreadPool;
        private void OnEnable()
        {
            SceneView.onSceneGUIDelegate += OnSceneGUI;

            m_ThreadPool = new ThreadPool(8);
            
            m_OverlapShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(OVERLAP_SHADER_PATH);
            if (m_OverlapShader)
            {
                m_ComputeKernel = m_OverlapShader.FindKernel("Compute");
                m_ResetKernel = m_OverlapShader.FindKernel("Reset");
            }
            else
            {
                Debug.LogError("Load compute shader failed at " + OVERLAP_SHADER_PATH);
                return;
            }
            
            m_State = State.Initialized;
        }

        private void OnDestroy()
        {
            m_ThreadPool.Dispose();
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
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
        
        private void Update()
        {
            switch (m_State)
            {
                case State.None:
                    state = "editor initialization failed, check console for errors";
                    break;

                case State.Initialized:
                    if (m_BatchCount > 0)
                    {
                        if (m_BatchCount == m_BatchDone)
                        {
                            m_State = State.Extracted;
                            m_IsProcessing = false;

                            Repaint();
                        }
                        else
                        {
                            state = "extracting triangles";
                        }
                    }
                    else
                    {
                        state = "editor initialized";
                    }
                    break;


                case State.Extracted:
                    state = "triangles extracted";
                    break;

            }
        }

        string state = string.Empty;
        private void OnGUI()
        {   
            GUILayout.Label(state);
            
            GUIStateButton(State.Initialized, "Extract Obstacle Triangles", ExtractObstacleTriangles);
            GUIStateButton(State.Extracted, "Generate Octree Data", GenerateOctreeData);
            GUIStateButton(State.Generated, "Construct NavVolume", () => { });            
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            Vector3 cube = new Vector3(1, 1, 1);

            Vector3 root = Vector3.zero;
            float edge = 4;

            int sum = 0;
            for (int i = 0; i < 64; i++)
            {
                int k = 0;

                int c = i;
                var sub_root = root;
                int p = 8;
                do
                {
                    int sub = c % p;
                    c = c / p;

                    var offset = new Vector3(c & 1, (c / 2) & 1, (c / 4) & 1);
                    offset = 2 * offset - Vector3.one;
                    sub_root += edge / (1 << (k + 1)) / 2 * offset;

                    c = sub;
                    p /= 8;
                } while (++k < 2);

                Handles.zTest = UnityEngine.Rendering.CompareFunction.Less;
                Handles.color = Color.white;
                if (data[i] > 0)
                {
                    Handles.color = Color.red;
                    Handles.DrawWireCube(sub_root, cube);
                }
                sum += data[i];
                //if (r2[i] > 0)
                //{
                //    Gizmos.color = Color.red;
                //    Gizmos.DrawCube(r[i], cube);
                //}
                //else
                //{
                //    Gizmos.color = Color.white;
                //    Gizmos.DrawWireCube(r[i], cube);
                //}
            }
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
                            List<ObstacleTriangle> batch = new List<ObstacleTriangle>();
                            for (int i = idx; i < idx + j; i += 3)
                            {
                                var t = new ObstacleTriangle(vertices, triangles[i], triangles[i + 1], triangles[i + 2]);
                                batch.Add(t);
                            }

                            lock (m_ObstacleTriangles)
                            {
                                m_ObstacleTriangles.AddRange(batch);
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

            if (m_BatchCount > 0)
            {
                m_IsProcessing = true;
            }
        }
        
        int[] data = new int[64];

        private const string OVERLAP_SHADER_PATH = "Assets/CShader/CubeTriangleOverlap.compute";
        private ComputeShader m_OverlapShader;
        private int m_ComputeKernel;
        private int m_ResetKernel;
        private void GenerateOctreeData()
        {
            var triangles = new ComputeBuffer(m_ObstacleTriangles.Count, sizeof(float) * 45);
            triangles.SetData(m_ObstacleTriangles);
            m_OverlapShader.SetBuffer(m_ComputeKernel, "input", triangles);

            var octree = new ComputeBuffer(64, sizeof(int), ComputeBufferType.Raw);
            m_OverlapShader.SetBuffer(m_ComputeKernel, "output", octree);

            // reset buffer
            m_OverlapShader.SetBuffer(m_ResetKernel, "output", octree);
            m_OverlapShader.Dispatch(m_ResetKernel, 1, 1, 1);

            m_OverlapShader.SetFloats("center", new float[3] { 0, 0, 0 });
            m_OverlapShader.SetFloat("half_edge", 0.5f);

            m_OverlapShader.Dispatch(m_ComputeKernel, m_ObstacleTriangles.Count, 1, 1);
            octree.GetData(data);

            foreach (var d in data)
            {
                Debug.Log(d);
            }

            octree.Release();
            triangles.Release();
            
            SceneView.RepaintAll();
        }
    }
}