/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */
using System;
using Unity.Mathematics;
using UnityEngine;

namespace ibc.commands
{
    using controller;
    using objects;

    /// <summary>Implements strike command</summary>
    [Serializable]
    public struct StrikeCommand : IBilliardCommand
    {
        public int BallIdentifier;
        public float Velocity;
        public Cue Cue;
        public CueTransform Transform;
        public float MinimumPitch;


        public bool Execute(BilliardState state)
        {
            Ball ball = state.GetPhysicsBall(BallIdentifier);

            if (ball.State != Ball.StateType.Normal)
                throw new Exception("Could not strike a ball, ball is not in normal state");
            float pitch = math.max(Transform.Pitch, MinimumPitch);

            if (state.Solver.ResolveBallCueImpact(ref ball, Cue, Velocity, Transform.Jaw, pitch, (double2)Transform.Offset.xy)){
                ball.State = Ball.StateType.Struck;
                state.SetPhysicsBall(ball);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool HasChanged(StrikeCommand cmd, float eventThreshold)
        {
            if (BallIdentifier != cmd.BallIdentifier)
                return true;
            if (math.abs(Transform.Jaw - cmd.Transform.Jaw) > eventThreshold)
                return true;
            if (math.abs(Transform.Pitch - cmd.Transform.Pitch) > eventThreshold)
                return true;
            if (math.abs(Velocity - cmd.Velocity) > eventThreshold)
                return true;
            if (math.length(Transform.Offset - cmd.Transform.Offset) > eventThreshold)
                return true;
            if (math.length(MinimumPitch - cmd.MinimumPitch) > eventThreshold)
                return true;

            return false;
        }

        public void Log(BilliardState state)
        {
            Ball ball = state.GetPhysicsBall(BallIdentifier);

            Debug.Log($"<color=green>Ball {ball.Identifier} was struck </color>\n" +
                    $"\t<color=grey>Prior to impact cue stick velocity: {Velocity:F2}\n" +
                    $"\tPost impact cue ball velocity: {math.length(ball.Velocity):F2}\n" +
                    $"\tPost impact cue ball angular velocity: {math.length(ball.AngularVelocity):F2}\n</color>");
        }

        public void LogWarn(BilliardState state)
        {
            Ball ball = state.GetPhysicsBall(BallIdentifier);

            Debug.LogWarning($"<color=yellow>Invalid ball strike, miscue occurred</color>");
        }
    }
}