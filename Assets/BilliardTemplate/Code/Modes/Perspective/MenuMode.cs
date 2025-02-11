/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;
using UnityEngine.InputSystem;

namespace ibc.mode.perspective
{
    [CreateAssetMenu(fileName = "Menu Mode", menuName = "Billiard/Mode/Perspective/Menu Mode")]
    public class MenuMode : Mode
    {
        private GameObject _menuCamera;

        public override void Initialize(ModeManager manager)
        {
            base.Initialize(manager);

            _menuCamera = GameObject.FindGameObjectWithTag("MenuCamera");
            if (_menuCamera == null)
                Debug.LogError("Menu camera is not found");

            _menuCamera.gameObject.SetActive(false);

        }

        public override void Enter()
        {
            Manager.Control.Escape.Internal.started += EscapeOnStarted;
            Manager.Control.View.Internal.started += ViewOnStarted;
            Manager.Control.Focus.Internal.started += FocusOnStarted;

            Camera.GetCameraTransform().gameObject.SetActive(false);
            _menuCamera.gameObject.SetActive(true);
        }
        private void FocusOnStarted(InputAction.CallbackContext obj)
        {
            //last mode
            ChangeMode<AimMode>();
        }

        private void ViewOnStarted(InputAction.CallbackContext obj)
        {
            //last mode
            ChangeMode<ViewMode>();
        }

        private void EscapeOnStarted(InputAction.CallbackContext obj)
        {
            //last mode
            ChangeMode<ViewMode>();
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
            Manager.Control.Escape.Internal.started -= EscapeOnStarted;
            Manager.Control.Focus.Internal.started -= FocusOnStarted;
            Manager.Control.View.Internal.started -= ViewOnStarted;

            Camera.GetCameraTransform().gameObject.SetActive(true);
            _menuCamera.gameObject.SetActive(false);

        }

    }
}