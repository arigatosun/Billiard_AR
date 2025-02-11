/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;

namespace ibc.unity
{
    using ibc.solvers;
    using objects;


    [ExecuteInEditMode]
    /// <summary>
    /// Representation of the cue inside of the unity scene.
    /// </summary>
    public class UnityCue : MonoBehaviour, IIdentifiable
    {
        public int Identifier;

        /// <summary>Typical cue stick mass is between 0.45 to 0.6kg.</summary>
        public double Mass = 0.567;

        /// <summary>The length is around 1.5m. </summary>
        public double Length = 1.4732;

        /// <summary>The cue tip radius.</summary>
        public double CueTipRadius = 0.007;

        /// <summary>Impact parameters between cue and the ball</summary>
        public PhysicsImpactParameters ImpactParameters = new PhysicsImpactParameters
        {
            Friction = 0.87,
            NormalRestitution = 0.75,
            TangentialRestitution = 0.75,
        };

        /// <summary>
        /// Gets the cue data packed in Cue object from this unity object.
        /// </summary>
        public Cue Data => new Cue(Identifier, Mass, Length, CueTipRadius, ImpactParameters);

        [HideInInspector][SerializeField] private Transform _visual;

        private void OnEnable()
        {
            if(!_visual)
            {
                _visual = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                _visual.name = $"Visual";
                _visual.transform.SetParent(transform, false);
                _visual.transform.localPosition = new Vector3(0, 0, -(float)Length / 2.0f);
                float diameter = (float)CueTipRadius * 2;
                _visual.transform.localScale = new Vector3(diameter, diameter, (float)Length);
            }
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
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, transform.position - transform.forward * (float)Length);
        }
    }
}