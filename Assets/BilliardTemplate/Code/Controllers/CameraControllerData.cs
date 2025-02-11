/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;

namespace ibc.controller
{


    /// <summary>Class that better defines camera controller constraints.</summary>
    [CreateAssetMenu(fileName = "Camera Data", menuName = "Billiard/Controllers/Camera Data")]
    public class CameraControllerData : ScriptableObject
    {
        //Minimum camera distance from the target
        [SerializeField] public float MinDistance = 0.01f;
        //Maximum camera distance from the target
        [SerializeField] public float MaxDistance = 2;
        //Minimum pitch
        [SerializeField] public float MinPitch = 0f;
        //Maximum pitch
        [SerializeField] public float MaxPitch = 89;
        //Move speed when the distance is at minimum
        [SerializeField] public float MinMoveSpeed = 0.001f;
        //Move speed when the distance is at maximum
        [SerializeField] public float MaxMoveSpeed = 0.02f;
        //Rotate speed of the camera
        [SerializeField] public float RotateSpeed = 1f;
        //Zoom speed of the camera
        [SerializeField] public float ZoomSpeed = 0.01f;
    }
}