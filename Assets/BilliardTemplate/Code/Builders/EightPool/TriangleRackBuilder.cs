using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ibc.builders
{

    using unity;

    [ExecuteInEditMode]
    public class TriangleRackBuilder : BuilderBase
    {
        private static int[] _indexMap =
        {
            5, 13, 15, 6, 12,
            11, 7, 14, 4,
            10, 8, 3,
            9, 2,
            1,
        };

        [SerializeField] private int _seed;
        [SerializeField] private Mesh _mesh;
        [SerializeField] private double _ballRadius = 57.15E-3 / 2;
        [SerializeField] private double _ballMass = 0.156;
        [SerializeField] private double _ballScaleFactor = 1;

        [SerializeField] private Material[] _materials;

        private Material GetBallMaterial(int index)
        {
            return (_materials != null && index < _materials.Length) ? _materials[index] : null;
        }


        private quaternion RandomRotation()
        {
            return quaternion.Euler(UnityEngine.Random.insideUnitSphere * 360);
        }

        [ContextMenu("Build")]
        private void Build()
        {
            List<UnityBall> balls = new List<UnityBall>();
            int index = 0;
            for (int k = 5; k >= 1; --k)
            {
                for (int i = 0; i < k; ++i, ++index)
                {
                    double posX = i * 2 * _ballRadius + _ballRadius * (5 - k);
                    double posZ = Mathf.Sqrt(3) * _ballRadius * (5 - k);
                    double3 pos = new double3(posZ, 0, posX);
                    pos.z -= _ballRadius * 5 - _ballRadius;
                    balls.Add(CreateBall(_indexMap[index], _ballRadius, _ballMass, _mesh, _ballScaleFactor, GetBallMaterial(_indexMap[index]), pos, RandomRotation()));
                }
            }

            balls.Add(CreateBall(0, _ballRadius, _ballMass, _mesh, _ballScaleFactor, GetBallMaterial(0), new double3(25 * _ballRadius * 2,0,0), RandomRotation()));

            var obj = new GameObject("Balls");
            foreach (var ball in balls)
            {
                ball.transform.SetParent(obj.transform);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}