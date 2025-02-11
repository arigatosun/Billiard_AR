/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;

namespace ibc.solvers
{
    /// <summary>Class that holds all constants related to physics simulation and can be serialized by unity.</summary>
    [CreateAssetMenu(fileName = "Physics Solver Constants", menuName = "Billiard/Constants/Physics Solver")]
    public class PhysicsSolverConstantsSerializable : ScriptableObject
    {

        public PhysicsSolverConstants Data = new PhysicsSolverConstants()
        {
            Gravity = 9.81,
            
            SlidingFriction = 0.2, //old value was 0.044
            SpinningFriction = 0.2,
            RollingFriction = 0.015,

            BallToSlateImpactParameters = new PhysicsImpactParameters()
            {
                NormalRestitution = 0.5,
                TangentialRestitution = 0.5,
                Friction = 0.4,
            },

            BallToCushionImpactParameters = new PhysicsImpactParameters()
            {
                NormalRestitution = 0.8,
                TangentialRestitution = 0.8,
                Friction = 0.2,
            },

            BallToBallImpactParameters = new PhysicsImpactParameters()
            {
                NormalRestitution = 0.94,
                TangentialRestitution = 0.94,
                Friction = 0.06,
            },

            SleepVelocity = 1E-3,
            SleepAngularVelocity = 1E-3,

            PolynomialErrorBound = 1E-8,
            CushionSolverErrorTolerance = 1E-4,
            CollisionAppliedDisplacement = 1E-3,

            Planar = false,
        };

    }
}