/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;

namespace ibc.objects
{

    [Serializable]
    public readonly struct Polygon : IEquatable<Cushion>, IComparable<Cushion>, IIdentifiable
    {
        public readonly int Identifier;
        public readonly float3[] Points;

        public Polygon(int id, float3[] points)
        {
            Identifier = id;
            Points = points;
        }

        public int CompareTo(Cushion x)
        {
            return Identifier.CompareTo(x.Identifier);
        }

        public bool Equals(Cushion other)
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