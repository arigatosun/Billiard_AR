using UnityEngine;
using Unity.Mathematics;
using ibc.unity;

namespace ibc.controller
{
    [RequireComponent(typeof(LineRenderer))]
    public class CircleController : MonoBehaviour
    {
        [SerializeField] private int _segments = 60;
        [SerializeField] private float _radius = 0.2f;
        [SerializeField] private Color _circleColor = Color.white;
        [SerializeField] private float _lineWidth = 0.01f;

        private LineRenderer _lineRenderer;
        private UnityBall _targetBall;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
            _lineRenderer.startColor = _circleColor;
            _lineRenderer.endColor = _circleColor;
            _lineRenderer.positionCount = _segments + 1;

            // 円を描画するレイヤーを TrajectoryLine に設定
            gameObject.layer = LayerMask.NameToLayer("TrajectoryLine");
        }

        public void Initialize(UnityBall targetBall, float radius)
        {
            _targetBall = targetBall;
            _radius = radius;
            DrawCircle();
        }

        private void LateUpdate()
        {
            if (_targetBall != null)
            {
                DrawCircle();
            }
        }

        private void DrawCircle()
        {
            float deltaTheta = (2f * Mathf.PI) / _segments;
            float theta = 0f;

            Vector3 ballPosition = _targetBall.transform.position;

            for (int i = 0; i <= _segments; i++)
            {
                float x = _radius * Mathf.Cos(theta);
                float z = _radius * Mathf.Sin(theta);
                Vector3 pos = new Vector3(x, 0, z) + ballPosition;
                _lineRenderer.SetPosition(i, pos);
                theta += deltaTheta;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}