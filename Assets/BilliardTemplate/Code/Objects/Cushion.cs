/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;

namespace ibc.objects
{

    [Serializable]
    public struct Cushion : IEquatable<Cushion>, IComparable<Cushion>, IIdentifiable
    {
        public int Identifier;
        public float Height;

        public double3 P0;
        public double3 P1;
        public double3 Dir;
        public double3 Normal;
        public double Distance;

        public double3 this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return P0;
                    case 1: return P1;
                }

                throw new ArgumentOutOfRangeException($"{i}");
            }
        }

        public Cushion(int id, double3 p0, double3 p1, float height)
        {
            Identifier = id;
            Height = height;
            P0 = p0;
            P1 = p1;
            Distance = math.length(p1 - p0);
            Dir = math.normalizesafe(p1 - p0, double3.zero);
            Normal = math.cross(Dir, new float3(0, 1, 0));

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