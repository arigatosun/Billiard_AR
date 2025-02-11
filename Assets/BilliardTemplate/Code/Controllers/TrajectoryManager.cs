/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */
using System;
using Unity.Mathematics;
using UnityEngine;

namespace ibc.controller
{
    using objects;
    using solvers;

    /// <summary>Class that manages ball trajectory display.</summary>
    public class TrajectoryManager : MonoBehaviour
    {
        //Cue controller reference
        [SerializeField] private CueController _cueController;
        //Billiard state reference
        [SerializeField] private Billiard _billiard;
        //Trajectory controller prefab
        [SerializeField] private GameObject _trajectoryPrefab;
        //Trajectory ghost ball prefab
        [SerializeField] private GameObject _trajectoryGhostBallPrefab;
        //Trajectory ghost ball radius
        [SerializeField] private float _trajectoryGhostBallScaleFactor = 1f;
        [SerializeField] private float _velocityFactor = 1f;
        [SerializeField] private float _maxVelocity = .5f;
        [SerializeField] private float _minStrikeVelocity = 0.25f;

        [SerializeField] private PhysicsJobConstantsSerializable _physicsJobConstants;
        [SerializeField] private PhysicsSolverConstantsSerializable _physicsSolverConstants;

        private TrajectoryController _cueBallTrajectory;
        private TrajectoryController _objectBallTrajectoryController;
        
        private GameObject _ghostBall;
        private BilliardState _latestState;
        private Ball[] _originalBalls;
        private bool _showTrajectories;

        private void Start()
        {
            //update state on start
            OnBilliardStableStateReached();

            //listen for billiard state change
            _billiard.State.OnStableStateChange += OnStableStateChange;
            _cueController.OnStrikeCommandChange += OnCueTransformChange;

            //show trajectories by default
            _showTrajectories = true;

            //create trajectories
            _cueBallTrajectory = CreateTrajectory(_trajectoryPrefab);
            _objectBallTrajectoryController = CreateTrajectory(_trajectoryPrefab);
            _ghostBall = CreateGhostBall();
        }


        private void OnStableStateChange(bool isStable)
        {
            if (isStable)
            {
                OnBilliardStableStateReached();
                ShowTrajectories();
            }
            else
            {
                HideTrajectories();
            }
        }

        private TrajectoryController CreateTrajectory(GameObject prefab)
        {
            var obj = Instantiate(prefab, transform, false);
            var trajectoryController = obj.GetComponent<TrajectoryController>();
            return trajectoryController;
        }

        private GameObject CreateGhostBall()
        {
            var obj = Instantiate(_trajectoryGhostBallPrefab, transform, false);
            obj.transform.localScale = Vector3.one * (float)_billiard.SelectedBall.Radius * 2 * _trajectoryGhostBallScaleFactor;
            return obj;
        }

        private void ForEachTrajectory(Action<TrajectoryController> action)
        {
            action.Invoke(_cueBallTrajectory);
            action.Invoke(_objectBallTrajectoryController);
        }
        
        private void ResetTrajectories()
        {
            ForEachTrajectory(controller => controller.Reset());
        }

        private void ResetBalls()
        {
            _ghostBall.gameObject.SetActive(false);
        }

        private void HideTrajectoriesInternal()
        {
            ForEachTrajectory(controller => controller.Hide());
        }

        private void ShowTrajectoriesInternal()
        {
            ForEachTrajectory(controller => controller.Show());
        }

        private void OnBilliardStableStateReached()
        {
            if (_latestState != null)
                _latestState.Dispose();

            _latestState = new BilliardState(_billiard.State, _physicsJobConstants.Data, _physicsSolverConstants.Data);

            //cache the balls
            _originalBalls = _latestState.GetPhysicsBalls();
        }

        private void OnDestroy()
        {
            if (_latestState != null)
                _latestState.Dispose();
        }


        private void OnCueTransformChange()
        {
            if (_showTrajectories)
            {
                ShowTrajectoriesInternal();
                ResetTrajectories();
                ResetBalls();

                //resets balls and output 
                _latestState.Reset(_originalBalls);

                //perform strike command
                var strikeCommand = _cueController.GetStrikeCommand();
                strikeCommand.Velocity = math.max(_minStrikeVelocity, _cueController.GetDrawVelocity());
                strikeCommand.Execute(_latestState);
                
                //get velocity vector
                var ball = _latestState.GetPhysicsBall(_billiard.SelectedBall.Identifier);

                //add starting point
                _cueBallTrajectory.AddPoint(ball.Position);
                
                //try to find next collision event
                int maxIterations = 1000;
                int it = 0;
                PhysicsSolver.Event collisionEvent = _latestState.Solver.GetNextEvent(_latestState.GetPhysicsScene());
                _latestState.Solver.Step(_latestState.GetPhysicsScene(), collisionEvent);
                Ball newBall;
                do
                {
                    collisionEvent = _latestState.Solver.GetNextEvent(_latestState.GetPhysicsScene());
                    _latestState.Solver.Step(_latestState.GetPhysicsScene(), collisionEvent);
                    newBall = _latestState.GetPhysicsBall(_billiard.SelectedBall.Identifier);
                    it++;
                    
                    switch (collisionEvent.Type)
                    {
                        case PhysicsSolver.EventType.BallCollision:
                        {
                            _cueBallTrajectory.AddPoint(newBall.Position);
                            _ghostBall.transform.position = (float3)newBall.Position;
                            _ghostBall.gameObject.SetActive(true);

                            var firstBall = _latestState.GetPhysicsBallByIndex(collisionEvent.BallIndex);
                            var objectBall = _latestState.GetPhysicsBallByIndex(collisionEvent.OtherIndex);
                            if (firstBall.Identifier != ball.Identifier)
                                objectBall = firstBall;

                            var vel = math.length(objectBall.Velocity) * _velocityFactor;
                            var newPos = objectBall.Position + math.clamp(vel, 0, _maxVelocity) * math.normalizesafe(objectBall.Velocity);

                            _objectBallTrajectoryController.AddPoint(objectBall.Position);
                            _objectBallTrajectoryController.AddPoint(newPos);
                        }

                            break;
                        case PhysicsSolver.EventType.CushionCollision:
                        {
                            _cueBallTrajectory.AddPoint(newBall.Position);
                            _ghostBall.transform.position = (float3)newBall.Position;
                            _ghostBall.gameObject.SetActive(true);

                            var vel = math.length(newBall.Velocity) * _velocityFactor;
                            if (vel > 1E-5f)
                            {
                                var newPos = newBall.Position + math.clamp(vel, 0, _maxVelocity) * math.normalizesafe(newBall.Velocity);

                                _objectBallTrajectoryController.AddPoint(newBall.Position);
                                _objectBallTrajectoryController.AddPoint(newPos);
                            }
                        }
                            break;
                        case PhysicsSolver.EventType.PocketCollision:
                            
                            _cueBallTrajectory.AddPoint(newBall.Position);
                            _ghostBall.transform.position = (float3)newBall.Position;
                            _ghostBall.gameObject.SetActive(true);

                            break;
                    }

                    if (IsCollisionEvent(collisionEvent.Type))
                        break;

                } while (collisionEvent.Type != PhysicsSolver.EventType.None && it < maxIterations);

                if (!IsCollisionEvent(collisionEvent.Type))
                {
                    _cueBallTrajectory.AddPoint(newBall.Position);
                }
            }
        }
        

        private bool IsCollisionEvent(PhysicsSolver.EventType collisionEventType)
        {
            if (collisionEventType == PhysicsSolver.EventType.StateTransition)
                return false;
            return true;
        }
        
        public void ShowTrajectories()
        {
            _showTrajectories = true;
        }

        public void HideTrajectories()
        {
            ResetBalls();
            HideTrajectoriesInternal();
            _showTrajectories = false;
        }

    }
}