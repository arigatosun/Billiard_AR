/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;
using UnityEngine;

namespace ibc.controller
{

    /// <summary>Class that controls camera.</summary>
    public class CameraController : MonoBehaviour
    {

        public Camera Internal => _camera;

        /// <summary>Gets or sets the pitch.</summary>
        /// <value>The pitch or camera orientation around x axis.</value>
        public float Pitch { get => _pitch; set => _pitch = value; }

        /// <summary>Gets or sets the jaw.</summary>
        /// <value>The jaw or camera orientation around Y axis.</value>
        public float Jaw { get => _jaw; set => _jaw = value; }

        /// <summary>Gets or sets the distance.</summary>
        /// <value>The distance from the target to the camera.</value>
        public float Distance { get => _distance; set => _distance = value; }

        /// <summary>Gets or sets the target position.</summary>
        /// <value>The target position.</value>
        public Vector3 Target { get => _target; set => _target = value; }

        [SerializeField] private CameraControllerData _data;
        [SerializeField] private Vector3 _target;
        [SerializeField] private float _distance = 1;
        [SerializeField] private float _jaw;
        [SerializeField] private float _pitch;

        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
            if(_camera == null)
            {
                Debug.LogError("Could not find main camera");
                return;
            }
        }

        /// <summary>Sets the camera data.</summary>
        public void SetCameraData(CameraControllerData data)
        {
            _data = data;
        }

        /// <summary>Moves the camera towards the target by specified amount.</summary>
        /// <param name="dy">Amount.</param>
        public void Zoom(float dy)
        {
            Distance -= dy * _data.ZoomSpeed;
            Distance = Mathf.Clamp(Distance, _data.MinDistance, _data.MaxDistance);
        }

        /// <summary>Updates the camera controller.</summary>
        public void Tick()
        {
            _camera.transform.position = Quaternion.Euler(Pitch, Jaw, 0) * new Vector3(0, 0, -Distance) + (Vector3)_target;
            _camera.transform.LookAt(_target);
        }


        /// <summary>Pans the camera.</summary>
        /// <param name="dx">Delta amount in x direction.</param>
        /// <param name="dy">Delta amount in y direction.</param>
        public void Pan(float dx, float dy)
        {
            _target = _target + Quaternion.AngleAxis(Jaw, Vector3.up) * new Vector3(dx, 0, dy) *
                math.lerp(_data.MinMoveSpeed, _data.MaxMoveSpeed, math.unlerp(_data.MinDistance, _data.MaxDistance, Distance));
        }


        /// <summary>Orbits the camera around the target.</summary>
        /// <param name="dx">The dx amount.</param>
        /// <param name="dy">The dy amount.</param>
        public void Orbit(float dx, float dy)
        {
            Jaw += dx * _data.RotateSpeed;
            Pitch += dy * _data.RotateSpeed;
            Pitch = Mathf.Clamp(Pitch, _data.MinPitch, _data.MaxPitch);
        }

        /// <summary>Returns camera transform.</summary>
        public Transform GetCameraTransform()
        {
            return _camera.transform;
        }
    }
}