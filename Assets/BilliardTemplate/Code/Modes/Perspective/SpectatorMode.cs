/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;

namespace ibc.mode.perspective
{
    [CreateAssetMenu(fileName = "Perspective Spectator Mode", menuName = "Billiard/Mode/Perspective/Spectator Mode")]
    public class SpectatorMode : Mode
    {

        public override void Enter()
        {
            Billiard.State.OnStableStateChange += OnStableStateChange;
            Manager.Control.View.Internal.started += ViewOnStarted;
            Manager.Control.Escape.Internal.started += EscapeOnStarted;

        }


        private void OnStableStateChange(bool stable)
        {
            if (stable) ChangeMode<AimMode>();
        }

        private void ViewOnStarted(InputAction.CallbackContext obj)
        {
            ChangeMode<ViewMode>();
        }

        private void EscapeOnStarted(InputAction.CallbackContext obj)
        {
            ChangeMode<MenuMode>();
        }

        public override void LateTick()
        {
            //TODO: follow all balls that are moving by including them in the view
            var target = Billiard.SelectedBall.transform;
            var dir = target.position - Camera.GetCameraTransform().position;
            var rotation = Quaternion.LookRotation(dir);
            Camera.GetCameraTransform().rotation = rotation;
        }

        public override void Exit()
        {
            Manager.Control.View.Internal.started -= ViewOnStarted;
            Billiard.State.OnStableStateChange -= OnStableStateChange;
            Manager.Control.Escape.Internal.started -= EscapeOnStarted;

        }

    }
}