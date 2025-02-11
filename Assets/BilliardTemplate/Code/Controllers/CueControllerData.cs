using UnityEngine;

namespace ibc.controller
{
    [CreateAssetMenu(fileName = "Cue Data", menuName = "Billiard/Controllers/Cue Data")]
    public class CueControllerData : ScriptableObject
    {
        //Unity layers that can interact with cue stick
        [SerializeField] public LayerMask CollisionLayers;
        //Maximum distance that cue stick can be drawn
        [SerializeField] public float MaxDistance = 0.5f;
        //Cue stick draw speed
        [SerializeField] public float DrawSpeed = 0.01f;
        //Minimum distance the cue stick has to be drawn before it can strike the ball
        [SerializeField] public float MinDrawDistanceBeforeStrike = 0.05f;
        //Cue stick position offset speed
        [SerializeField] public float OffsetSpeed = 0.01f;
        //Threshold set for event invoke
        [SerializeField] public float CueStrikeChangeThreshold = 0.01f;
    }
}