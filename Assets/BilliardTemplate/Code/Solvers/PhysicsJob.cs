/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */
//#define DEBUG_EVENTS

using ibc.objects;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

#if DEBUG_EVENTS
using UnityEngine;
#endif

namespace ibc.solvers
{
    [Serializable]
    public struct PhysicsJobConstants
    {
        public int MaximumEventsPerStep;
        public double MinimumEventTime;
    }

    [Serializable]
    public struct PhysicsJobOutput : IDisposable
    {
        public NativeArray<PhysicsSolver.Event> Events;
        public NativeArray<int> EventsCount;


        public PhysicsJobOutput(PhysicsJobConstants constants, Allocator allocator)
        {
            Events = new NativeArray<PhysicsSolver.Event>(constants.MaximumEventsPerStep, allocator);
            EventsCount = new NativeArray<int>(1, allocator);
        }

        public void ResetEventCount()
        {
            EventsCount[0] = 0;
        }

        public void AddEvent(PhysicsSolver.Event e)
        {
            Events[EventsCount[0]] = e;
            EventsCount[0]++;
        }

        public void SetEventCount(int count)
        {
            EventsCount[0] = count;
        }

        public int GetEventCount()
        {
            return EventsCount[0];
        }

        public bool HasEvent()
        {
            return EventsCount != null && EventsCount[0] > 0;
        }

        public PhysicsSolver.Event GetLastEvent()
        {
            if (!HasEvent())
                return new PhysicsSolver.Event() { Type = PhysicsSolver.EventType.None };

            return Events[GetEventCount() - 1];
        }

        public double GetAccumulatedEventTime()
        {
            double t = 0;
            for(int i=0; i< GetEventCount(); ++i)
            {
                t += Events[i].Time;
            }
            return t;
        }

        public void Dispose()
        {
            Events.Dispose();
            EventsCount.Dispose();
        }
    }

    [Serializable]
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.High, OptimizeFor = OptimizeFor.Performance)]
    public struct PhysicsJob : IJob, IDisposable
    {
        public PhysicsJobConstants Constants;
        public PhysicsSolver Solver;
        public PhysicsScene Scene;
        public PhysicsJobOutput Output;

        public PhysicsJob(PhysicsSolver solver, PhysicsScene scene, PhysicsJobOutput output, PhysicsJobConstants constants)
        {
            Solver = solver;
            Scene = scene;
            Output = output;
            Constants = constants;
        }

        public void Execute()
        {
            double accTime = 0;
            int iterations = 0;

#if DEBUG_EVENTS
            Debug.Log("->");
#endif

            Solver.Step(Scene, Output.GetLastEvent());
            Output.ResetEventCount();

            do
            {
                //step last event
                Solver.Step(Scene, Output.GetLastEvent());
                Output.AddEvent(Solver.GetNextEvent(Scene));

                //TODO: integrate rotation

#if DEBUG_EVENTS
                var lastEvent = Output.GetLastEvent();
                var eventCount = Output.GetEventCount();
                var balls = Scene.Balls;
                switch (lastEvent.Type)
                {
                    case PhysicsSolver.EventType.None:
                        Debug.Log($"[{eventCount}] No event reported");
                        break;
                    case PhysicsSolver.EventType.StateTransition:
                        Debug.Log($"[{eventCount}] Transition event: Ball id: {balls[lastEvent.BallIndex].Identifier}" +
                                  $" {balls[lastEvent.BallIndex].Motion} -> {lastEvent.Motion} in {(float)lastEvent.Time} Velocity: {(float3)balls[lastEvent.BallIndex].Velocity}");
                        break;
                    case PhysicsSolver.EventType.PocketCollision:
                        Debug.Log($"[{eventCount}] Pocket collision event: Ball id:{balls[lastEvent.BallIndex].Identifier} in {(float)lastEvent.Time}");
                        break;
                    case PhysicsSolver.EventType.BallCollision:
                        Debug.Log($"[{eventCount}] Ball collision event: Ball id:{balls[lastEvent.BallIndex].Identifier} Other id: {balls[lastEvent.OtherIndex].Identifier}" +
                                  $" in {(float)lastEvent.Time}");
                        break;
                    case PhysicsSolver.EventType.CushionCollision:
                        Debug.Log($"[{eventCount}] Cushion collision event: Ball id:{balls[lastEvent.BallIndex].Identifier}" +
                                  $" in {(float)lastEvent.Time}");
                        break;
                    case PhysicsSolver.EventType.CushionVertexCollision:
                        Debug.Log($"[{eventCount}] Cushion vertex collision event: Ball id:{balls[lastEvent.BallIndex].Identifier}" +
                                  $" in {(float)lastEvent.Time}");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
#endif

                accTime += math.abs(Output.GetLastEvent().Time);
                ++iterations;
            } while ((accTime < Constants.MinimumEventTime) && iterations < Constants.MaximumEventsPerStep);
        }


        public void Dispose()
        {
            Scene.Dispose();
            Output.Dispose();
        }

        public void Reset(Ball[] balls)
        {
            Scene.Balls.CopyFrom(balls);
        }
    }
}