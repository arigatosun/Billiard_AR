/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;
using Unity.Mathematics;

namespace ibc.unity
{
    using objects;

    [ExecuteInEditMode]
    public class UnityBall : MonoBehaviour, IIdentifiable
    {
        public int Identifier;
        public double Mass = 0.1555;
        public double Radius = 0.05715 / 2.0;
        public double ModelScaleFactor = 1f;

        public Ball Data => new Ball(Identifier, (float3)transform.position, transform.rotation, Mass, Radius);

        private void Update()
        {
#if UNITY_EDITOR
            transform.localScale = Vector3.one * (float)(Radius * 2.0 * ModelScaleFactor);
#endif
        }

        public int GetIdentifier()
        {
            return Identifier;
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR

            if (UnityEditor.Selection.Contains(gameObject))
                Gizmos.color = Color.red;
            else Gizmos.color = Color.white;
#endif
            Gizmos.DrawWireSphere(transform.position, (float)Radius);
        }
    }
}