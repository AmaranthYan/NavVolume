using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NavVolume.PathFinding
{
    public class PathFollower : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Running
        }

        [Header("Agent")]
        [SerializeField]
        private Rigidbody m_NavAgent;
        
        [Header("Path")]
        [SerializeField]
        private float m_PathStartRadius = 1f;
        [SerializeField]
        private float m_PathEndRadius = 1f;
        
        [Header("Motion")]
        [SerializeField]
        private float m_NavSpeed;
        [SerializeField]
        [Range(0, 1)]
        private float m_CompensationCoefficient;
        
        [Header("Debug")]
        [SerializeField]
        private bool m_DebugDraw = false;

        private NavPath m_NavPath = null;
        private Vector3 m_DummyPosition;
        private bool m_IsSimulating;

        public State NavState
        {
            get; private set;
        }

        NavPath _path = new NavPath()
        {
            Nodes = new Vector3[5]
            {
                new Vector3(5, -17, -9),
                new Vector3(-2, -10, 8),
                new Vector3(2,-5, 5),
                new Vector3(12,-9, 5),
                new Vector3(19,-9, 10)
            }
        };

        private void Awake()
        {
            if (m_NavAgent == null)
            {
                m_NavAgent = GetComponent<Rigidbody>();
            }
        }

        void Start()
        {
            NavState = State.Idle;

            // debug
            Navigate(_path);
            //Navigate(new NavPath());
        }

        private void OnDrawGizmos()
        {
            if (!m_DebugDraw)
            {
                return;
            }

            if (m_NavPath != null)
            {
                for (int i = 1; i < m_NavPath.Nodes.Length; i++)
                {
                    Gizmos.DrawLine(m_NavPath.Nodes[i - 1], m_NavPath.Nodes[i]);
                }
                
                Gizmos.DrawWireSphere(m_NavPath.Nodes[0], m_PathStartRadius);
                Gizmos.DrawWireSphere(m_NavPath.Nodes[m_NavPath.Nodes.Length - 1], m_PathStartRadius);

                Gizmos.DrawWireCube(m_DummyPosition, Vector3.one);
                var distance = Vector3.Distance(m_DummyPosition, m_NavAgent.transform.position);
                var k = 1 - Mathf.Clamp01(distance / (m_NavSpeed * Time.fixedDeltaTime) / 20); // magnify 20 times
                Gizmos.color = new Color(1, k, k);
                Gizmos.DrawLine(m_NavAgent.transform.position, m_DummyPosition);
            }
            
        }

        public void Navigate(NavPath path)
        {
            if (path?.Nodes?.Length >= 2)
            {
                if (Vector3.Distance(m_NavAgent.transform.position, path.Nodes[0]) <= m_PathStartRadius)
                {
                    StopAllCoroutines();

                    m_NavPath = path;

                    StartCoroutine(SimulatePath());
                    StartCoroutine(SetMotion());
                }
            }
            else
            {
                Debug.LogError("Invalid NavPath");
            }          
        }

        IEnumerator SimulatePath()
        {
            m_IsSimulating = true;

            m_DummyPosition = m_NavPath.Nodes[0];
            int next = 1;

            while (next < m_NavPath.Nodes.Length)
            {
                var distance = m_NavSpeed * Time.fixedDeltaTime;

                float remaining = 0;
                while ((remaining = (m_NavPath.Nodes[next] - m_DummyPosition).magnitude) <= distance)
                {
                    distance -= remaining;
                    m_DummyPosition = m_NavPath.Nodes[next];
                    
                    if (++next == m_NavPath.Nodes.Length)
                    {
                        break;
                    }
                }

                if (next < m_NavPath.Nodes.Length)
                {
                    var direction = m_NavPath.Nodes[next] - m_NavPath.Nodes[next - 1];
                    m_DummyPosition += direction * distance / direction.magnitude;
                }

                yield return new WaitForFixedUpdate();
            }

            m_IsSimulating = false;
        }

        IEnumerator SetMotion()
        {
            NavState = State.Running;

            while (m_IsSimulating || Vector3.Distance(m_NavAgent.transform.position, m_DummyPosition) > m_PathEndRadius)
            {
                var velocity = (m_DummyPosition - m_NavAgent.transform.position) / Time.fixedDeltaTime;

                float ratio = m_NavSpeed / velocity.magnitude;
                velocity *= (Mathf.Max(0, 1 - ratio) * m_CompensationCoefficient + Mathf.Min(ratio, 1));

                m_NavAgent.velocity = velocity;
                m_NavAgent.transform.rotation = Quaternion.LookRotation(velocity, Vector3.up);

                yield return new WaitForFixedUpdate();
            }

            m_NavAgent.velocity = Vector3.zero;

            NavState = State.Idle;
        }
    }

}