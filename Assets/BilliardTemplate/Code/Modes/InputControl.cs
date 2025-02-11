/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine.InputSystem;

namespace ibc.mode
{
    public class Trigger
    {
        public bool Active;
        public InputAction Internal;

        public Trigger(InputAction action)
        {
            action.performed += context => Active = true;
            action.canceled += context => Active = false;
            Internal = action;
        }
    }

    public class Delta
    {
        public float Value;
        private InputAction _internal;

        public Delta(InputAction action)
        {
            _internal = action;
        }

        public void Update()
        {
            Value = _internal.ReadValue<float>();
        }
    }

    public class InputControl
    {
        public Trigger Fire { get; private set; }
        public Trigger AltFire { get; private set; }
        public Trigger View { get; private set; }
        public Trigger Space { get; private set; }

        public Trigger Escape { get; private set; }
        public Trigger Focus { get; private set; }
        public Delta JawDelta { get; private set; }
        public Delta PitchDelta { get; private set; }


        public InputControl(InputActionMap map)
        {
            Fire = new Trigger(map.FindAction("Fire"));
            AltFire = new Trigger(map.FindAction("AltFire"));
            View = new Trigger(map.FindAction("View"));
            Focus = new Trigger(map.FindAction("Focus"));
            Escape = new Trigger(map.FindAction("Escape"));
            Space = new Trigger(map.FindAction("Space"));

            JawDelta = new Delta(map.FindAction("JawDelta"));
            PitchDelta = new Delta(map.FindAction("PitchDelta"));
        }

        public void Update()
        {
            JawDelta.Update();
            PitchDelta.Update();
        }
    }
}