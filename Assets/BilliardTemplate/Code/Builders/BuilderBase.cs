/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace ibc.builders
{
    using unity;

    /// <summary>Base class that supports building a billiard scene.</summary>
    public class BuilderBase : MonoBehaviour
    {

        /// <summary>Creates the table cushion in the unity scene and stores reference to it.</summary>
        /// <param name="identifier">The cushion identifier.</param>
        /// <param name="height">The cushion height, in both snooker and pool h = 7R/5</param>
        /// <param name="points">The points that represent cushion edge.</param>
        /// <returns>UnityCushion.</returns>
        public UnityCushion CreateCushion(int identifier, float height, params float3[] points)
        {
            Transform mainTr = new GameObject($"Cushion_{identifier}").transform;
            UnityCushion unityCushion = mainTr.gameObject.AddComponent<UnityCushion>();
            unityCushion.Identifier = identifier;
            unityCushion.Height = height;
            unityCushion.Points = points.ToList();
            return unityCushion;
        }


        /// <summary>Creates the table hole in the unity scene.</summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="pocketHeight">The hole height</param>
        /// <param name="position">The hole center position.</param>
        /// <param name="radius">The hole radius.</param>
        /// <returns>UnityHole.</returns>
        public UnityHole CreateHole(int identifier, float pocketHeight, double3 position, double radius)
        {
            Transform mainTr = new GameObject($"Hole_{identifier}").transform;
            mainTr.transform.position = (float3)position;
            UnityHole unityHole = mainTr.gameObject.AddComponent<UnityHole>();
            unityHole.Identifier = identifier;
            unityHole.Radius = radius;
            UnityHoleDrop unityHoleDrop = mainTr.gameObject.AddComponent<UnityHoleDrop>();
            unityHoleDrop.Points.Add(Vector3.zero);
            unityHoleDrop.Points.Add(new Vector3(0, -pocketHeight, 0));
            return unityHole;
        }


        /// <summary>Creates the unity polygon in the unity scene.</summary>
        /// <param name="identifier">The polygon identifier.</param>
        /// <param name="points">The list of points.</param>
        /// <returns>UnityPolygon.</returns>
        public UnityPolygon CreatePolygon(int identifier, bool drawMesh, params float3[] points)
        {
            GameObject obj = new GameObject($"Polygon_{identifier}");
            UnityPolygon unityArea = obj.AddComponent<UnityPolygon>();
            unityArea.Identifier = identifier;
            unityArea.DrawMesh = drawMesh;
            unityArea.Points = System.Array.ConvertAll<float3, Vector3>(points, t => t);
            return unityArea;
        }


        public UnityBall CreateBall(int id, double radius, double mass, Mesh mesh, double scaleFactor, Material mat, double3 pos, quaternion rot)
        {
            var ball = new GameObject($"Ball_{id}");
            ball.transform.SetPositionAndRotation((float3)pos, rot);
            ball.AddComponent<MeshFilter>().mesh = mesh;
            ball.AddComponent<MeshRenderer>().material = mat;
            ball.AddComponent<SphereCollider>();
            var unityBall = ball.AddComponent<UnityBall>();
            unityBall.Identifier = id;
            unityBall.Radius = radius;
            unityBall.Mass = mass;
            unityBall.ModelScaleFactor = scaleFactor;
            return unityBall;
        }


        /// <summary>Gets the convex hull from the specified points in 2-dim.</summary>
        /// <param name="points">The points.</param>
        /// <returns>The list of points that represent convex hull.</returns>
        public static List<float3> GetConvexHull(List<float3> points)
        {
            if (points == null)
                return null;

            if (points.Count <= 1)
                return points;

            int n = points.Count, k = 0;
            List<float3> h = new List<float3>(new float3[2 * n]);

            points.Sort((a, b) => a.x == b.x ? a.z.CompareTo(b.z) : a.x.CompareTo(b.x));

            for (int i = 0; i < n; ++i)
            {
                while (k >= 2 && Cross(h[k - 2], h[k - 1], points[i]) <= 0)
                    k--;
                h[k++] = points[i];
            }

            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && Cross(h[k - 2], h[k - 1], points[i]) <= 0)
                    k--;
                h[k++] = points[i];
            }

            return h.Take(k - 1).ToList();
        }

        private static double Cross(float3 o, float3 a, float3 b)
        {
            return (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
        }

    }
}