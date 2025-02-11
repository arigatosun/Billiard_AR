/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using UnityEngine.Events;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Linq;
using System.Collections.Generic;

namespace ibc
{
    using objects;
    using solvers;

    [Serializable]
    /// <summary>Class that manages billiard state simulation and interpolation.</summary>
    public class BilliardState : IDisposable
    {
        private const double MinimumInbetweenStepDelta = 1E-5;

        /// <summary>An event raised by the physics simulation system.</summary>
        public UnityAction<PhysicsSolver.Event> OnPhysicsEvent;
        /// <summary>An event indicating that stable physics state is changed. 
        /// Called after state becomes stable or before state becomes unstable.</summary>
        public UnityAction<bool> OnStableStateChange;
        /// <summary>Time that passed since next event was acquired.</summary>
        public double Timer => _timer;
        /// <summary> Physics balls used in the physics simulation.</summary>
        public IEnumerable<Ball> PhysicsBalls => _physicsJob.Scene.Balls;
        /// <summary>Temporary balls used to represent state between physics events. These balls do not affect simulation and should be
        /// used whenever ball state is required between physics events otherwise physics balls are proffered.</summary>
        public IEnumerable<Ball> TemporaryBalls => _balls;
        /// <summary>Whether billiard state is stationary(interpolation has finished or next event is none)</summary>
        public bool Stationary => Timer > _physicsJob.Output.GetLastEvent().Time || _physicsJob.Output.GetLastEvent().Type == PhysicsSolver.EventType.None;
        /// <summary>Physics solver used by the physics job.</summary>
        public PhysicsSolver Solver => _physicsJob.Solver;

        private Ball[] _balls;
        private PhysicsJob _physicsJob;
        private double _timer;
        private bool _wasStable;

        /// <summary>Initializes a new instance of the <see cref="T:ibc.BilliardStateBase" /> class by copying from another billiard state.</summary>
        /// <param name="src">The source.</param>
        public BilliardState(BilliardState src, PhysicsJobConstants jobConstants, PhysicsSolverConstants solverConstants) : 
            this(new BilliardManagedScene(src._physicsJob.Scene.Balls.ToArray(), src._physicsJob.Scene.Holes.ToArray(), src._physicsJob.Scene.Cushions.ToArray(), null), jobConstants, solverConstants)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="T:ibc.BilliardStateBase" /> class.</summary>
        /// <param name="scene">The billiard scene used to provide starting state and world information.</param>
        /// <param name="constants">The constants.</param>
        public BilliardState(BilliardManagedScene scene, PhysicsJobConstants jobConstants, PhysicsSolverConstants solverConstants)
        {
            var physicsSolver = new PhysicsSolver(solverConstants);
            var physicsScene = new PhysicsScene(scene, Allocator.Persistent);
            var physicsOutput = new PhysicsJobOutput(jobConstants, Allocator.Persistent);

            _balls = scene.Balls.ToArray();
            _physicsJob = new PhysicsJob(physicsSolver, physicsScene, physicsOutput, jobConstants);
            _timer = 0;
            _wasStable = true;
        }


        /// <summary>Steps the billiard state to the next physics event.</summary>
        public PhysicsSolver.Event Step()
        {
            _physicsJob.Schedule().Complete();
            return _physicsJob.Output.GetLastEvent();
        }

        public void Step(PhysicsSolver.Event ev)
        {
            Solver.Step(_physicsJob.Scene, ev);
        }

        /// <summary>Steps the billiard state forward by dt amount.</summary>
        /// <param name="dt">The dt.</param>
        public double Tick(float dt)
        {
            if (Stationary)
            {

                OnPhysicsEvent?.Invoke(_physicsJob.Output.GetLastEvent());

                Step();
                Solver.Step(_physicsJob.Scene, _balls, 0);

                for (int i = 0; i < _physicsJob.Output.GetEventCount() - 1; ++i)
                    OnPhysicsEvent?.Invoke(_physicsJob.Output.Events[i]);


                //check if next event(towards witch state is transitioned) is acquired
                //if so reset timer used for smoothly transitioning to the new state and send on stable state change event
                if (_physicsJob.Output.GetLastEvent().Type != PhysicsSolver.EventType.None)
                {
                    if (_wasStable)
                    {
                        OnStableStateChange?.Invoke(false);
                        _wasStable = false;
                    }

                    _timer = 0f;
                }
                else
                {
                    if (!_wasStable)
                    {
                        OnStableStateChange?.Invoke(true);
                        _wasStable = true;
                    }
                }
            }

            if (!Stationary)
            {
                return InbetweenStep(dt, _physicsJob.Output.GetLastEvent());
            }
            else
            {
                return dt;
            }
        }

        public double InbetweenStep(float dt, PhysicsSolver.Event ev, bool integrateRotation = true)
        {
            double maxDt = Math.Clamp(_timer + dt, 0, ev.Time + MinimumInbetweenStepDelta) - _timer;

            Solver.Step(_physicsJob.Scene, _balls, _timer);

            if (integrateRotation)
            {
                //integration of rotation
                for (var i = 0; i < _balls.Length; i++)
                {
                    Ball ball = _balls[i];
                    if (ball.State == Ball.StateType.Normal)
                        ball.Rotation = math.mul(quaternion.Euler((float3)ball.AngularVelocity * (float)maxDt), ball.Rotation);
                    _balls[i] = ball;
                }
            }

            _timer += maxDt;
            return maxDt;
        }

        /// <summary>Gets the temporary ball.</summary>
        /// <param name="identifier">The ball identifier.</param>
        public Ball GetTemporaryBall(int identifier)
        {
            for (int i = 0; i < _balls.Length; ++i)
            {
                if (_balls[i].Identifier == identifier)
                    return _balls[i];
            }

            throw new ArgumentOutOfRangeException();
        }

        /// <summary>Gets the temporary ball.</summary>
        /// <param name="identifier">The ball index.</param>
        public Ball GetTemporaryBallByIndex(int index)
        { 
            return _balls[index];
        }

        /// <summary>Gets the physics ball.</summary>
        /// <param name="identifier">The ball identifier.</param>
        public Ball GetPhysicsBall(int identifier)
        {
            for (int i = 0; i < _physicsJob.Scene.Balls.Length; ++i)
            {
                if (_physicsJob.Scene.Balls[i].Identifier == identifier)
                    return _physicsJob.Scene.Balls[i];
            }

            throw new ArgumentOutOfRangeException();
        }

        /// <summary>Gets the physics ball.</summary>
        /// <param name="identifier">The ball identifier.</param>
        public Ball GetPhysicsBallByIndex(int index)
        {
            return _physicsJob.Scene.Balls[index];
        }

        /// <summary>Sets the physics ball.</summary>
        /// <param name="identifier">The ball identifier.</param>
        public void SetPhysicsBall(Ball ball)
        {
            for(int i=0; i< _physicsJob.Scene.Balls.Length; ++i)
            {
                if (_physicsJob.Scene.Balls[i].Identifier == ball.Identifier)
                {
                    _physicsJob.Scene.Balls[i] = ball;
                    return;
                }
            }

            throw new ArgumentOutOfRangeException();
        }

        /// <summary>
        /// Resets balls to match the provided ones, event output is also reset. Timer is set to zero.
        /// This is done in order to invalidate all events and force the calculation of the new ones since the ball state
        /// has changed.
        /// </summary>
        public void Reset(Ball[] balls)
        {
            _physicsJob.Reset(balls);
            _balls = balls.ToArray();
            _physicsJob.Output.ResetEventCount();
            _timer = 0;
        }

        /// <summary>
        /// Returns the number of balls.
        /// </summary>
        public int GetBallCount()
        {
            return _balls.Length;
        }

        /// <summary>Returns the <strong>copy</strong> of the physics balls.</summary>
        public Ball[] GetPhysicsBalls()
        {
            return _physicsJob.Scene.Balls.ToArray();
        }

        /// <summary>
        /// Disposes this instance releasing all used resources.
        /// </summary>
        public void Dispose()
        {
            _physicsJob.Dispose();
        }

        public PhysicsScene GetPhysicsScene()
        {
            return _physicsJob.Scene;
        }
    }
}