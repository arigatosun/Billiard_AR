/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace ibc.mode
{
    using controller;

    /// <summary>
    /// Finite state machine manager for different Modes that user can be in.
    /// </summary>
    public class ModeManager : MonoBehaviour
    {

        /// <summary> Reference to the main camera controller. </summary>
        public CameraController CamControlller => _cameraController;

        /// <summary> Reference to the main cue controller. </summary>
        public CueController CueController => _cueController;

        /// <summary> Reference to the billiard. </summary>
        public Billiard Billiard => _billiard;

        /// <summary> Current active mode</summary>
        public Mode CurrentMode => _currentMode;

        /// <summary> Current active mode index</summary>
        public int CurrentModeIndex => _currentModeIndex;

        /// <summary>Gets the input control.</summary>
        public InputControl Control => _control;

        [SerializeField] private CameraController _cameraController;
        [SerializeField] private CueController _cueController;
        [SerializeField] private Billiard _billiard;
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private Mode[] _modes;

        private int _currentModeIndex;
        private Mode _currentMode;
        private InputControl _control;

        private void Awake()
        {
            _currentMode = null;
            _currentModeIndex = -1;
            _control = new InputControl(_playerInput.currentActionMap);
        }

        private void Start()
        {
            for (int i = 0; i < _modes.Length; i++)
                _modes[i].Initialize(this);

            ChangeMode(0);
        }

        private void OnGUI()
        {
            GUILayout.Label($"{((Time.deltaTime)*1000):F2} ms");
            GUILayout.Label($"{CurrentMode.GetType().ToString()}");
        }

        private void Update()
        {
            Control.Update();
            CurrentMode.Tick();
        }

        private void LateUpdate()
        {
            Control.Update();
            CurrentMode.LateTick();
        }

        /// <summary>
        /// Change current active mode using the mode index.
        /// </summary>

        public void ChangeMode(int index)
        {
            CurrentMode?.Exit();
            _currentModeIndex = index;
            _currentMode = _modes[index];
            CurrentMode.Enter();

            Cursor.lockState = CurrentMode.CursorLockMode;
            Cursor.visible = CurrentMode.CursorVisible;
        }

        /// <summary>
        /// Change current active mode using the mode type.
        /// </summary>
        public void ChangeMode(Type modeType)
        {
            for (int i = 0; i < _modes.Length; ++i)
            {
                if (_modes[i].GetType() != modeType)
                    continue;

                ChangeMode(i);
            }
        }

        public InputControl GetInput()
        {
            return Control;
        }
    }
}