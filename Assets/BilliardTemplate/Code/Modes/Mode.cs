/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;

namespace ibc.mode
{
    using controller;

    public abstract class Mode : ScriptableObject
    {

        public CursorLockMode CursorLockMode => _cursorLockMode;
        public bool CursorVisible => _cursorVisible;


        protected ModeManager Manager;
        protected CameraController Camera => Manager.CamControlller;
        protected CueController Cue => Manager.CueController;
        protected Billiard Billiard => Manager.Billiard;

        [SerializeField]
        protected CursorLockMode _cursorLockMode = CursorLockMode.Locked;
        [SerializeField]
        protected bool _cursorVisible = false;

        public virtual void Initialize(ModeManager manager)
        {
            Manager = manager;
        }

        public abstract void Enter();
        public virtual void Tick() { }

        public virtual void LateTick() { }

        public abstract void Exit();

        protected void ChangeMode<T>()
        {
            Manager.ChangeMode(typeof(T));
        }

    }
}