/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using ibc.objects;
using System;
using UnityEngine;

namespace ibc.unity
{

    public class UnityHole : MonoBehaviour, IIdentifiable
    {
        public int Identifier;
        public double Radius;

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