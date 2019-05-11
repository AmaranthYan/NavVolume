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
                m_Reset2Kernel = m_OverlapShader.FindKernel("Reset2");
                m_Reset32Kernel = m_OverlapShader.FindKernel("Reset32");
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
        
        [MenuItem("NavVolume/Editor")]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EditorWindow.GetWindow(typeof(OctreeEditor), true, "NavVolume Editor");
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
        float m_EdgeLength = 10f;
        private void OnGUI()
        {   
            GUILayout.Label(state);
            
            GUIStateButton(State.Initialized, "Extract Obstacle Triangles", ExtractObstacleTriangles);

            depth = EditorGUILayout.IntSlider("Octree Depth", depth, MIN_OCTREE_DEPTH, MAX_OCTREE_DEPTH);
            m_EdgeLength = EditorGUILayout.FloatField("Edge Size", m_EdgeLength);
            GUIStateButton(State.Extracted, "Generate Grid Data", GenerateGridData);

            GUIStateButton(State.Generated, "Construct NavVolume", () => { });            
        }

        int length = 0;
        List<Vector3> debugCubes = new List<Vector3>();
        float cubeEdge = 0;
        private void OnSceneGUI(SceneView sceneView)
        {
            
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Less;
            Vector3 cube = new Vector3(cubeEdge, cubeEdge, cubeEdge);

            Handles.DrawWireCube(Vector3.zero, Vector3.one * m_EdgeLength);

            for (int i = 0; i < length; i++)
            {
                Handles.color = Color.red;
                Handles.DrawWireCube(debugCubes[i], cube);                
                //sum += data[i];
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
        
        int[] data = new int[2 << ((MAX_OCTREE_DEPTH - 2) * 3)];

        private const string OVERLAP_SHADER_PATH = "Assets/CShader/CubeTriangleOverlap.compute";
        private ComputeShader m_OverlapShader;
        private int m_ComputeKernel;
        private int m_Reset2Kernel;
        private int m_Reset32Kernel;

        private const int MIN_OCTREE_DEPTH = 2;
        private const int MAX_OCTREE_DEPTH = 7;

        int depth = MIN_OCTREE_DEPTH;
        
        private void GenerateGridData()
        {
            var triangles = new ComputeBuffer(m_ObstacleTriangles.Count, sizeof(float) * 45);
            triangles.SetData(m_ObstacleTriangles);
            m_OverlapShader.SetBuffer(m_ComputeKernel, "input", triangles);

            int group = 1 << (depth - 2);
            int ggg = group * group * group;

            var grid = new ComputeBuffer(ggg * 2, sizeof(int), ComputeBufferType.Raw);
            m_OverlapShader.SetBuffer(m_ComputeKernel, "output", grid);

            // reset buffer
            if (depth <= 3)
            {
                m_OverlapShader.SetBuffer(m_Reset2Kernel, "output", grid);
                m_OverlapShader.Dispatch(m_Reset2Kernel, ggg, 1, 1);
            }
            else
            {
                m_OverlapShader.SetBuffer(m_Reset32Kernel, "output", grid);
                m_OverlapShader.Dispatch(m_Reset32Kernel, ggg / 16, 1, 1);
            }            

            m_OverlapShader.SetFloat("half_edge", m_EdgeLength / (1 << depth) / 2);
            m_OverlapShader.SetInt("tri_count", m_ObstacleTriangles.Count);




            int sqrt = (int)Mathf.Ceil(Mathf.Sqrt(m_ObstacleTriangles.Count));
            m_OverlapShader.SetInt("tri_count_sqrt", sqrt);
            
            float corner = -m_EdgeLength / 2;

            // unity will crash if dispatch too many thread groups at once
            // use multiple dispatch instead
            if (depth == 7)
            {
                for (int i = 0; i < 8; i++)
                {
                    m_OverlapShader.SetInt("offset", i * 8192);
                    float corner_x = corner - corner * (i & 1);
                    float corner_y = corner - corner * ((i >> 1) & 1);
                    float corner_z = corner - corner * ((i >> 2) & 1);
                    m_OverlapShader.SetFloats("corner", corner_x, corner_y, corner_z);
                    m_OverlapShader.Dispatch(m_ComputeKernel, 16 * sqrt, 16 * sqrt, 16);
                }
            }
            else
            {
                m_OverlapShader.SetInt("offset", 0);
                m_OverlapShader.SetFloats("corner", corner, corner, corner);
                m_OverlapShader.Dispatch(m_ComputeKernel, group * sqrt, group * sqrt, group);                
            }
            grid.GetData(data);
            
            grid.Release();
            triangles.Release();

            //return;

            length = 0;
            Vector3 root = Vector3.zero;
            float edge = m_EdgeLength;

            int total = 2 << ((depth - 2) * 3);
            for (int i = 0; i < total * 32; i++)
            {
                int k = 0;

                int c = i;
                var sub_root = root;
                int p = total * 32;
                do
                {
                    p /= 8;
                    int sub = c % p;
                    c = c / p;

                    var offset = new Vector3(c & 1, (c / 2) & 1, (c / 4) & 1);
                    offset = 2 * offset - Vector3.one;
                    sub_root += edge / (1 << (k + 1)) / 2 * offset;

                    c = sub;
                } while (++k < depth);

                if ((data[i / 32] & (1 << (i % 32))) != 0)
                {
                    if (++length > debugCubes.Count)
                    {
                        debugCubes.Add(sub_root);
                    }
                    else
                    {
                        debugCubes[length - 1] = sub_root;
                    }
                }                
            }
            cubeEdge = m_EdgeLength / (1 << depth);

            SceneView.RepaintAll();
        }
    }
}