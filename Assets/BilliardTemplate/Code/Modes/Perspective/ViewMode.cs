/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;
using UnityEngine.InputSystem;

namespace ibc.mode.perspective
{
    [CreateAssetMenu(fileName = "Perspective View Mode", menuName = "Billiard/Mode/Perspective/View Mode")]
    public class ViewMode : Mode
    {
        public override void Enter()
        {
            Manager.Control.Focus.Internal.started += FocusOnStarted;
            Manager.Control.Escape.Internal.started += EscapeOnStarted;
            Camera.Target = Manager.Billiard.SelectedBall.transform.position;
        }

        private void EscapeOnStarted(InputAction.CallbackContext obj)
        {
            ChangeMode<MenuMode>();
        }

        private void FocusOnStarted(InputAction.CallbackContext obj)
        {
            if (Billiard.State.Stationary)
                ChangeMode<AimMode>();
        }


        public override void Tick()
        {
            var control = Manager.Control;
            if (control.View.Active)
            {
                Camera.Pan(control.JawDelta.Value, control.PitchDelta.Value);
            }
            else
            {
                if (control.Fire.Active) Camera.Zoom(control.PitchDelta.Value);
                else Camera.Orbit(control.JawDelta.Value, control.PitchDelta.Value);
            }

            Camera.Tick();
        }

        public override void Exit()
        {
            Manager.Control.Focus.Internal.started -= FocusOnStarted;
            Manager.Control.Escape.Internal.started -= EscapeOnStarted;
        }

    }
}