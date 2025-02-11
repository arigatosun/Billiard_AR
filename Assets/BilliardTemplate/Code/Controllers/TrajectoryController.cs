/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */
using Unity.Mathematics;
using UnityEngine;

namespace ibc.controller
{
    /// <summary>Class that handles ball trajectory display by managing unity line renderer.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryController : MonoBehaviour
    {
        [SerializeField]
        private float _minDistance = 0.01f;
        private LineRenderer _lineRenderer;

        public Color[] ColorMap;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        public void Reset()
        {
            _lineRenderer.positionCount = 0;
        }

        public void Hide()
        {
            _lineRenderer.enabled = false;
        }

        public void Show()
        {
            _lineRenderer.enabled = true;
        }

        public void AddPoint(double3 position)
        {
            float3 p = (float3)position;

            if (_lineRenderer.positionCount > 0)
            {
                if (math.length(((float3)_lineRenderer.GetPosition(_lineRenderer.positionCount - 1)).xy - p.xz) < _minDistance)
                    return;
            }

            _lineRenderer.positionCount++;
            _lineRenderer.SetPosition(_lineRenderer.positionCount - 1, new float3(p.x, p.z, -p.y));
        }

        public void SetIdentifier(int identifier)
        {
            gameObject.name = $"Trajectory[{identifier}]";

            if (ColorMap.Length > identifier && identifier >= 0)
            {
                _lineRenderer.startColor = ColorMap[identifier];
                _lineRenderer.endColor = ColorMap[identifier];
            }
        }
    }
}