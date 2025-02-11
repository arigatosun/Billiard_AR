/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using UnityEngine.Events;
using UnityEngine;
using Unity.VisualScripting;
using System.Linq;

namespace ibc
{
    using commands;
    using objects;
    using solvers;

    /// <summary>Class that manages eight pool billiard logic and state.</summary>
    public class EightPoolBilliard : Billiard
    {
        [SerializeField]
        private int _whiteBallIdentifier = 0;
        [SerializeField]
        private int _blackBallIdentifier = 8;

        /// <summary>
        /// Possible ball types used in the American billiard.
        /// </summary>
        public enum BallType : int
        {
            Solid = 0,
            Stripe = 1,
            Black = 2,
            None = 3,
        }

        /// <summary>
        /// Current game state.
        /// </summary>
        public enum GameState : int
        {
            Breakshot,
            Open,
            Active,
            Ended
        }

        /// <summary>
        /// Event type that occurred during the billiard game.
        /// </summary>
        public enum EventType : int
        {
            /// <summary>Invalid event</summary>
            None,
            /// <summary>Foul occurred</summary>
            Foul,
            /// <summary>Current player won</summary>
            PlayerWon,
            /// <summary>Indicates that it is next player turn</summary>
            NextPlayer,
            /// <summary>Current player lost</summary>
            PlayerLost,
            /// <summary>Indicates game state change</summary>
            GameStateChange,
        }

        /// <summary>
        /// Event is sent when new billiard event occurs.
        /// </summary>
        public UnityAction<EventType> OnEightPoolEvent;

        /// <summary>
        /// Current game state.
        /// </summary>
        public GameState CurrentGameState => _gameState;

        /// <summary>
        /// Index of the active player.
        /// </summary>
        public int ActivePlayerIndex => _currentPlayer;

        /// <summary>
        /// Index of the other player.
        /// </summary>
        public int OtherPlayerIndex => 1 - _currentPlayer;

        /// <summary>
        /// Ball type that is assigned for the active player.
        /// </summary>
        public BallType ActivePlayerBallType => _playerBallTypes[ActivePlayerIndex];

        /// <summary>
        /// Ball type that is assigned for the other player.
        /// </summary>
        public BallType OtherPlayerBallType => _playerBallTypes[OtherPlayerIndex];

        public bool BallTypesAssigned => ActivePlayerBallType != BallType.None;

        private GameState _gameState = GameState.Breakshot;
        private BallType[] _playerBallTypes;
        private int _currentPlayer;
        private ResetPoint _resetPoint;
        private Ball _whiteBallOriginal;

        protected override void Awake()
        {
            base.Awake();

            _playerBallTypes = new BallType[2] { BallType.None, BallType.None };
            _currentPlayer = 0;
        }


        private void Start()
        {
            _resetPoint = CreateResetPoint(default);
            _whiteBallOriginal = this.State.GetPhysicsBall(_whiteBallIdentifier);

            Debug.Log("Eight pool game started");

        }

        protected void InvokeEvent(EventType e)
        {
            OnEightPoolEvent?.Invoke(e);
            Debug.Log($"Event: {e}");
        }

        protected void ChangeGameState(GameState state)
        {
            switch (state)
            {
                case GameState.Breakshot:
                    break;
                case GameState.Active:
                    break;
                case GameState.Ended:
                    break;
            }

            _gameState = state;
            InvokeEvent(EventType.GameStateChange);
        }

        protected void SwitchTurn()
        {
            _currentPlayer = 1 - _currentPlayer;
            InvokeEvent(EventType.NextPlayer);
        }

        protected BallType GetBallTypeFromIdentifier(int id)
        {
            return Mathf.FloorToInt((float)id / 8) == 0 ? BallType.Solid : BallType.Stripe;
        }

        protected override void OnStableStateChange(bool isStable)
        {
            if (isStable)
            {
                var balls = State.GetPhysicsBalls();
                var pocketedBalls = GetPocketedBalls(balls);
                var cachedEvents = GetCachedEvents();
                bool anyBallPocketed = pocketedBalls.Any();
                bool whiteBallPocketed = pocketedBalls.Contains(_whiteBallIdentifier);
                bool blackBallPocketed = pocketedBalls.Contains(_blackBallIdentifier);
                bool anyBallOutsidePlayingArea = GetBallsOutsidePolygon(_managedScene.Polygons[0], balls).Any();
                PhysicsSolver.Event firstBallHit = cachedEvents.FirstOrDefault(t => t.Type == PhysicsSolver.EventType.BallCollision);


                switch (_gameState)
                {
                    case GameState.Breakshot:
                        if (blackBallPocketed)
                        {
                            if (whiteBallPocketed)
                            {
                                InvokeEvent(EventType.PlayerLost);
                                ChangeGameState(GameState.Ended);
                            }
                            else if (anyBallOutsidePlayingArea)
                            {
                                InvokeEvent(EventType.PlayerLost);
                                ChangeGameState(GameState.Ended);
                            }
                            else
                            {
                                InvokeEvent(EventType.PlayerWon);
                                ChangeGameState(GameState.Ended);
                            }
                        }

                        if (whiteBallPocketed)
                        {
                            //foul
                            InvokeEvent(EventType.Foul);
                            SwitchTurn();
                            
                            //take from the pocket
                            Take(_whiteBallIdentifier);
                            Reset(_resetPoint);
                        }
                        else
                        {
                            int numberOfDistinctCushionHits = cachedEvents.Where(t => t.Type == PhysicsSolver.EventType.CushionCollision &&
                            (t.BallIndex != _whiteBallIdentifier || t.OtherIndex != _whiteBallIdentifier)).DistinctBy(t => t.BallIndex).Count();

                            if (anyBallPocketed || numberOfDistinctCushionHits >= 4)
                            {
                                ChangeGameState(GameState.Open);
                            }
                            else
                            {
                                InvokeEvent(EventType.Foul);
                                SwitchTurn();
                                Reset(_resetPoint);
                            }
                        }
                        break;
                    case GameState.Open:
                    case GameState.Active:
                        if (blackBallPocketed)
                        {
                            if (whiteBallPocketed)
                            {
                                InvokeEvent(EventType.PlayerLost);
                                ChangeGameState(GameState.Ended);
                            }
                            else if (anyBallOutsidePlayingArea)
                            {
                                InvokeEvent(EventType.PlayerLost);
                                ChangeGameState(GameState.Ended);
                            }
                            else
                            {
                                InvokeEvent(EventType.PlayerWon);
                                ChangeGameState(GameState.Ended);
                            }
                        }

                        if (whiteBallPocketed)
                        {
                            //foul
                            InvokeEvent(EventType.Foul);
                            SwitchTurn();
                            State.SetPhysicsBall(_whiteBallOriginal);

                            //take from the pocket
                            Take(_whiteBallIdentifier);
                        }
                        else
                        {
                            if (anyBallPocketed)
                            {
                                var ball = pocketedBalls.First();
                                var ballType = GetBallTypeFromIdentifier(ball);

                                if (CurrentGameState == GameState.Open)
                                {
                                    ChangeGameState(GameState.Active);
                                    AssignBallType(ballType);
                                }
                                else
                                {
                                    //check if correct ball is pocketed
                                    if(ballType != ActivePlayerBallType)
                                    {
                                        InvokeEvent(EventType.Foul);
                                        SwitchTurn();
                                    }
                                }
                            }
                        }

                        break;
                    case GameState.Ended:
                        break;
                }




            }

            base.OnStableStateChange(isStable);

        }


        public void AssignBallType(BallType ballType)
        {
            _playerBallTypes[ActivePlayerIndex] = ballType;
            _playerBallTypes[OtherPlayerIndex] = (BallType)(1 - (int)ballType);
        }

        public override bool CanExecuteCommand(IBilliardCommand command)
        {
            if(command is StrikeCommand)
                return CanExecuteStrikeCommand((StrikeCommand)command);

            return false;
        }

        public bool CanExecuteStrikeCommand(StrikeCommand command)
        {
            if (!State.Stationary)
                return false;
            if (CurrentGameState == GameState.Ended)
                return false;

            return true;
        }
    }
}