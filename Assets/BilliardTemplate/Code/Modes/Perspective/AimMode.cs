/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ibc.mode.perspective
{

    /// <summary>
    /// Mode in which user has a target ball and can orbit around it.
    /// </summary>
    [CreateAssetMenu(fileName = "Perspective Aim Mode", menuName = "Billiard/Mode/Perspective/Aim Mode")]
    [Serializable]
    public class AimMode : Mode
    {
        [SerializeField]
        private float _cueControllerPitchOffset = 5;

        public override void Enter()
        {
            Manager.Control.View.Internal.started += ViewOnStarted;
            Manager.Control.Focus.Internal.started += FocusOnStarted;
            Manager.Control.Escape.Internal.started += EscapeOnStarted;

        }

        private void EscapeOnStarted(InputAction.CallbackContext obj)
        {
            ChangeMode<MenuMode>();
        }

        private void ViewOnStarted(InputAction.CallbackContext obj)
        {
            ChangeMode<ViewMode>();
        }

        private void FocusOnStarted(InputAction.CallbackContext obj)
        {
            ChangeMode<ShotMode>();
        }

        public override void Tick()
        {
            var control = Manager.Control;
            var cueBall = Manager.Billiard.SelectedBall;
            if (cueBall != null) Camera.Target = cueBall.transform.position;

            if (Manager.Control.Fire.Active)
                Camera.Zoom(control.PitchDelta.Value);
            else
                Camera.Orbit(control.JawDelta.Value, control.PitchDelta.Value);

            Cue.SetJaw(Camera.Jaw);
            Cue.SetPitch(Camera.Pitch - _cueControllerPitchOffset);

            Camera.Tick();
        }

        public override void Exit()
        {
            Manager.Control.View.Internal.started -= ViewOnStarted;
            Manager.Control.Focus.Internal.started -= FocusOnStarted;
            Manager.Control.Escape.Internal.started -= EscapeOnStarted;

        }
    }
}