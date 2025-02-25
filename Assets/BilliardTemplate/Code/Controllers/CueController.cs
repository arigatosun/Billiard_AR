/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace ibc.controller
{
    using commands;
    using unity;

    [Serializable]
    public struct CueTransform
    {
        public float3 Center, Offset;
        public float Jaw, Pitch;
    }

    /// <summary>Class that controls the cue stick.</summary>
    public class CueController : MonoBehaviour
    {
        /// <summary>An event called when cue transform changes.</summary>
        public UnityAction OnStrikeCommandChange;

        /// <summary>Gets the cue transform.</summary>
        public CueTransform CueTransform => _cueTransform;

        /// <summary>Gets a value indicating whether selected cue stick is visible in unity scene.</summary>
        public bool Visible => _billiard.SelectedCueStick.gameObject.activeSelf;

        //Reference to the billiard
        [SerializeField] private Billiard _billiard;
        [SerializeField] private CueControllerData _data;
        [SerializeField] private float _drawVelocityMultiplier = 10f;

        //distance the cue stick has been drawn
        private float _distance;
        //whether the cue stick can strike
        private bool _canStrike;
        //callback reference
        private Action _onStrike;
        //cue transform cached
        [SerializeField]
        private CueTransform _cueTransform;
        [SerializeField]
        private StrikeCommand _strikeCommandCached;
        private float _minimumPitch;
        private float _cachedVelocity;

        private void Start()
        {
            _billiard.State.OnPhysicsEvent += OnBilliardStateEvent;
        }

        private void LateUpdate()
        {
            if (_strikeCommandCached.HasChanged(GetStrikeCommand(), _data.CueStrikeChangeThreshold))
            {
                OnStrikeCommandChange?.Invoke();
                _strikeCommandCached = GetStrikeCommand();
            }

            CalculateCueTransform();
            CalculateStrike();           
        }

        private void CalculateStrike()
        {
            if (_cueTransform.Offset.z > _data.MinDrawDistanceBeforeStrike) //strike requirement 
            {
                _canStrike = true;
            }
            else
            if (_cueTransform.Offset.z < 0) //strike occurred
            {
                if (_canStrike)
                {
                    //perform strike
                    PerformStrike(GetImpactVelocity());
                    _canStrike = false;
                }
                else 
                {
                    //do not let cue stick go through the ball
                    _distance = 0f;
                }
            }
        }

        [ContextMenu("Strike")]
        /// <summary>
        /// Performs the strike.
        /// </summary>
        public void PerformStrike(float vel)
        {
            var strikeCommand = GetStrikeCommand();
            strikeCommand.Velocity = vel;
            if (_billiard.ExecuteCommand(strikeCommand))
            {
                strikeCommand.Log(_billiard.State);
                _onStrike?.Invoke();
            }
            else
            {
                strikeCommand.LogWarn(_billiard.State);
            }
        }

        /// <summary>
        /// Calculates and updates the cue stick transform.
        /// </summary>
        /// <param name="pitchIncrement">The pitch increment.</param>
        private void CalculateCueTransform(float pitchIncrement = 1f)
        {
            float pitch = _cueTransform.Pitch;
            Quaternion rotation;
            UnityBall ball = _billiard.SelectedBall;
            UnityCue cue = _billiard.SelectedCueStick;
            float radius = (float)ball.Radius;
            float z = 0f;
            float x = _cueTransform.Offset.x * radius;
            float y = _cueTransform.Offset.y * radius;
            float r2 = radius * radius;
            float x2 = x * x;
            float y2 = y * y;
            if (r2 > x2 + y2) z = Mathf.Sqrt(r2 - x2 - y2);
            _cueTransform.Offset.z = z + _distance;
            Vector3 dir = new Vector3(_cueTransform.Offset.x * radius, _cueTransform.Offset.y * radius, -z);
            do
            {
                rotation = Quaternion.Euler(pitch, _cueTransform.Jaw, 0);
                pitch += pitchIncrement;
            } 
            while (Physics.Raycast(ball.transform.position + rotation * dir, rotation * Vector3.back, out _, (float)cue.Length, _data.CollisionLayers) && pitch < 90);
            _cueTransform.Offset.z = z + _distance;
            dir = new Vector3(_cueTransform.Offset.x * radius, _cueTransform.Offset.y * radius, -_cueTransform.Offset.z);
            cue.transform.position = ball.transform.position + rotation * dir;
            cue.transform.rotation = rotation;
            _minimumPitch = pitch;
        }

        private void OnBilliardStateEvent(solvers.PhysicsSolver.Event e)
        {
            if (e.Type == solvers.PhysicsSolver.EventType.None)
                Show();
            else
                Hide();
        }

        private void OnDestroy()
        {
            _billiard.State.OnPhysicsEvent -= OnBilliardStateEvent;
        }


        /// <summary>
        /// Shows the cue stick visual.
        /// </summary>
        public void Show()
        {
            _billiard.SelectedCueStick.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the cue stick visual.
        /// </summary>
        public void Hide()
        {
            _billiard.SelectedCueStick.gameObject.SetActive(false);
        }

        /// <summary>
        /// Sets the cue stick distance.
        /// </summary>
        /// <param name="distance">The distance.</param>
        /// <returns>Updated cue stick distance</returns>
        public float SetDistance(float distance)
        {
            _distance = Mathf.Clamp(distance, 2 * (float)-_billiard.SelectedBall.Radius, _data.MaxDistance);
            return distance;
        }

        /// <summary>
        /// Starts the cue stick draw.
        /// </summary>
        /// <param name="onStrike">On strike callback.</param>
        public void StartDraw(Action onStrike)
        {
            _onStrike = onStrike;
        }

        /// <summary>
        /// Ends the cue stick draw.
        /// </summary>
        public void EndDraw()
        {
            _distance = 0f;
            _canStrike = false;
            _cueTransform.Offset = float3.zero;
        }

        /// <summary>
        /// Draws the cue stick by the specified amount.
        /// </summary>
        /// <param name="dy">Draw amount delta.</param>
        /// <param name="dt">Time delta.</param>
        public void Draw(float dy, float dt)
        {
            _cachedVelocity = dy * _data.DrawSpeed / dt;
            SetDistance(_distance - dy * _data.DrawSpeed);
        }

        /// <summary>
        /// Updates the offset from the center of the ball.
        /// </summary>
        /// <param name="dx">The dx.</param>
        /// <param name="dy">The dy.</param>
        public void UpdateOffset(float dx, float dy)
        {
            _cueTransform.Offset.x += dx * _data.OffsetSpeed;
            _cueTransform.Offset.y += dy * _data.OffsetSpeed;
            if (math.length(_cueTransform.Offset.xy) > 1)
                _cueTransform.Offset.xy = math.normalize(_cueTransform.Offset.xy);
        }

        /// <summary>
        /// Gets the direction of the cue stick.
        /// </summary>
        public float3 GetDirection()
        {
            return _billiard.SelectedCueStick.transform.rotation * Vector3.forward;
        }

        /// <summary>
        /// Gets the planar cue stick direction.
        /// </summary>
        public float3 GetPlanarDirection()
        {
            return Quaternion.Euler(0, _cueTransform.Jaw, 0) * Vector3.forward;
        }

        public void SetJaw(float jaw)
        {
            _cueTransform.Jaw = jaw;
        }

        public void SetPitch(float pitch)
        {
            _cueTransform.Pitch = pitch;
        }

        public float GetDrawVelocity()
        {
            return math.unlerp(0, _data.MaxDistance, _distance) * _drawVelocityMultiplier;
        }

        public float GetImpactVelocity()
        {
            return _cachedVelocity;
        }

        public StrikeCommand GetStrikeCommand()
        {
            StrikeCommand command = new StrikeCommand()
            {
                BallIdentifier = _billiard.SelectedBall.Identifier,
                Cue = _billiard.SelectedCueStick.Data,
                Transform = _cueTransform,
                Velocity = _cachedVelocity,
                MinimumPitch = _minimumPitch,
            };

            return command;
        }
    }
}