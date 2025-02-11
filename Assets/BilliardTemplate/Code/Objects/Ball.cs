/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;

namespace ibc.objects
{


    /// <summary>Billiard ball physics representation.</summary>
    [Serializable]
    public struct Ball : IComparable<Ball>, IEquatable<Ball>, IIdentifiable
    {
        /// <summary>Different possible ball states.</summary>
        public enum StateType
        {
            Normal,
            Struck,
            Pocketed,
            Invalid,
        }

        /// <summary>Governing type for the ball motion.</summary>
        public enum MotionType
        {
            Stationary = 1,
            Rolling = 2,
            Sliding = 4,
            StationarySpin = 8,
            Airborne = 16,
            Landing = 32,
        }


        /// <summary>The unique ball identifier.</summary>
        public int Identifier;
        /// <summary>Governing type for the ball motion.</summary>
        public MotionType Motion;
        /// <summary>Current ball state.</summary>
        public StateType State;
        /// <summary>Ball position.</summary>
        public double3 Position;
        /// <summary>Ball velocity.</summary>
        public double3 Velocity;
        /// <summary>Ball angular velocity.</summary>
        public double3 AngularVelocity;
        /// <summary>Mass.</summary>
        public double Mass;
        /// <summary>Radius.</summary>
        public double Radius;
        /// <summary>Inertia.</summary>
        public double Inertia;
        /// <summary>InverseInertia.</summary>
        public double InverseInertia;
        /// <summary>InverseMass.</summary>
        public double InverseMass;

        /// <summary>Cached orientation of the ball</summary>
        public quaternion Rotation;
        /// <summary>Cached acceleration coefficient in governing equation of motion.</summary>
        public double3 AccCoeff;
        /// <summary>Cached velocity coefficient in governing equation of motion.</summary>
        public double3 VelCoeff;

        public Ball(int id, double3 pos, quaternion rot, double mass, double radius)
        {
            Identifier = id;
            Position = pos;
            Velocity = AngularVelocity = double3.zero;
            Mass = mass;
            Radius = radius;
            Motion = MotionType.Stationary;
            Inertia = 2.0 / 5.0 * mass * Radius * Radius;
            InverseInertia = 1.0 / Inertia;
            InverseMass = 1 / mass;
            State = StateType.Normal;
            Rotation = rot;
            AccCoeff = VelCoeff = 0;
        }

        public int CompareTo(Ball x)
        {
            return Identifier.CompareTo(x.Identifier);
        }

        public bool Equals(Ball other)
        {
            return Identifier == other.Identifier;
        }

        public override int GetHashCode()
        {
            return Identifier.GetHashCode();
        }

        public int GetIdentifier()
        {
            return Identifier;
        }
    }
}