using ibc.objects;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ibc
{

    /// <summary>
    /// Class UnityHoleDrop handles visual movement of the ball into a hole once the ball is pocketed.
    /// </summary>
    public class UnityHoleDrop : MonoBehaviour
    {
        [Serializable]
        private class Data : IIdentifiable
        {
            public int Identifier;
            public Transform Obj;
            public int CurrentIndex;
            public Action Callback;
            public float VelPlane, VelY;
            public float3 AngVelocity;
            public float Gravity;

            public int GetIdentifier()
            {
                return Identifier;
            }
        }

        /// <summary>
        /// List of the points that the ball will follow once pocketed.
        /// </summary>
        public List<Vector3> Points = new List<Vector3>();

        private List<Data> _objects = new List<Data>();


        /// <summary>Registers a ball to be moved(visually) into a hole.
        /// Ball transform should not be modified outside of this script before the finish callback has been called or remove was manually called. </summary>
        /// <param name="identifier">The ball identifier.</param>
        /// <param name="tr">The ball transform.</param>
        /// <param name="velocity">The ball velocity.</param>
        /// <param name="angVelocity">The ball angular velocity.</param>
        /// <param name="gravity">The gravity.</param>
        /// <param name="finishCallback">Callback when ball drops into a pocket.</param>
        public void Put(int identifier, Transform tr, float3 velocity, float3 angVelocity, float gravity, Action finishCallback)
        {
            if (_objects.Find(t => t.Obj == transform) != null || _objects.Find(t => t.Identifier == identifier) != null)
            {
                Debug.LogError("Ball is already pocketed");
                return;
            }

            _objects.Add(new Data()
            {
                Identifier = identifier,
                Obj = tr,
                CurrentIndex = 0,
                Callback = finishCallback,
                VelPlane = math.length(velocity.xz),
                VelY = math.abs(math.min(velocity.y, 0)),
                AngVelocity = angVelocity,
                Gravity = gravity
            }); ;
        }


        /// <summary>Removes the specified ball from the register. 
        /// This is manually called once ball falls into a pocket</summary>
        /// <param name="identifier">The ball identifier.</param>
        public bool Remove(int identifier)
        {
            var index = _objects.GetIndex(identifier);
            if (index == -1)
            {
                return false;
            }

            _objects.RemoveAt(index);
            return true;
        }

        private float3 GetPoint(int index)
        {
            return transform.position + transform.rotation * Points[index];
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            //move the pocketed balls into a holes
            for (int i = _objects.Count - 1; i >= 0; --i)
            {
                var data = _objects[i];

                //check if finished
                if (Points.Count == data.CurrentIndex)
                {
                    //finished
                    Remove(_objects[i].Identifier);
                    continue;
                }

                float3 pos = (float3)data.Obj.position;
                float3 nextPoint = GetPoint(data.CurrentIndex);

                //decompose movement 
                float posX = MoveTowards(pos.x, nextPoint.x, data.VelPlane * dt);
                float posZ = MoveTowards(pos.z, nextPoint.z, data.VelPlane * dt);
                float posY = MoveTowards(pos.y, nextPoint.y, data.VelY * dt);
                

                //update position and integrate rotation
                data.Obj.position = new Vector3(posX, posY, posZ);
                data.Obj.rotation = math.mul(quaternion.Euler(data.AngVelocity * dt), data.Obj.rotation);

                //apply gravity
                data.VelY += data.Gravity * dt;


                //switch movement to the next point
                if (math.lengthsq((float3)data.Obj.position - (float3)nextPoint) <= 1E-5)
                    data.CurrentIndex++;
            }
        }

        private float MoveTowards(float current, float target, float maxDistanceDelta)
        {
            float delta = target - current;
            float deltaSq = delta * delta;
            float distSq = maxDistanceDelta * maxDistanceDelta;
            if (deltaSq == 0f || deltaSq <= distSq)
                return target;

            float deltaAbs = (float)Math.Abs(delta);
            float normDelta = delta / deltaAbs;
            return current + normDelta * maxDistanceDelta;
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR

            if (UnityEditor.Selection.Contains(gameObject))
                Gizmos.color = Color.red;
            else Gizmos.color = Color.white;
#endif
            if (Points != null)
            {
                for (var i = 0; i < Points.Count; i++)
                {
                    Gizmos.DrawSphere(GetPoint(i), 0.01f);
                    if (i + 1 != Points.Count) Gizmos.DrawLine(GetPoint(i), GetPoint(i + 1));
                }
            }
        }

    }
}