/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

using System;
using Unity.Mathematics;
using Unity.Collections;

namespace ibc.solvers
{
    using objects;

    [Serializable]
    public struct PhysicsImpactParameters
    {

        /// <summary>The normal restitution coefficient with values in range [0,1] describes the degree to which the initial normal relative velocity is reversed.</summary>
        public double NormalRestitution;
        /// <summary>The tangential restitution coefficient with values in range [-1, 1] describes the degree to which the initial tangential relative velocity is reversed.</summary>
        public double TangentialRestitution;
        /// <summary>The standard friction coefficient with values in range [0,inf] is used to bound the impulse within the friction cone..</summary>
        public double Friction;
    }

    /// <summary>
    /// Container for the constants used in the physics solver. Constant values are taken from:
    /// - https://billiards.colostate.edu/faq/physics/physical-properties/
    /// </summary>
    [Serializable]
    public struct PhysicsSolverConstants
    {
        /// <summary>The gravity of earth. Example value is: 9.81.</summary>
        public double Gravity;

        /// <summary>Ball-Cloth sliding friction. Example value is 0.2 and range [0.15, 0.4] </summary>
        public double SlidingFriction;

        /// <summary>Ball-Cloth spinning friction (due to area contact surface between ball and table). Example value is: 0.2 </summary>
        public double SpinningFriction;

        /// <summary>Ball-Cloth rolling friction. Example value is 0.015 and range is [0.005, 0.015]</summary>
        public double RollingFriction;

        public PhysicsImpactParameters BallToSlateImpactParameters;
        public PhysicsImpactParameters BallToCushionImpactParameters;
        public PhysicsImpactParameters BallToBallImpactParameters;

        /// <summary>The sleep velocity of the rigid body. Example value is 1 * 10^-3 m/s that is 1 mm per second.
        /// All velocities below this threshold are set to zero.
        /// </summary>
        public double SleepVelocity;

        /// <summary>The sleep angular velocity of the rigid body. Example value is 1 * 10^-3 rad/s that is ~0.057 degrees per second.
        /// All velocities below this threshold are set to zero.
        /// </summary>
        public double SleepAngularVelocity;

        /// <summary>Error bound used when solving polynomials, should be few orders of magnitude larger then zero epsilon.
        /// Value of 1E-10 indicates that polynomial root solver absolute error is +-0.5E-10 </summary>
        public double PolynomialErrorBound;

        /// <summary>Error tolerance used for tolerating floating point arithmetic error that can arise during calculations of ball to cushion intersection plus
        /// the error introduced by the polynomial solver(due to error bound).
        /// Error tolerance should be few orders in magnitude larger then polynomial error bound. (example: PolynomialErrorBound=1E-12  --->  CushionSolverErrorTolerance=1E-8).
        /// Lower error tolerance is better as it removes false negatives.
        /// Value of 1E-3 indicates that ball-cushion collisions are registered if predicted ball position by the polynomial solver
        /// is within 1mm of the cushion. 
        /// </summary>
        public double CushionSolverErrorTolerance;

        /// <summary>Minimum applied displacement when collision occurs to move the bodies apart. This is done in order to prevent penetration and improve performance,
        /// typical value is 1E-3 or 1mm.
        /// </summary>
        public double CollisionAppliedDisplacement;

        /// <summary>Whether simulation is constrained to a plane. If enabled improves performance and simulation stability. </summary>
        public bool Planar;

    }

    [Serializable]
    /// <summary>
    /// Physics scene representation.
    /// </summary>
    public struct PhysicsScene : IDisposable
    {
        public NativeArray<Ball> Balls;
        public NativeArray<Hole> Holes;
        public NativeArray<Cushion> Cushions;

        public PhysicsScene(BilliardManagedScene scene, Allocator allocator)
        {
            Balls = new NativeArray<Ball>(scene.Balls, allocator);
            Holes = new NativeArray<Hole>(scene.Holes, allocator);
            Cushions = new NativeArray<Cushion>(scene.Cushions, allocator);
        }

        public void Dispose()
        {
            Balls.Dispose();
            Holes.Dispose();
            Cushions.Dispose();
        }
    }

    [Serializable]
    /// <summary>Physics Solver contains methods for solving equations of motions, calculating next event and related.</summary>
    public struct PhysicsSolver
    {
        private const double Infinity = double.MaxValue;
        private static readonly double3 UnitY = new double3(0, 1, 0);
        private const int StationaryMask = (int)Ball.MotionType.Stationary | (int)Ball.MotionType.StationarySpin;
        private const int MovementMask = ~StationaryMask;

        /// <summary> Type of the event that can occur during simulation for a given ball. </summary>
        public enum EventType
        {
            /// <summary>Indicates no event</summary>
            None,
            /// <summary>Indicates ball `motion` state transition</summary>
            StateTransition,
            /// <summary>Indicates ball collision with another ball</summary>
            BallCollision,
            /// <summary>Indicates ball collision with cushion line</summary>
            CushionCollision,
            /// <summary>Indicates ball collision with hole</summary>
            PocketCollision,
        }

        /// <summary> Container for a physics event </summary>
        public struct Event
        {
            /// <summary>The time at which event `will` occur. Valid for all events, even for None.</summary>
            public double Time;
            /// <summary>The main ball <strong>index</strong>
            /// for which the event is created.</summary>
            public int BallIndex;
            /// <summary>The other object <strong>index</strong>
            /// that is part of the event(if any).</summary>
            public int OtherIndex;
            /// <summary>The cushion vertex index, in case of vertex cushion collision event.</summary>
            public int VertexIndex;
            /// <summary>The event type.</summary>
            public EventType Type;
            /// <summary>The next motion state for the ball, in case of transition event.</summary>
            public Ball.MotionType Motion;
        }

        private readonly PhysicsSolverConstants _constants;

        /// <summary>Constructs new physics solver with specified parameters </summary>
        public PhysicsSolver(PhysicsSolverConstants constants)
        {
            _constants = constants;
        }

        /// <summary> Returns ball motion based on current ball velocity and angular velocity.</summary>
        private Ball.MotionType CalculateMotion(Ball ball)
        {
            if (ball.Velocity.y * ball.Velocity.y > math.EPSILON_DBL)
                return Ball.MotionType.Airborne;

            if (math.lengthsq(ball.Velocity + math.cross(ball.AngularVelocity, -UnitY * ball.Radius)) > math.EPSILON_DBL)
                return Ball.MotionType.Sliding;

            if (math.lengthsq(ball.Velocity) > math.EPSILON_DBL || math.lengthsq(ball.AngularVelocity.xz) > math.EPSILON_DBL)
                return Ball.MotionType.Rolling;

            if (math.lengthsq(ball.AngularVelocity.y) > math.EPSILON_DBL)
                return Ball.MotionType.StationarySpin;

            return Ball.MotionType.Stationary;
        }

        /// <summary> Returns next state for the ball with transition time. </summary>
        private double GetMotionTransitionEvent(Ball ball, out Ball.MotionType motion)
        {
            switch (ball.State)
            {
                case Ball.StateType.Normal: break;
                case Ball.StateType.Struck: motion = CalculateMotion(ball); return 0;
                case Ball.StateType.Pocketed: motion = Ball.MotionType.Stationary; return Infinity;
                case Ball.StateType.Invalid: motion = Ball.MotionType.Stationary; return 0;
                default: throw new ArgumentOutOfRangeException();
            }

            switch (ball.Motion)
            {
                case Ball.MotionType.Stationary:
                    {
                        //stationary ball remains stationary
                        motion = Ball.MotionType.Stationary;
                        return Infinity;
                    }
                case Ball.MotionType.Rolling:
                    {
                        //rolling ball can either become stationary or stationary with spin about vertical axis
                        double spinTime = SpinningTime(ball);
                        double rollTime = RollingTime(ball);
                        if (spinTime > rollTime)
                        {
                            motion = Ball.MotionType.StationarySpin;
                            return rollTime;
                        }

                        motion = Ball.MotionType.Stationary;
                        return rollTime;
                    }
                case Ball.MotionType.Sliding:
                    {
                        //TODO: check if sliding directly goes to stationary state by checking whether velocity and angular velocity are zero at the end of the sliding
                        double slideTime = SlidingTime(ball);
                        motion = Ball.MotionType.Rolling;
                        return slideTime;
                    }
                case Ball.MotionType.StationarySpin:
                    {
                        double spinTime = SpinningTime(ball);
                        motion = Ball.MotionType.Stationary;
                        return spinTime;
                    }
                case Ball.MotionType.Airborne:
                    {
                        double airborneTime = AirborneTime(ball);
                        motion = Ball.MotionType.Landing;
                        return airborneTime;
                    }
                case Ball.MotionType.Landing:
                    {
                        motion = Ball.MotionType.Landing;
                        return Infinity;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary> Returns all real roots for the polynomial formed via dot product D^2 = ||A*t^2 + B*t + C||^2 </summary>
        private double RunPolySolver(double3 A, double3 B, double3 C, double D, double timeBound)
        {
            double root;
            double c4 = math.dot(A, A);
            double c3 = 2 * math.dot(A, B);
            double c2 = 2 * math.dot(A, C) + math.dot(B, B);
            double c1 = 2 * math.dot(B, C);
            double c0 = math.dot(C, C) - (D * D);

            Poly4 poly = new Poly4(c4, c3, c2, c1, c0);
            if (poly.SmallestPositiveRoot(timeBound, _constants.PolynomialErrorBound, out root) && root >= 0)
                return root;

            return Infinity;
        }

        /// <summary> Returns time of impact between two balls. </summary>
        private double GetBall2BallCollisionEvent(Ball ball1, Ball ball2, double timeBound)
        {

            double r1r2 = ball1.Radius + ball2.Radius;
            double dist = math.length(ball1.Position - ball2.Position);
            double pen = dist - r1r2;

            double3 v = ball1.Velocity - ball2.Velocity;
            if (pen < 0 && math.dot(ball1.Position - ball2.Position, v) <= 0)
                return 0;

            double3 A = ball1.AccCoeff - ball2.AccCoeff;
            double3 B = ball1.VelCoeff - ball2.VelCoeff;
            double3 C = ball1.Position - ball2.Position;

            return RunPolySolver(A, B, C, ball1.Radius + ball2.Radius, timeBound);
        }

        /// <summary> Returns time of impact between ball and a point</summary>
        private double GetBall2PointCollisionEvent(Ball ball, double3 p0, double timeBound)
        {
            double dist = math.length(ball.Position - p0);
            double pen = dist - ball.Radius;

            if (pen < 0)
                return 0;

            return RunPolySolver(ball.AccCoeff, ball.VelCoeff, ball.Position - p0, ball.Radius, timeBound);
        }

        /// <summary> Returns closest point on a line segment</summary>
        private double3 GetClosestPointOnLineSegment(double3 pos, double3 p0, double3 dir, double dist)
        {
            double d = math.dot(pos - p0, dir);
            double dc = math.clamp(d, 0, dist);
            return p0 + dir * dc;
        }

        /// <summary> Returns time of impact between ball and cushion. </summary>
        private double GetBall2CushionCollisionEvent(Ball ball, Cushion cushion, double timeBound, out int vertex)
        {
            vertex = -1;

            double3 p0 = cushion.P0;
            double3 p1 = cushion.P1;

            if (!_constants.Planar)
            {
                p0 += UnitY * cushion.Height;
                p1 += UnitY * cushion.Height;
            }

            //check if intersecting
            double3 cp = GetClosestPointOnLineSegment(ball.Position, p0, cushion.Dir, cushion.Distance);
            double dist = math.length(cp - ball.Position);
            double pen = dist - ball.Radius;
            if (pen < 0)
                return 0;

            double3 A = math.cross(ball.AccCoeff, cushion.Dir);
            double3 B = math.cross(ball.VelCoeff, cushion.Dir);
            double3 C = math.cross(ball.Position - p0, cushion.Dir);
            double root = RunPolySolver(A, B, C, ball.Radius, timeBound);

            //sphere intersects line
            if (root < Infinity)
            {
                //check if point lies on the segment
                double3 newPos = (ball.AccCoeff * root + ball.VelCoeff) * root + ball.Position;
                double3 newCp = GetClosestPointOnLineSegment(newPos, p0, cushion.Dir, cushion.Distance);
                double newDist = math.length(newCp - newPos);
                double newPen = newDist - ball.Radius;

                //order of checking is important
                //first line then vertex

                //threshold is provided such that is few orders of magnitude greater than polynomial error bound
                //its better to get false positive then false negative, so lower error is better
                if (newPen < _constants.CushionSolverErrorTolerance)
                    return root;

                //check if sphere intersects line segment end points
                double r1 = GetBall2PointCollisionEvent(ball, p0, timeBound);
                if (r1 < Infinity)
                {
                    vertex = 0;
                    return r1;
                }

                double r2 = GetBall2PointCollisionEvent(ball, p1, timeBound);
                if (r2 < Infinity)
                {
                    vertex = 1;
                    return r2;
                }

            }

            return Infinity;
        }

        /// <summary> Return collision time between ball and a pocket</summary>
        private double GetBall2PocketCollisionEvent(Ball ball, Hole hole, double timeBound)
        {
            double r1r2 = math.pow(ball.Radius + hole.Radius, 2);
            double dist = math.length(ball.Position - hole.Position);
            double pen = dist - r1r2;

            if (pen < 0)
                return 0;

            return RunPolySolver(ball.AccCoeff, ball.VelCoeff, ball.Position - hole.Position, ball.Radius + hole.Radius, timeBound);
        }

        /// <summary> Returns true if provided state type mask contains at least one moving state bit set</summary>
        private bool IsMoving(int mask)
        {
            return (mask & MovementMask) != 0;
        }

        /// <summary> Returns next closest physics event in time </summary>
        public Event GetNextEvent(PhysicsScene scene)
        {
            var balls = scene.Balls;
            var holes = scene.Holes;
            var cushions = scene.Cushions;

            Event e = default;
            e.Time = Infinity;
            e.Type = EventType.None;

            //get next transition event
            for (int i = 0; i < balls.Length; ++i)
            {
                double time = GetMotionTransitionEvent(balls[i], out var state);
                if (e.Time <= time) continue;

                e.Time = time;
                e.Motion = state;
                e.Type = EventType.StateTransition;
                e.BallIndex = i;

                //exit early
                if (e.Time == 0) return e;
            }

            //get next ball to ball collision event
            for (int i = 0; i < balls.Length; ++i)
            {
                if (balls[i].State != Ball.StateType.Normal) continue;

                for (int j = i + 1; j < balls.Length; ++j)
                {

                    if (balls[j].State != Ball.StateType.Normal) continue;

                    int state = (int)balls[i].Motion | (int)balls[j].Motion;

                    if (!IsMoving(state)) continue;

                    double time = GetBall2BallCollisionEvent(balls[i], balls[j], e.Time);
                    if (e.Time <= time) continue;

                    e.Time = time;
                    e.Type = EventType.BallCollision;
                    e.BallIndex = i;
                    e.OtherIndex = j;

                    //exit early
                    if (e.Time == 0) return e;
                }
            }

            //get next ball to pocket collision event
            for (int i = 0; i < balls.Length; ++i)
            {
                if (balls[i].State != Ball.StateType.Normal) continue;

                for (int j = 0; j < holes.Length; ++j)
                {
                    int state = (int)balls[i].Motion;
                    if (!IsMoving(state)) continue;

                    double time = GetBall2PocketCollisionEvent(balls[i], holes[j], e.Time);
                    if (e.Time <= time) continue;

                    e.Time = time;
                    e.Type = EventType.PocketCollision;
                    e.BallIndex = i;
                    e.OtherIndex = j;

                    //exit early
                    if (e.Time == 0) return e;
                }
            }


            //get next ball to cushion collision event
            for (int i = 0; i < balls.Length; ++i)
            {
                if (balls[i].State != Ball.StateType.Normal) continue;

                for (int j = 0; j < cushions.Length; ++j)
                {
                    int state = (int)balls[i].Motion;
                    if (!IsMoving(state)) continue;

                    double time = GetBall2CushionCollisionEvent(balls[i], cushions[j], e.Time, out var vertex);
                    if (e.Time <= time) continue;

                    e.Time = time;
                    e.Type = EventType.CushionCollision;
                    e.BallIndex = i;
                    e.OtherIndex = j;
                    e.VertexIndex = vertex;

                    //exit early
                    if (e.Time == 0) return e;
                }
            }

            return e;
        }

        /// <summary> Updates provided ball array to match reference physics scene state, just offset by delta time. Rotation is preserved. </summary>
        public void Step(PhysicsScene scene, Ball[] balls, double dt)
        {
            for (var i = 0; i < balls.Length; i++)
            {
                quaternion origRot = balls[i].Rotation;
                StepBallState(scene.Balls[i], out balls[i], dt);
                balls[i].Rotation = origRot;
            }
        }

        public void Step(PhysicsScene scene, Event e)
        {
            // outパラメータは使わないので、discardまたは変数に捨てる
            Step(scene, e, out _);
        }

        /// <summary> Moves physics scene to the next state which is when the provided event happens </summary>
        public void Step(PhysicsScene scene, Event e, out double3 contactPoint)
        {
            // 衝突点が無い場合のデフォルト値として∞ベクトル
            contactPoint = new double3(
                double.PositiveInfinity,
                double.PositiveInfinity,
                double.PositiveInfinity
            );

            if (e.Type == EventType.None)
                return;

            var balls = scene.Balls;
            var cushions = scene.Cushions;

            double t = math.max(e.Time, 0);
            if (t > 0)
            {
                // 時間 t だけ物理を進める
                for (var i = 0; i < balls.Length; i++)
                {
                    if (balls[i].State != Ball.StateType.Normal) continue;
                    StepBallState(balls[i], out var b, t);
                    CacheCoeff(ref b);
                    balls[i] = b;
                }
            }

            // イベント対象のボール
            Ball ball = balls[e.BallIndex];

            switch (e.Type)
            {
                // 1) モーションや状態遷移イベント
                case EventType.StateTransition:
                    {
                        // 例：もしボールが "Struck" 状態なら "Normal" に戻す
                        if (ball.State == Ball.StateType.Struck)
                            ball.State = Ball.StateType.Normal;

                        // 例：イベントに格納されているモーションへ移行
                        ball.Motion = e.Motion;

                        // 例：空中から着地(Landing)するタイミングで床衝突を解決→再度モーションを計算
                        if (ball.Motion == Ball.MotionType.Landing)
                        {
                            ResolveBallSurfaceImpact(ref ball);
                            ball.Motion = CalculateMotion(ball);
                        }
                        break;
                    }

                // 2) ポケット衝突イベント
                case EventType.PocketCollision:
                    {
                        // ボールをポケット済みに
                        ball.Motion = Ball.MotionType.Stationary;
                        ball.State = Ball.StateType.Pocketed;

                        // もし衝突点を描画したいなら、衝突前の位置を contactPoint に入れる
                        // contactPoint = ball.Position; // etc...
                        break;
                    }

                // 3) ボール同士の衝突イベント
                case EventType.BallCollision:
                    {
                        // もう一方のボールを取得
                        // （ 例： e.OtherIndex のボール ）
                        Ball otherBall = balls[e.OtherIndex];

                        // 衝突前の接触点を計算したい場合は、以下のように書ける
                        //  (衝突前の位置を使う or 2球の中心から法線を出すなど)
                        //  contactPoint = ...;

                        // 実際の衝突解決
                        ResolveBallToBallImpact(ref ball, ref otherBall);

                        // 衝突後、双方のモーションを再判定
                        ball.Motion = CalculateMotion(ball);
                        otherBall.Motion = CalculateMotion(otherBall);

                        // 変更を元の配列に反映
                        balls[e.OtherIndex] = otherBall;
                        break;
                    }

                // 4) クッション衝突イベント
                case EventType.CushionCollision:
                    {
                        // 衝突前のボール位置などから contactPoint を計算 (描画用)
                        double3 preImpactPos = ball.Position;
                        Cushion cushion = cushions[e.OtherIndex];
                        double3 p0 = cushion.P0;
                        if (_constants.Planar)
                            p0 += new double3(0, 1, 0) * cushion.Height;

                        double3 cp = GetClosestPointOnLineSegment(preImpactPos, p0, cushion.Dir, cushion.Distance);
                        double3 normal = cushion.Normal;
                        if (math.dot(normal, cp - preImpactPos) > 0)
                            normal = -normal;

                        // ボール表面の接触点を可視化用に記録
                        contactPoint = preImpactPos - normal * ball.Radius;

                        // 実際の衝突解決
                        if (e.VertexIndex == -1)
                        {
                            ResolveBallCushionImpact(ref ball, cushion);
                        }
                        else
                        {
                            ResolveBallVertexImpact(ref ball, cushion, e.VertexIndex);
                        }
                        // 衝突後のモーションを再判定
                        ball.Motion = CalculateMotion(ball);
                        break;
                    }
            }

            // 更新されたボールデータを書き戻す
            CacheCoeff(ref ball);
            balls[e.BallIndex] = ball;
        }

        private double SlidingTime(Ball b)
        {
            double3 relVelCp = b.Velocity + math.cross(b.AngularVelocity, new double3(0, -b.Radius, 0));
            return 2 / 7.0 * (math.length(relVelCp) / (_constants.Gravity * _constants.SlidingFriction));
        }

        private double AirborneTime(Ball b)
        {
            Poly2 poly2 = new Poly2(-0.5 * _constants.Gravity, b.Velocity.y, b.Position.y);
            if (poly2.LargestPositiveRoot(out var root) && root >= 0)
                return root;
            return Infinity;
        }

        private double RollingTime(Ball b)
        {
            return math.length(b.Velocity) / (_constants.Gravity * _constants.RollingFriction);
        }

        private double SpinningTime(Ball b)
        {
            return 2 * b.Radius / (5 * _constants.SpinningFriction * _constants.Gravity) * math.abs(b.AngularVelocity.y);
        }

        private void SetHeight(ref Ball b0, double height)
        {
            b0.Position.y = height > 0 ? height : 0;
        }

        private void SetPosition(ref Ball b0, double2 pos)
        {
            b0.Position.x = pos.x;
            b0.Position.z = pos.y;
        }

        private void SetPosition(ref Ball b0, double3 pos)
        {
            SetPosition(ref b0, pos.xz);
            SetHeight(ref b0, pos.y);
        }

        private void SetVelocity(ref Ball b0, double3 vel)
        {
            if (_constants.Planar)
            {
                vel.y = 0;
            }

            if (math.lengthsq(vel) > _constants.SleepVelocity * _constants.SleepVelocity)
                b0.Velocity = vel;
            else
                b0.Velocity = double3.zero;
        }

        private void SetAngularVelocity(ref Ball b0, double3 vel)
        {
            if (math.lengthsq(vel) > _constants.SleepAngularVelocity * _constants.SleepAngularVelocity)
                b0.AngularVelocity = vel;
            else
                b0.AngularVelocity = double3.zero;
        }

        private void StepBallState(Ball b0, out Ball b1, double dt)
        {
            b1 = b0;
            double spinTime = SpinningTime(b0); //total spin time
            switch (b0.Motion)
            {
                case Ball.MotionType.Sliding:
                    {
                        double3 velPoc0 = b0.Velocity + math.cross(b0.AngularVelocity, -UnitY * b0.Radius);

                        //update ball state
                        double3 velPoc0Unit = math.normalizesafe(velPoc0, double3.zero);
                        double st = math.min(dt, spinTime);
                        double spinDirection = math.sign(b0.AngularVelocity.y);
                        double factor = 5 / (2.0 * b0.Radius) * _constants.Gravity;
                        double3 angVelXZ = _constants.SlidingFriction * dt * math.cross(UnitY, velPoc0Unit);
                        double3 angVelY = _constants.SpinningFriction * st * spinDirection * UnitY;

                        SetPosition(ref b1, b0.Position + b0.Velocity * dt - 0.5 * _constants.SlidingFriction * _constants.Gravity * dt * dt * velPoc0Unit);
                        SetVelocity(ref b1, b0.Velocity - _constants.SlidingFriction * _constants.Gravity * dt * velPoc0Unit);
                        SetAngularVelocity(ref b1, b0.AngularVelocity + factor * (angVelXZ - angVelY));
                        break;
                    }
                case Ball.MotionType.Rolling:
                    {
                        double3 velUnit = math.normalizesafe(b0.Velocity, double3.zero);
                        double3 vel = b0.Velocity - _constants.RollingFriction * _constants.Gravity * dt * velUnit;
                        double st = math.min(dt, spinTime);
                        double spinDirection = math.sign(b0.AngularVelocity.y);
                        double factor = 5 / (2.0 * b0.Radius) * _constants.Gravity * _constants.SpinningFriction * spinDirection;
                        double3 angVelXZ = math.cross(UnitY, vel) / b0.Radius;
                        double3 angVelY = (b0.AngularVelocity.y - factor * st) * UnitY;

                        SetPosition(ref b1, b0.Position + b0.Velocity * dt - 0.5 * _constants.RollingFriction * _constants.Gravity * dt * dt * velUnit);
                        SetVelocity(ref b1, vel);
                        SetAngularVelocity(ref b1, angVelXZ + angVelY);
                        break;
                    }
                case Ball.MotionType.Airborne:
                    {
                        SetHeight(ref b1, b0.Position.y + b0.Velocity.y * dt - 0.5 * _constants.Gravity * dt * dt);
                        SetPosition(ref b1, b0.Position.xz + b0.Velocity.xz * dt);
                        SetVelocity(ref b1, new double3(b0.Velocity.x, 0, b0.Velocity.z) + UnitY * (b0.Velocity.y - _constants.Gravity * dt));
                    }
                    break;
                case Ball.MotionType.Stationary:
                    {
                        SetAngularVelocity(ref b1, 0);
                        SetVelocity(ref b1, 0);
                    }
                    break;
                case Ball.MotionType.StationarySpin:
                    {
                        double spinDirection = math.sign(b0.AngularVelocity.y);
                        double st = math.min(dt, spinTime);
                        double factor = 5 / (2.0 * b0.Radius) * _constants.Gravity * _constants.SpinningFriction * spinDirection;
                        SetAngularVelocity(ref b1, b0.AngularVelocity - factor * st * UnitY);
                    }
                    break;
            }
        }


        /// <summary>
        /// Simple algebraic bilinear collision law for special case when the mass matrix is diagonal. This bilinear law is ideal for collisions of spheres and matches experimental data reasonably well.
        /// First axis is in normal direction, second axis is tangent direction and third axis is perpendicular to the previous two axis.
        /// Orientation is such that the v dot n < 0 and v dot t < 0.
        /// Therefore l[0] is measure along normal direction, l[1] is measure along tangential direction...
        /// </summary>
        /// <param name="v">Relative velocity of the bodies at the point of impact.</param>
        /// <param name="n">Impact normal oriented such that dot product <v,n> is negative</param>
        /// <param name="c">Impact parameters.</param>
        /// <param name="l">Diagonal mass matrix elements M = W^(-1)</param>
        private double3 BilinearCollisionLaw(in double3 v, in double3 n, in PhysicsImpactParameters c, in double3 l)
        {
            double vn = math.dot(v, n);
            double3 t = v - vn * n;
            double3 tn = math.normalizesafe(-t, double3.zero);
            double tLen = math.length(t);

            double i0 = (1 + c.NormalRestitution) * vn * l[0];
            double i1 = (1 + c.NormalRestitution) * c.Friction * vn * l[1];
            double i2 = (1 + c.TangentialRestitution) * tLen * l[2];

            double3 I_t = math.min(i1, i2) * tn;
            double3 I_n = i0 * n;

            return -(I_n + I_t);
        }

        private void ResolveBallVertexImpact(ref Ball b, Cushion cushion, int vertex)
        {
            double3 p0 = cushion[vertex];

            if (_constants.Planar)
                p0 += UnitY * cushion.Height;

            double3 cp = p0;
            double3 normal = math.normalize(b.Position - cp);
            double3 r1 = -normal * b.Radius;

            double pen = math.abs(math.length(cp - b.Position) - b.Radius);
            double3 delta = normal * (pen + _constants.CollisionAppliedDisplacement);
            SetPosition(ref b, b.Position + delta);

            if (math.dot(b.Velocity, normal) > 0)
                return;

            double3 v = b.Velocity + math.cross(b.AngularVelocity, r1);
            double m = (2 / 7.0) * b.Mass;
            double3 l = new double3(b.Mass, m, m);
            double3 impulse = BilinearCollisionLaw(v, normal, _constants.BallToCushionImpactParameters, l);
            SetVelocity(ref b, b.Velocity + b.InverseMass * impulse);
            SetAngularVelocity(ref b, b.AngularVelocity + b.InverseInertia * math.cross(r1, impulse));
        }

        /// <summary>Updates ball velocity and position due to impact with the cushion. </summary>
        private void ResolveBallCushionImpact(ref Ball b, Cushion cushion)
        {
            double3 p0 = cushion.P0;
            if (_constants.Planar)
                p0 += UnitY * cushion.Height;

            double3 cp = GetClosestPointOnLineSegment(b.Position, p0, cushion.Dir, cushion.Distance);
            double3 normal = cushion.Normal;

            //normal is oriented to point from the cushion line towards the ball
            if (math.dot(normal, cp - b.Position) > 0)
                normal *= -1;

            double pen = math.abs(math.length(cp - b.Position) - b.Radius);
            double3 delta = normal * (pen + _constants.CollisionAppliedDisplacement);
            SetPosition(ref b, b.Position + delta);
            if (math.dot(b.Velocity, normal) > 0)
                return;

            double3 r1 = -normal * b.Radius;
            double3 v = b.Velocity + math.cross(b.AngularVelocity, r1);
            double m = (2 / 7.0) * b.Mass;
            double3 l = new double3(b.Mass, m, m);
            double3 impulse = BilinearCollisionLaw(v, normal, _constants.BallToCushionImpactParameters, l);

            SetVelocity(ref b, b.Velocity + b.InverseMass * impulse);
            SetAngularVelocity(ref b, b.AngularVelocity + b.InverseInertia * math.cross(r1, impulse));
        }

        /// <summary>Updates balls velocities and positions based on relative impact velocity. </summary>
        private void ResolveBallToBallImpact(ref Ball b1, ref Ball b2)
        {
            double3 centreLine = b1.Position - b2.Position;
            double3 normal = math.normalize(centreLine);
            double3 cp = b2.Position + normal * b2.Radius;

            //relative position of the contact point with respect to a ball
            double3 r1 = cp - b1.Position;
            double3 r2 = cp - b2.Position;

            //world relative velocity 
            double3 v = b1.Velocity + math.cross(b1.AngularVelocity, r1) - b2.Velocity -
                        math.cross(b2.AngularVelocity, r2);

            //impulse would not be able to resolve these cases:
            // 1. relative velocity is positive -> balls are separating
            // 2. impulse is too small
            // but large penetration push could result in more invalid collisions
            double pen = math.abs(b1.Radius + b2.Radius - math.length(centreLine)) + _constants.CollisionAppliedDisplacement;
            double3 delta = normal * pen * 0.5;
            SetPosition(ref b1, b1.Position + delta);
            SetPosition(ref b2, b2.Position - delta);

            //check if balls are moving towards each other
            double vn = math.dot(normal, v);
            if (vn >= 0)
                return;

            //local mass matrix
            double m = 1 / (b1.InverseMass + b2.InverseMass);
            double mt = m * (2 / 7.0);
            double3 l = new double3(m, mt, mt);
            double3 impulse = BilinearCollisionLaw(v, normal, _constants.BallToBallImpactParameters, l);

            SetVelocity(ref b1, b1.Velocity + b1.InverseMass * (impulse));
            SetVelocity(ref b2, b2.Velocity - b2.InverseMass * (impulse));

            SetAngularVelocity(ref b1, b1.AngularVelocity + b1.InverseInertia * math.cross(r1, impulse));
            SetAngularVelocity(ref b2, b2.AngularVelocity - b2.InverseInertia * math.cross(r2, impulse));

        }

        /// <summary>Updates ball velocity and height due to impact with the slate. </summary>
        private void ResolveBallSurfaceImpact(ref Ball b)
        {
            double3 r1 = -UnitY * b.Radius;
            double3 v = b.Velocity + math.cross(b.AngularVelocity, r1);
            double m = (2 / 7.0) * b.Mass;
            double3 l = new double3(b.Mass, m, m);
            double3 impulse = BilinearCollisionLaw(v, -UnitY, _constants.BallToSlateImpactParameters, l);

            SetHeight(ref b, 0f);
            SetVelocity(ref b, b.Velocity + b.InverseMass * impulse);
            SetAngularVelocity(ref b, b.AngularVelocity + b.InverseInertia * math.cross(r1, impulse));
        }

        /// <summary>Converts vector to skew symmetric matrix A such that A * r = v x r for any vector r. </summary>
        private double3x3 CrossProductMatrix(double3 v)
        {
            double3x3 A = new double3x3(
                0, -v.z, v.y,
                v.z, 0, -v.x,
                -v.y, v.x, 0);
            return A;
        }

        /// <summary>
        /// Computer the impulse predicted by the algebraic model proposed by Chatterjee and Ruina.
        /// </summary>
        /// <param name="M">The inertia mass matrix.</param>
        /// <param name="v">The relative velocity.</param>
        /// <param name="n">The normal such that relative velocity is in the opposite direction.</param>
        /// <param name="mu">The friction coefficient.</param>
        /// <param name="e">The restitution in the normal direction.</param>
        /// <param name="e_t">The tangential restitution.</param>
        /// <returns>Impulse that satisfies energy and friction constraints.</returns>
        private double3 SimpleAlgebraicImpulse(double3x3 M, double3 v, double3 n, double mu, double e, double e_t)
        {
            //perfect plastic frictionless collision(mu = 0, e = 0, e_t = -1)
            double3 P_I = -n * math.dot(n, v) / (math.dot(n, math.mul(math.inverse(M), n)));
            double3 P_II = -math.mul(M, v);

            //candidate impulse that satisfy the energy constraint and non inter-penetration criteria
            double3 P_hat = (1 + e) * P_I + (1 + e_t) * (P_II - P_I);
            double k;

            //in case impulse is out of the friction cone, re-project it
            if (math.length(P_hat - math.dot(n, P_hat) * n) > mu * math.dot(n, P_hat))
            {
                k = mu * (1 + e) * math.dot(n, P_I) / (math.length(P_II - n * math.dot(n, P_II)) - (mu * math.dot(n, P_II - P_I)));
            }
            else
            {
                k = 1 + e_t;
            }

            return (1 + e) * P_I + k * (P_II - P_I);
        }

        /// <summary>
        /// Calculates the inverse inertia matrix. Product of this matrix with impulses generates change in relative velocity: v = WI
        /// </summary>
        /// <param name="p1">The position of the body 1 in world frame.</param>
        /// <param name="p2">The position of the body 2 in world frame.</param>
        /// <param name="cp">The contact position in the world frame.</param>
        /// <param name="R1">The rotation from world frame to local frame of body 1.</param>
        /// <param name="R2">The rotation from world frame to local frame of body 2.</param>
        /// <param name="im1">The inverse mass for body 1.</param>
        /// <param name="im2">The inverse mass for body 2.</param>
        /// <param name="iQ1">The inverse inertia matrix in local frame for body 1.</param>
        /// <param name="iQ2">The inverse inertia matrix in local frame for body 2.</param>
        /// <returns>Inverse inertia matrix</returns>
        private double3x3 CalculateInverseInertiaMatrix(double3 p1, double3 p2, double3 cp, double3x3 R1, double3x3 R2, double im1, double im2, double3x3 iQ1, double3x3 iQ2)
        {
            double3 r1 = cp - p1;
            double3 r2 = cp - p2;

            double3x3 P1 = CrossProductMatrix(r1);
            double3x3 P2 = CrossProductMatrix(r2);

            double3x3 S1 = math.mul(P1, math.mul(R1, math.mul(iQ1, math.mul(math.transpose(R1), P1))));
            double3x3 S2 = math.mul(P2, math.mul(R2, math.mul(iQ2, math.mul(math.transpose(R2), P2))));
            double3x3 S = S1 + S2;

            double3x3 W = (im1 + im2) * double3x3.identity - S;
            return W;
        }

        private static double3x3 EulerZXY(double3 xyz)
        {
            double3 s = math.sin(xyz);
            double3 c = math.cos(xyz);
            return new double3x3(
                c.y * c.z + s.x * s.y * s.z, c.z * s.x * s.y - c.y * s.z, c.x * s.y,
                c.x * s.z, c.x * c.z, -s.x,
                c.y * s.x * s.z - c.z * s.y, c.y * c.z * s.x + s.y * s.z, c.x * c.y
                );
        }

        /// <summary>Updates ball velocity due to impact with the stick.</summary>
        /// <param name="b">The ball data.</param>
        /// <param name="cue">The cue stick data.</param>
        /// <param name="vel">Velocity of the cue stick, positive.</param>
        /// <param name="jaw">Rotation about Y axis between cue frame from world frame in degrees.</param>
        /// <param name="pitch">Rotation about X axis between cue frame from world frame in degrees.</param>
        /// <param name="offset">Offset of the strike in the cue stick frame, normalized.</param>
        /// <param name="c">Impact parameters between cue and ball</param>
        /// <returns>False if miscue occurs, true otherwise</returns>
        public bool ResolveBallCueImpact(ref Ball b, in Cue cue, in double vel, in double jaw, in double pitch, in double2 offset)
        {
            //TODO: ASSERT
            PhysicsImpactParameters c = cue.ImpactParameters;
            double ro1 = b.Radius;
            double ro2 = cue.Length / 2.0;
            double mu = c.Friction;
            double l = (mu * b.Radius) / (math.sqrt(1 + mu * mu));

            //contact point in the cue stick frame(z axis is aligned with stick axis and is oriented from the ball)
            double3 cp_stick = new double3(offset.x, offset.y, 0) * b.Radius;

            //check for miscue
            if (math.lengthsq(cp_stick) >= math.min(l * l, 1))
                return false;

            //update z component of the position for the contact point in the stick frame
            cp_stick.z = -math.sqrt(ro1 * ro1 - math.lengthsq(cp_stick));

            //inverse angular inertia matrix
            double3x3 iQ1 = double3x3.identity * b.InverseInertia;
            double3x3 iQ2 = double3x3.identity * cue.InverseInertia;

            //stick frame from world frame
            double3x3 Rws = EulerZXY(new double3(math.radians(pitch), math.radians(jaw), 0));

            //contact point in the world frame
            double3 cp_world = math.mul(Rws, cp_stick);

            //calculate inverse inertia matrix
            double3 p1 = 0;
            double3 p2 = cp_world + math.mul(Rws, new double3(0, 0, -ro2));
            double3x3 W = CalculateInverseInertiaMatrix(p1, p2, cp_world, double3x3.identity, Rws, b.InverseMass, cue.InverseMass, iQ1, iQ2);
            double3x3 M = math.inverse(W);
            double3 cueVel = Rws.c2 * (float)vel;
            double3 normal = -Rws.c2;
            double3 impulse = SimpleAlgebraicImpulse(M, cueVel, normal, mu, c.NormalRestitution, c.TangentialRestitution);

            SetVelocity(ref b, b.Velocity - b.InverseMass * impulse);
            SetAngularVelocity(ref b, b.AngularVelocity - b.InverseInertia * math.cross(cp_world, impulse));

            return true;
        }

        /// <summary> Updates ball acceleration and velocity coefficients. </summary>
        public void CacheCoeff(ref Ball ball)
        {
            switch (ball.Motion)
            {
                case Ball.MotionType.StationarySpin:
                case Ball.MotionType.Stationary:
                    {
                        ball.VelCoeff = 0;
                        ball.AccCoeff = 0;
                        return;
                    }
                    ;
                case Ball.MotionType.Rolling:
                    {
                        double3 velUnit = math.normalizesafe(ball.Velocity, double3.zero);
                        ball.VelCoeff = ball.Velocity;
                        ball.AccCoeff = -0.5 * _constants.RollingFriction * _constants.Gravity * velUnit;
                        return;
                    }
                case Ball.MotionType.Sliding:
                    {
                        double3 velPoc0 = ball.Velocity + math.cross(ball.AngularVelocity, -UnitY * ball.Radius);
                        double3 velPoc0Unit = math.normalizesafe(velPoc0, double3.zero);
                        ball.VelCoeff = ball.Velocity;
                        ball.AccCoeff = -0.5 * _constants.SlidingFriction * _constants.Gravity * velPoc0Unit;
                        return;
                    }
                case Ball.MotionType.Airborne:
                    {
                        ball.VelCoeff = ball.Velocity;
                        ball.AccCoeff = -UnitY * 0.5 * _constants.Gravity;
                        return;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}