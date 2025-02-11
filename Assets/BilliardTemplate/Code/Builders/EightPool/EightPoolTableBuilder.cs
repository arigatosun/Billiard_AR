using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ibc.builders.eightpool
{
    using unity;

    public class EightPoolTableBuilder : BuilderBase
    {
        public float BallRadius = 0.028575f;
        public float MidPocketAngle = 80f;
        public float MidPocketMouthSize = 11.7E-2f;
        public float CornerPocketMouthSize = 11.7E-2f;
        public float CornerPocketAngle = 45f;
        public float CushionDepth = 5.5E-2f;
        public float TableWidth = 2.24f;
        public float TableHeight = 1.12f;
        public float WhiteBallOffset = 1.25f;
        public float HoleRadius = 0.05f;
        public float HoleHeight = 0.2f;

        [ContextMenu("Build")]
        private void Build()
        {
            List<UnityCushion> cushions = new List<UnityCushion>();
            float Height = 2f / 5f * BallRadius; 

            //bottom left rail
            float angle1 = math.radians(CornerPocketAngle);
            float angle2 = math.radians(MidPocketAngle);

            float3[] botLeftRail;
            {
                float3 offset = new float3(0, 0, -TableHeight / 2.0f);
                float3 p1 = new float3(-TableWidth * 0.5f + math.sin(angle1) * CornerPocketMouthSize, 0, 0) + offset;
                float3 p0 = new float3(p1.x - CushionDepth / math.tan(angle1), 0, -CushionDepth) + offset;
                float3 p2 = new float3(-MidPocketMouthSize / 2.0f, 0, 0) + offset;
                float3 p3 = new float3(p2.x + CushionDepth / math.tan(angle2), 0, -CushionDepth) + offset;

                botLeftRail = new[] { p0, p1, p2, p3, };
            }
            cushions.Add(CreateCushion(0, Height, botLeftRail));

            //left rail
            float3[] leftRail;
            {
                float3 offset = new float3(-TableWidth / 2.0f, 0, 0);
                float3 p1 = new float3(0, 0, -TableHeight * 0.5f + math.cos(angle1) * CornerPocketMouthSize) + offset;
                float3 p0 = new float3(-CushionDepth, 0, p1.z - CushionDepth / math.tan(angle1)) + offset;
                float3 p2 = new float3(0, 0, TableHeight * 0.5f - math.cos(angle1) * CornerPocketMouthSize) + offset;
                float3 p3 = new float3(-CushionDepth, 0, p2.z + CushionDepth / math.tan(angle1)) + offset;
                leftRail = new float3[] { p0, p1, p2, p3 };
            }
            cushions.Add(CreateCushion(1, Height, leftRail));
            float3[] points = new float3[4];

            //bottom right rail
            float3x3 reflectMatrixX = float3x3.Scale(new float3(-1, 1, 1));
            for (int i = 0; i < botLeftRail.Length; ++i) points[i] = math.mul(reflectMatrixX, botLeftRail[i]);
            cushions.Add(CreateCushion(2, Height, points));

            //top right rail
            float3x3 reflectMatrixZ = float3x3.Scale(new float3(1, 1, -1));
            for (int i = 0; i < botLeftRail.Length; ++i) points[i] = math.mul(reflectMatrixZ, points[i]);
            cushions.Add(CreateCushion(3, Height, points));

            //top left rail
            for (int i = 0; i < botLeftRail.Length; ++i) points[i] = math.mul(reflectMatrixZ, botLeftRail[i]);
            cushions.Add(CreateCushion(4, Height, points));

            //right rail
            for (int i = 0; i < leftRail.Length; ++i) points[i] = math.mul(reflectMatrixX, leftRail[i]);
            cushions.Add(CreateCushion(5, Height, points));

            var obj = new GameObject("Cushions");
            foreach (var cushion in cushions)
            {
                cushion.transform.SetParent(obj.transform);
            }

            List<UnityPolygon> areas = new List<UnityPolygon>();

            //create playing area that includes holes
            List<float3> playingAreaPoints = new List<float3>();
            foreach (var unityCushion in cushions)
                playingAreaPoints.AddRange(unityCushion.Points);
            playingAreaPoints = GetConvexHull(playingAreaPoints);
            areas.Add(CreatePolygon(0, false, playingAreaPoints.ToArray()));

            //create playing area
            playingAreaPoints = new List<float3>();
            foreach (var unityCushion in cushions)
                playingAreaPoints.AddRange(unityCushion.Points.GetRange(1, 2));
            playingAreaPoints = GetConvexHull(playingAreaPoints);
            areas.Add(CreatePolygon(1, true, playingAreaPoints.ToArray()));

            var area = new GameObject("Playing Area");
            foreach (var a in areas)
                a.transform.SetParent(area.transform);
            area.transform.position = new Vector3(0, -BallRadius, 0);
            BuildHoles();
        }

        private void BuildHoles()
        {
            List<UnityHole> holes = new List<UnityHole>();

            float3 p0 = new float3(-TableWidth * 0.5f, 0, -TableHeight * 0.5f);
            float3 p1 = new float3(-p0.x, 0, p0.z);
            float3 p2 = new float3(-p0.x, 0, -p0.z);
            float3 p3 = new float3(p0.x, 0, -p0.z);

            holes.Add(CreateHole(0, HoleHeight, p0, HoleRadius));
            holes.Add(CreateHole(1, HoleHeight, p1, HoleRadius));
            holes.Add(CreateHole(2, HoleHeight, p2, HoleRadius));
            holes.Add(CreateHole(3, HoleHeight, p3, HoleRadius));
            holes.Add(CreateHole(4, HoleHeight, (p0 + p1) * 0.5f, HoleRadius));
            holes.Add(CreateHole(5, HoleHeight, (p2 + p3) * 0.5f, HoleRadius));

            var obj = new GameObject("Holes");
            foreach (var hole in holes)
            {
                hole.transform.SetParent(obj.transform);
            }
        }
    }
}