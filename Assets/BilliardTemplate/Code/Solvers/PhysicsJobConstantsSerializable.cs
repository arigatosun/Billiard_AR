/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine;

namespace ibc.solvers
{
    [CreateAssetMenu(fileName = "Physics Job Constants", menuName = "Billiard/Constants/Physics Job Constants")]
    public class PhysicsJobConstantsSerializable : ScriptableObject
    {
        public PhysicsJobConstants Data = new PhysicsJobConstants()
        {
            MaximumEventsPerStep = 128,
            MinimumEventTime = 0.01f,
        };
    }
}