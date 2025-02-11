/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using ibc.solvers;
using System;

namespace ibc.objects
{

    [Serializable]
    public struct Cue : IEquatable<Cue>, IComparable<Cue>, IIdentifiable
    {
        public int Identifier;
        public double Mass;
        public double Length;
        public double Radius;
        public PhysicsImpactParameters ImpactParameters;

        public double Inertia;
        public double InverseInertia;
        public double InverseMass;

        public Cue(int id, double mass, double length, double radius, PhysicsImpactParameters parameters)
        {
            Identifier = id;
            Mass = mass;
            Length = length;
            Radius = radius;
            ImpactParameters = parameters;
            Inertia = (1 / 12.0 * mass * length * length) + (Radius * Radius * mass / 4.0);
            InverseInertia = 1 / Inertia;
            InverseMass = 1 / mass;
        }


        public int CompareTo(Cue x)
        {
            return Identifier.CompareTo(x.Identifier);
        }

        public bool Equals(Cue other)
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