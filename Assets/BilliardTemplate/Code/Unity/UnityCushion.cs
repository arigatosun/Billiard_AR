/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */
using ibc.objects;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace ibc.unity
{

    public class UnityCushion : MonoBehaviour, IIdentifiable
    {
        public int Identifier;
        public float Height;
        public List<float3> Points;
        public float handleRadius = 0.01f;

        public int GetIdentifier()
        {
            return Identifier;
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR

            if (UnityEditor.Selection.Contains(gameObject))
                return;
            if (Points != null)
            {
                for (var i = 0; i < Points.Count; i++)
                {
                    float3 nextPoint = Points[(i + 1) % Points.Count];
                    Gizmos.DrawSphere(Points[i], 0.01f);
                    Gizmos.DrawLine(Points[i] + new float3(0, 1, 0) * Height, nextPoint + new float3(0, 1, 0) * Height);
                }
            }
#endif

        }

    }
}