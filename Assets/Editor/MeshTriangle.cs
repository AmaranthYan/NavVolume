using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NavVolume.Utility;

public class MeshTriangle : EditorWindow
{
    ThreadPool m_ThreadPool;
    private void OnEnable()
    {
        m_ThreadPool = new ThreadPool(8);
    }

    private void OnDestroy()
    {
        m_ThreadPool.Dispose();
    }

    // Add menu item named "My Window" to the Window menu
    [MenuItem("MyMenu/Do Something")]
    public static void ShowWindow()
    {
        //Show existing window instance. If one doesn't exist, make one.
        EditorWindow.GetWindow(typeof(MeshTriangle));
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Base Settings"))
        {
            Do();
        }
    }

    class Triangle
    {
        public Vector3[] vertices = new Vector3[3];
        //public Vector3[] edges = new Vector3[3];
        // min, max
        public Vector3[] aabb = new Vector3[2];
        public Vector3 normal;
        public Vector3[] axes = new Vector3[9];
    }

    int Batch = 200;
    HashSet<Triangle> t = new HashSet<Triangle>();
    private void Do()
    {
        var obstacles = GameObject.FindGameObjectsWithTag("Obstacle");

        foreach (var o in obstacles)
        {
            var mesh = o.GetComponent<MeshFilter>()?.sharedMesh;
            
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

                Debug.Log("triangles" + triangles.Length);
                int k = 0;
                while (k < triangles.Length)
                {
                    int idx = k;
                    int j = Mathf.Min(triangles.Length - idx, Batch * 3);

                    Debug.Log("idx " + idx + " count "+ j);
                    m_ThreadPool.QueueTask(() =>
                    {
                        List<Triangle> tri = new List<Triangle>();
                        for (int i = idx; i < idx + j; i += 3)
                        {
                            var tr = new Triangle();
                            tr.vertices[0] = vertices[triangles[i]];
                            tr.vertices[1] = vertices[triangles[i + 1]];
                            tr.vertices[2] = vertices[triangles[i + 2]];
                            tri.Add(tr);
                        }
                        Debug.Log("thread " + idx + " " + tri.Count);

                        lock (t)
                        {
                            t.UnionWith(tri);
                            Debug.Log("total " + t.Count);
                        }
                    });

                    k += Batch * 3;
                }                
            }
            else
            {
                Debug.LogWarning("no mesh");
            }

        }
    }
}
