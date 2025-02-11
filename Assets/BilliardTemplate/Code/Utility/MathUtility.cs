using Unity.Mathematics;

namespace ibc.utility
{
    using unity;
    using objects;

    public static class MathUtility
    {

        public static bool IsPointInPolygon(this Polygon polygon, float3 point)
        {
            return IsPointInPolygon(polygon.Points, point);
        }

        public static bool IsPointInPolygon(float3[] points, float3 point)
        {
            bool result = false;
            int j = points.Length - 1;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].z < point.z && points[j].z >= point.z || points[j].z < point.z && points[i].z >= point.z)
                {
                    if (points[i].x + (point.z - points[i].z) / (points[j].z - points[i].z) * (points[j].x - points[i].x) < point.x)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }
    }
}