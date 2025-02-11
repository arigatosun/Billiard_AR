/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace ibc.ui
{
    using controller;

    public class OffsetController : MonoBehaviour
    {
        [SerializeField] private Text _offsetLabel;
        [SerializeField] private CueController _cueController;
        [SerializeField] private RectTransform _hitSpot;
        [SerializeField] private float _maxLocalRadiusPixels = 50f;

        void Update()
        {
            float2 offset = _cueController.CueTransform.Offset.xy;
            _hitSpot.anchoredPosition = offset * _maxLocalRadiusPixels;
            _offsetLabel.text = $"{offset.x:F2}  {offset.y:F2}";
        }
    }
}