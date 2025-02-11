/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;
using UnityEngine.InputSystem;

namespace ibc.mode.perspective
{

    [CreateAssetMenu(fileName = "Perspective Shot Mode", menuName = "Billiard/Mode/Perspective/Shot Mode")]
    public class ShotMode : Mode
    {
       

        public override void Enter()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Manager.Control.AltFire.Internal.started += AltFireStarted;
            Manager.Control.Escape.Internal.started += AltFireStarted;

            Cue.StartDraw(OnStrike);
        }

        private void AltFireStarted(InputAction.CallbackContext obj)
        {
            ChangeMode<AimMode>();
        }

        public override void Tick()
        {
            var control = Manager.Control;

            if (control.Fire.Active)
            {
                Cue.UpdateOffset(control.JawDelta.Value, control.PitchDelta.Value);
            }
            else
            {
                Cue.Draw(control.PitchDelta.Value, Time.deltaTime);
            }

            if (control.Space.Active)
            {
                Cue.PerformStrike(Cue.GetDrawVelocity());
            }
        }

        public void OnStrike()
        {
            ChangeMode<SpectatorMode>();
        }

        public override void Exit()
        {
            Cue.EndDraw();
            Manager.Control.AltFire.Internal.started -= AltFireStarted;
            Manager.Control.Escape.Internal.started -= AltFireStarted;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}