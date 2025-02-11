/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;

namespace ibc.controller
{
    using mode.perspective;
    using mode;

    /// <summary>Class that controls the crosshair(visual target indicator).</summary>
    public class CrosshairController : MonoBehaviour
    {
        //Visual game object
        [SerializeField] private GameObject _visual;

        //reference to the camera controller
        private CameraController _camController;

        //reference to the mode manager
        private ModeManager _modeManager;

        private void Start()
        {
            _camController = FindObjectOfType<CameraController>();
            _modeManager = FindObjectOfType<ModeManager>();
        }

        private void LateUpdate()
        {
            if (_modeManager.CurrentMode is ViewMode)
            {
                _visual.transform.localPosition = _camController.Target;
                if (!_visual.gameObject.activeSelf)
                    _visual.gameObject.SetActive(true);
            }
            else
            {
                if (_visual.gameObject.activeSelf)
                    _visual.gameObject.SetActive(false);
            }
        }
    }
}