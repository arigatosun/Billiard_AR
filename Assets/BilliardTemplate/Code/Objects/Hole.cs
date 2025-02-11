/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;

namespace ibc.objects
{

    [Serializable]
    public struct Hole : IEquatable<Hole>, IComparable<Hole>, IIdentifiable
    {
        public int Identifier;
        public double Radius;
        public double3 Position;

        public Hole(int identifier, double radius, double3 position)
        {
            Identifier = identifier;
            Radius = radius;
            Position = position;
        }

        public int CompareTo(Hole x)
        {
            return Identifier.CompareTo(x.Identifier);
        }

        public bool Equals(Hole other)
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