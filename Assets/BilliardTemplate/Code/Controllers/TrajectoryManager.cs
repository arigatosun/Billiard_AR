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
        // (省略 - 既存の変数はそのまま)
        [SerializeField] private CueController _cueController;
        [SerializeField] private Billiard _billiard;
        [SerializeField] private GameObject _trajectoryPrefab;
        [SerializeField] private GameObject _trajectoryGhostBallPrefab;
        [SerializeField] private GameObject _circlePrefab; // CircleControllerのプレハブ
        [SerializeField] private float _trajectoryGhostBallScaleFactor = 1f;
        [SerializeField] private float _velocityFactor = 1f;
        [SerializeField] private float _maxVelocity = .5f;
        [SerializeField] private float _minStrikeVelocity = 0.25f;
        [SerializeField] private float _defaultStrikeVelocity = 0.5f; // 追加
        [SerializeField] private int _maxCushionBounces = 3; // 追加
        [SerializeField] private float _circleRadiusMultiplier = 3f; // 追加：円の半径の倍率

        private CircleController _whiteBallCircle; // 新しく追加

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
            _cueController.OnStrikeCommandChange += OnCueTransformChange; //キュー操作時のイベント

            //show trajectories by default
            _showTrajectories = true;

            //create trajectories
            _cueBallTrajectory = CreateTrajectory(_trajectoryPrefab);
            _objectBallTrajectoryController = CreateTrajectory(_trajectoryPrefab);
            _ghostBall = CreateGhostBall();

            // 円の初期化を追加
            InitializeCircle();

            // 初期状態で軌道を表示 (追加)
            if (_showTrajectories)
            {
                ShowTrajectoriesInternal();
                CalculateAndShowTrajectories();
            }
        }

        private void InitializeCircle()
        {
            // CircleControllerのインスタンスを作成
            var circleObj = Instantiate(_circlePrefab, transform, false);
            _whiteBallCircle = circleObj.GetComponent<CircleController>();

            // 白玉の周りに円を初期化
            float circleRadius = (float)_billiard.SelectedBall.Radius * _circleRadiusMultiplier;
            _whiteBallCircle.Initialize(_billiard.SelectedBall, circleRadius);
        }


        private void OnStableStateChange(bool isStable)
        {
            if (isStable)
            {
                OnBilliardStableStateReached();
                // ShowTrajectories(); // ここはCalculateAndShowTrajectoriesを直接呼ぶ
                if (_showTrajectories) //追加
                {
                    ShowTrajectoriesInternal();
                    CalculateAndShowTrajectories();  // 状態が安定したら軌道再計算
                }

            }
            else
            {
                HideTrajectories();
            }
        }

        private TrajectoryController CreateTrajectory(GameObject prefab)
        {
            var obj = Instantiate(prefab, transform, false);
            obj.layer = LayerMask.NameToLayer("TrajectoryLine");
            var trajectoryController = obj.GetComponent<TrajectoryController>();
            return trajectoryController;
        }

        private GameObject CreateGhostBall()
        {
            var obj = Instantiate(_trajectoryGhostBallPrefab, transform, false);
            obj.layer = LayerMask.NameToLayer("TrajectoryLine");
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
            if (_whiteBallCircle != null)
            {
                _whiteBallCircle.Hide();
            }
        }

        private void ShowTrajectoriesInternal()
        {
            ForEachTrajectory(controller => controller.Show());
            if (_whiteBallCircle != null)
            {
                _whiteBallCircle.Show();
            }
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
            // キューの位置/角度が変わった時も軌道を再計算 (役割変更)
            if (_showTrajectories)
            {
                //ShowTrajectoriesInternal(); 不要。すでに表示されているはず
                CalculateAndShowTrajectories();
            }
        }



        // 軌道計算と表示の共通ロジック (新規関数)
        private void CalculateAndShowTrajectories()
        {
            // ① 既存の初期化まわりはそのまま
            ResetTrajectories();
            ResetBalls();

            _latestState.Reset(_originalBalls);

            var strikeCommand = _cueController.GetStrikeCommand();
            float currentVelocity = _cueController.GetDrawVelocity();
            strikeCommand.Velocity = currentVelocity > 0
                ? math.max(_minStrikeVelocity, currentVelocity)
                : _defaultStrikeVelocity;
            strikeCommand.Execute(_latestState);

            var ball = _latestState.GetPhysicsBall(_billiard.SelectedBall.Identifier);

            // 手玉の初期位置を軌道に追加
            _cueBallTrajectory.AddPoint(ball.Position);

            int maxIterations = 1000;
            int it = 0;
            int cushionBounces = 0;

            // ② 最初の衝突イベントと衝突解決を行う
            PhysicsSolver.Event collisionEvent =
                _latestState.Solver.GetNextEvent(_latestState.GetPhysicsScene());

            // 「衝突直前の接触点」が返ってくる想定
            double3 contactPoint;
            _latestState.Solver.Step(_latestState.GetPhysicsScene(), collisionEvent, out contactPoint);

            Ball newBall;
            do
            {
                // 次の衝突イベントを取得 & 解決
                collisionEvent = _latestState.Solver.GetNextEvent(_latestState.GetPhysicsScene());
                _latestState.Solver.Step(_latestState.GetPhysicsScene(), collisionEvent, out contactPoint);

                // 衝突後のボール状態を取得
                newBall = _latestState.GetPhysicsBall(_billiard.SelectedBall.Identifier);
                it++;

                // ③ 衝突種類に応じた描画処理
                switch (collisionEvent.Type)
                {
                    case PhysicsSolver.EventType.BallCollision:
                        {
                            // 衝突点が有効かどうかを判定（Infinityでないか）
                            if (!math.any(math.isinf(contactPoint)))
                            {
                                _cueBallTrajectory.AddPoint(contactPoint);
                                _ghostBall.transform.position = (float3)contactPoint;
                            }
                            else
                            {
                                // 衝突点が無効なら従来どおり newBall.Position を使う
                                _cueBallTrajectory.AddPoint(newBall.Position);
                                _ghostBall.transform.position = (float3)newBall.Position;
                            }

                            _ghostBall.gameObject.SetActive(true);

                            // ボール同士の衝突では、相手ボールの簡易予測線も表示する
                            var firstBall = _latestState.GetPhysicsBallByIndex(collisionEvent.BallIndex);
                            var objectBall = _latestState.GetPhysicsBallByIndex(collisionEvent.OtherIndex);
                            if (firstBall.Identifier != ball.Identifier)
                                objectBall = firstBall;

                            var vel = math.length(objectBall.Velocity) * _velocityFactor;
                            var newPos = objectBall.Position
                                + math.clamp(vel, 0, _maxVelocity)
                                * math.normalizesafe(objectBall.Velocity);

                            _objectBallTrajectoryController.AddPoint(objectBall.Position);
                            _objectBallTrajectoryController.AddPoint(newPos);

                            // 手玉が的球に当たった時点で軌道予測終了
                            return;
                        }

                    case PhysicsSolver.EventType.CushionCollision:
                        {
                            cushionBounces++;

                            // 衝突点が有効ならそちらを描画
                            if (!math.any(math.isinf(contactPoint)))
                            {
                                _cueBallTrajectory.AddPoint(contactPoint);
                                _ghostBall.transform.position = (float3)contactPoint;
                            }
                            else
                            {
                                _cueBallTrajectory.AddPoint(newBall.Position);
                                _ghostBall.transform.position = (float3)newBall.Position;
                            }

                            _ghostBall.gameObject.SetActive(true);
                        }
                        break;

                    case PhysicsSolver.EventType.PocketCollision:
                        {
                            // 衝突点が有効ならそちらを描画
                            if (!math.any(math.isinf(contactPoint)))
                            {
                                _cueBallTrajectory.AddPoint(contactPoint);
                                _ghostBall.transform.position = (float3)contactPoint;
                            }
                            else
                            {
                                _cueBallTrajectory.AddPoint(newBall.Position);
                                _ghostBall.transform.position = (float3)newBall.Position;
                            }

                            _ghostBall.gameObject.SetActive(true);
                            // ポケット衝突で予測終了
                            return;
                        }
                }

            } while (
                cushionBounces < _maxCushionBounces &&
                collisionEvent.Type != PhysicsSolver.EventType.None &&
                it < maxIterations
            );

            // ④ ループ終了時の最終位置を追加
            _cueBallTrajectory.AddPoint(newBall.Position);
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
            OnCueTransformChange(); // 追加
        }

        public void HideTrajectories()
        {
            ResetBalls();
            HideTrajectoriesInternal();
            _showTrajectories = false;
        }

    }
}