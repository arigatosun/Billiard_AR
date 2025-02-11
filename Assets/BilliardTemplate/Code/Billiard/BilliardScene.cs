/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;
using System.Collections.Generic;

namespace ibc
{
    using unity;
    using objects;


    /// <summary>Unity billiard scene data container.</summary>
    [Serializable]
    public class BilliardUnityScene
    {
        public UnityBall[] Balls;
        public UnityHole[] Holes;
        public UnityCushion[] Cushions;
        public UnityPolygon[] Polygons;
        public UnityCue[] Cues;
    }


    /// <summary>Managed billiard scene data container.</summary>
    [Serializable]
    public class BilliardManagedScene
    {
        public Ball[] Balls;
        public Hole[] Holes;
        public Cushion[] Cushions;
        public Polygon[] Polygons;

        public BilliardManagedScene(Ball[] balls, Hole[] holes, Cushion[] cushions, Polygon[] polygons)
        {
            Balls = balls;
            Holes = holes;
            Cushions = cushions;
            Polygons = polygons;
        }

        public BilliardManagedScene(BilliardUnityScene scene)
        {
            Balls = scene.Balls.ToManaged();
            Holes = scene.Holes.ToManaged();
            Cushions = scene.Cushions.ToManaged();
            Polygons = scene.Polygons.ToManaged();
        }
    }


    /// <summary>Utility class that provides conversion methods between various scene/object representation.</summary>
    public static class BilliardSceneConversion
    {
        public static T GetTarget<T>(this T[] array, int identifier) where T : IIdentifiable
        {
            return array[GetIndex<T>(array, identifier)];
        }

        public static T GetTarget<T>(this List<T> list, int identifier) where T : IIdentifiable
        {
            return list[GetIndex<T>(list, identifier)];
        }

        public static int GetIndex<T>(this T[] array, int identifier) where T : IIdentifiable
        {
            for (int i = 0; i < array.Length; i++)
            {
                T a = array[i];
                if (a.GetIdentifier() == identifier)
                    return i;
            }

            return -1;
        }

        public static int GetIndex<T>(this List<T> list, int identifier) where T : IIdentifiable
        {
            for (int i = 0; i < list.Count; i++)
            {
                T a = list[i];
                if (a.GetIdentifier() == identifier)
                    return i;
            }

            return -1;
        }

        public static Polygon[] ToManaged(this UnityPolygon[] polygons)
        {
            Polygon[] temp = new Polygon[polygons.Length];
            for (int i = 0; i < polygons.Length; i++)
            {
                UnityPolygon poly = polygons[i];
                temp[i] = new Polygon(poly.Identifier, System.Array.ConvertAll<UnityEngine.Vector3, float3>(poly.Points, t=>t));
            }
            return temp;
        }

        public static Ball[] ToManaged(this UnityBall[] balls)
        {
            Ball[] temp = new Ball[balls.Length];
            for (int i = 0; i < balls.Length; i++)
            {
                UnityBall ub = balls[i];
                temp[i] = new Ball(ub.Identifier, (float3)ub.transform.position, balls[i].transform.rotation, ub.Mass, ub.Radius);
            }
            return temp;
        }

        public static Hole[] ToManaged(this UnityHole[] holes)
        {
            var temp = new Hole[holes.Length];
            for (int i = 0; i < holes.Length; ++i)
            {
                UnityHole uh = holes[i];
                temp[i] = new Hole(uh.Identifier, uh.Radius, (float3)uh.transform.position);
            }
            return temp;
        }

        public static Cushion[] ToManaged(this UnityCushion[] cushions)
        {
            int numberOfLines = 0;
            for (int i = 0; i < cushions.Length; ++i)
            {
                for (int j = 0; j < cushions[i].Points.Count - 1; ++j)
                    numberOfLines++;
            }
            Cushion[] temp = new Cushion[numberOfLines];
            int index = 0;
            for (int i = 0; i < cushions.Length; ++i)
            {
                UnityCushion uc = cushions[i];
                for (int j = 0; j < uc.Points.Count - 1; ++j, ++index)
                {
                    temp[index] = new Cushion(uc.Identifier, uc.Points[j], uc.Points[j + 1], uc.Height);
                }
            }

            return temp;
        }
    }
}