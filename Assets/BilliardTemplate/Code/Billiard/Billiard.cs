/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */


using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Windows.Input;
using System;
using System.Linq;

namespace ibc
{
    using unity;
    using objects;
    using solvers;
    using commands;
    using utility;

    /// <summary>Class that ties billiard state with unity scene.</summary>
    public class Billiard : MonoBehaviour
    {
        /// <summary>
        /// Reset point data container.
        /// </summary>
        [Serializable]
        public class ResetPoint
        {
            public ICommand Command;
            public Ball[] Balls;
            public DateTime Time;
            public object UserData;
        }

        /// <summary>Job constants used for simulation via unity jobs.</summary>
        public PhysicsJobConstants JobConstants => _jobConstants.Data;

        /// <summary>Solver constants used during physics simulation.</summary>
        public PhysicsSolverConstants SolverConstants => _solverConstants.Data;

        /// <summary>Managed state of the billiard.</summary>
        public BilliardState State => _state;

        public UnityBall SelectedBall => _selectedBallCached;
        public UnityCue SelectedCueStick => _selectedCueCached;

        public int SelectedBallId => _selectedBallId;
        public int SelectedCueStickId => _selectedCueStickId;

        /// <summary>White ball identifier.</summary>
        public int WhiteBallId { get; set; } = 0; // 通常、白球は ID 0

        /// <summary>Whether the game is in manual placement mode.</summary>
        public bool IsManualPlacementMode { get; set; }

        [SerializeField] protected int _selectedBallId;
        [SerializeField] protected int _selectedCueStickId;
        [SerializeField] protected bool _frezeBallsOutsidePlayingAreaRadius = true;
        [SerializeField] protected PhysicsJobConstantsSerializable _jobConstants;
        [SerializeField] protected PhysicsSolverConstantsSerializable _solverConstants;

        protected BilliardState _state;
        protected BilliardManagedScene _managedScene;
        protected List<PhysicsSolver.Event> _cachedEvents;
        protected BilliardUnityScene _unityScene;
        protected UnityBall _selectedBallCached;
        protected UnityCue _selectedCueCached;

        protected virtual void Awake()
        {
            _unityScene = new BilliardUnityScene()
            {
                Balls = FindObjectsOfType<UnityBall>(),
                Holes = FindObjectsOfType<UnityHole>(),
                Cushions = FindObjectsOfType<UnityCushion>(),
                Polygons = FindObjectsOfType<UnityPolygon>(),
                Cues = FindObjectsOfType<UnityCue>(),
            };

            _cachedEvents = new List<PhysicsSolver.Event>();
            _managedScene = new BilliardManagedScene(_unityScene);
            _state = new BilliardState(_managedScene, _jobConstants.Data, _solverConstants.Data);
            _selectedBallCached = _unityScene.Balls.GetTarget(_selectedBallId);
            _selectedCueCached = _unityScene.Cues.GetTarget(_selectedCueStickId);


            State.OnPhysicsEvent += OnPhysicsEvent;
            State.OnStableStateChange += OnStableStateChange;
        }

        protected virtual void Update()
        {
            _state.Tick(Time.deltaTime);

            foreach (var unityBall in _unityScene.Balls)
            {
                Ball ball = _state.GetTemporaryBall(unityBall.Identifier);

                // 手動配置モード中の白球は物理同期をスキップ
                if (IsManualPlacementMode && unityBall.Identifier == WhiteBallId)
                {
                    Debug.Log($"Manual placement mode: {unityBall.Identifier}");
                    continue;
                }

                if (ball.Identifier != unityBall.Identifier) Debug.Log($"Mismatch {ball.Identifier} {unityBall.Identifier}");
                if (ball.State != Ball.StateType.Normal) continue;
                unityBall.transform.SetPositionAndRotation((float3)ball.Position, ball.Rotation);
            }
        }

        protected virtual void OnPhysicsEvent(PhysicsSolver.Event ev)
        {
            if (ev.Type != PhysicsSolver.EventType.None)
                _cachedEvents.Add(ev);

            if (ev.Type == PhysicsSolver.EventType.PocketCollision)
            {
                Put(_unityScene.Balls[ev.BallIndex].Identifier, _unityScene.Holes[ev.OtherIndex].Identifier);
            }

        }


        /// <summary>
        /// Takes the pocketed ball from the hole.
        /// </summary>
        /// <param name="ballIdentifier">The ball identifier.</param>
        /// <returns><c>true</c> if ball is removed, <c>false</c> otherwise.</returns>
        public bool Take(int ballIdentifier)
        {
            Ball ball = State.GetPhysicsBall(ballIdentifier);
            foreach (var hole in _unityScene.Holes)
            {
                hole.GetComponent<UnityHoleDrop>().Remove(ballIdentifier);
            }

            if (ball.State == Ball.StateType.Pocketed)
            {
                ball.State = Ball.StateType.Normal;
                State.SetPhysicsBall(ball);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Puts the specified ball into a hole.
        /// </summary>
        /// <param name="ballIdentifier">The ball identifier.</param>
        /// <param name="holeIdentifier">The hole identifier.</param>
        public void Put(int ballIdentifier, int holeIdentifier)
        {
            var hole = _unityScene.Holes.GetTarget(holeIdentifier);
            var holeDrop = hole.GetComponent<UnityHoleDrop>();
            var unityBall = _unityScene.Balls.GetTarget(ballIdentifier);
            var ball = State.GetPhysicsBall(unityBall.Identifier);

            ball.State = Ball.StateType.Pocketed;
            holeDrop.Put(unityBall.Identifier, unityBall.transform, (float3)ball.Velocity, (float3)ball.AngularVelocity, (float)_solverConstants.Data.Gravity, () => { });
            State.SetPhysicsBall(ball);

            Debug.Log($"<color=lightblue>Pocketed ball {ball.Identifier}</color>");
        }

        /// <summary>
        /// Creates the reset point.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>ResetPoint.</returns>
        public ResetPoint CreateResetPoint(ICommand command)
        {
            return CreateResetPoint(command, DateTime.Now);
        }

        /// <summary>
        /// Creates the reset point.
        /// </summary>
        /// <param name="command">The command that was applied after reset point is created .</param>
        /// <param name="time">The time of the reset point.</param>
        /// <param name="userData">The user data, if any.</param>
        /// <returns>ResetPoint.</returns>
        /// <exception cref="Exception">Throws if billiard state is not stationary</exception>
        public ResetPoint CreateResetPoint(ICommand command, DateTime time, object userData = null)
        {
            if (!State.Stationary)
                throw new Exception("Billiard state must be stationary");

            return new ResetPoint() { Balls = State.GetPhysicsBalls(), Time = time, Command = command, UserData = userData };
        }

        /// <summary>
        /// Called when billiard [stable state change].
        /// </summary>
        protected virtual void OnStableStateChange(bool isStable)
        {
            _cachedEvents.Clear();
        }

        /// <summary>
        /// Returns an enumerable ball identifiers where each ball position is inside polygon.
        /// </summary>
        /// <param name="polygon">The polygon to test against.</param>
        /// <param name="balls">The balls which are tested.</param>
        /// <returns>Returns an enumerable ball identifiers</returns>
        public static IEnumerable<int> GetBallsInsidePolygon(Polygon polygon, params Ball[] balls)
        {
            return balls.Where(b => polygon.IsPointInPolygon((float3)b.Position)).Select(t => t.Identifier);
        }

        /// <summary>
        /// Returns an enumerable ball identifiers where each ball position is outside polygon.
        /// </summary>
        /// <param name="polygon">The polygon to test against.</param>
        /// <param name="balls">The balls which are tested.</param>
        /// <returns>Returns an enumerable ball identifiers</returns>
        public static IEnumerable<int> GetBallsOutsidePolygon(Polygon polygon, params Ball[] balls)
        {
            return balls.Where(b => !polygon.IsPointInPolygon((float3)b.Position)).Select(t => t.Identifier);
        }

        /// <summary>
        /// Returns an enumerable ball identifiers where each ball is in pocketed state.
        /// </summary>
        /// <param name="balls">The balls.</param>
        /// <returns>Returns an enumerable ball identifiers.</returns>
        public static IEnumerable<int> GetPocketedBalls(params Ball[] balls)
        {
            return balls.Where(b => b.State == Ball.StateType.Pocketed).Select(t => t.Identifier);
        }

        public IEnumerable<PhysicsSolver.Event> GetCachedEvents()
        {
            return _cachedEvents;
        }

        /// <summary>
        /// Resets the billiard state to a cached reset point. Command is not applied.
        /// </summary>
        public void Reset(ResetPoint point)
        {
            State.Reset(point.Balls);
        }

        public virtual bool CanExecuteCommand(IBilliardCommand command)
        {
            return true;
        }

        public virtual bool ExecuteCommand(IBilliardCommand command)
        {
            return command.Execute(State);
        }

        private void OnDestroy()
        {
            _state.Dispose();
        }
    }
}