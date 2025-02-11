/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;

namespace ibc.builders
{
    /// <summary>Stores required information to build basic billiard scene.</summary>
    public class BilliardData : ScriptableObject
    {
        [Header("Ball Properties")]
        public float BallMass = 0.1555f;
        public float BallRadius = 0.05715f / 2;
        public double BallScaleFactor = 1f;
        public GameObject SpherePrefab;
    }
}