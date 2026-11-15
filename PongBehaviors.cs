using LibGFX.Core;
using LibGFX.Core.GameElements;
using LibGFX.Graphics;
using LibGFX.Physics;
using LibGFX.Physics.Behaviors3D;
using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace NeonPong3D;

/// <summary>Moves a paddle and synchronizes its kinematic transform with Bullet.</summary>
internal sealed class PaddleBehavior : IGameBehavior
{
    private readonly LibGFX.Core.Window _window;
    private readonly Func<float> _getTargetY;
    private readonly BoxRigidBody _body;
    private readonly float _speed;
    private readonly float _limit;
    private readonly float _errorProneness;
    private readonly bool _isPlayerControlled;
    private readonly Random _random = new();

    private GameElement _element = null!;
    private float _errorTimer;
    private float _aimError;

    public PaddleBehavior(
        LibGFX.Core.Window window,
        BoxRigidBody body,
        Func<float> getTargetY,
        float speed,
        float limit,
        bool isPlayerControlled,
        float errorProneness = 0f)
    {
        _window = window;
        _body = body;
        _getTargetY = getTargetY;
        _speed = speed;
        _limit = limit;
        _isPlayerControlled = isPlayerControlled;
        _errorProneness = Math.Clamp(errorProneness, 0f, 1f);
    }

    public void SetElement(GameElement element) => _element = element;
    public GameElement GetElement() => _element;

    public void OnUpdate(BaseScene scene, float dt)
    {
        var input = _isPlayerControlled
            ? ReadPlayerInput()
            : ReadCpuInput(dt);

        var position = _element.Transform.Position;
        var y = Math.Clamp(position.Y + input * _speed * dt, -_limit, _limit);
        _element.Transform.Position = new Vector3(position.X, y, position.Z);

        // The paddle is controlled by the game, so copy its transform into Bullet each frame.
        _body.Sync();
    }

    private float ReadPlayerInput()
    {
        var up = _window.IsKeyDown(Keys.W) ? 1f : 0f;
        var down = _window.IsKeyDown(Keys.S) ? 1f : 0f;
        return up - down;
    }

    private float ReadCpuInput(float dt)
    {
        UpdateAimError(dt);
        return GetNormalizedMovement(_getTargetY() + _aimError, dt);
    }

    private void UpdateAimError(float dt)
    {
        _errorTimer -= dt;
        if (_errorTimer > 0f)
            return;

        var randomDirection = (float)_random.NextDouble() * 2f - 1f;
        _aimError = randomDirection * PongTuning.CpuMaximumAimError * _errorProneness;
        _errorTimer = PongTuning.CpuMinimumErrorDuration
            + (float)_random.NextDouble() * PongTuning.CpuErrorDurationVariation;
    }

    private float GetNormalizedMovement(float targetY, float dt)
    {
        var maxStep = _speed * dt;
        var distance = targetY - _element.Transform.Position.Y;
        return Math.Clamp(distance, -maxStep, maxStep) / MathF.Max(maxStep, 0.0001f);
    }

    // The engine behavior interface requires hooks that this movement behavior does not use.
    public void OnInit(BaseScene scene, Viewport viewport, IRenderDevice renderer) { }
    public void OnShadowPass(BaseScene scene, Viewport viewport, IRenderDevice renderer) { }
    public void OnRender(BaseScene scene, Viewport viewport, IRenderDevice renderer, Camera camera) { }
    public void OnDispose(BaseScene scene, IRenderDevice renderer) { }
    public void OnCollide(Collision collision) { }
}

/// <summary>Applies launch and paddle-hit impulses to the Bullet ball body.</summary>
internal sealed class PongBallBehavior : IGameBehavior
{
    private readonly SphereRigidBody _body;
    private readonly Random _random = new();

    private GameElement _element = null!;
    private string? _activePaddleContact;
    private float _incomingXVelocity;

    public PongBallBehavior(SphereRigidBody body) => _body = body;

    public void SetElement(GameElement element) => _element = element;
    public GameElement GetElement() => _element;
    public Vector3 Velocity => _body.LinearVelocity;

    public void Launch(float direction)
    {
        _body.RigidBody.ClearForces();
        _body.RigidBody.Activate(true);
        _body.LinearVelocity = Vector3.Zero;

        var impulse = new Vector3(
            direction * PongTuning.BallServeSpeed,
            RandomRange(-PongTuning.BallServeVerticalRange, PongTuning.BallServeVerticalRange),
            0f);

        // The ball mass is one, so this impulse gives it the configured launch velocity.
        _body.ApplyCentralImpulse(impulse * PongTuning.BallMass);
    }

    public void Stop()
    {
        _body.RigidBody.ClearForces();
        _body.LinearVelocity = Vector3.Zero;
    }

    public void ResetPosition()
    {
        _element.Transform.Position = Vector3.Zero;
        _body.Sync();
        Stop();
        _activePaddleContact = null;
    }

    public void OnUpdate(BaseScene scene, float dt)
    {
        _incomingXVelocity = _body.LinearVelocity.X;
        ClearPaddleContactWhenSeparated();
    }

    private void ClearPaddleContactWhenSeparated()
    {
        var ballX = _element.Transform.Position.X;
        var leftContactEnded = _activePaddleContact == "PlayerPaddle"
            && MathF.Abs(ballX + PongTuning.PaddleX) > PongTuning.PaddleContactReleaseDistance;
        var rightContactEnded = _activePaddleContact == "CPU Paddle"
            && MathF.Abs(ballX - PongTuning.PaddleX) > PongTuning.PaddleContactReleaseDistance;

        if (leftContactEnded || rightContactEnded)
            _activePaddleContact = null;
    }

    public void OnCollide(Collision collision)
    {
        var paddleName = collision.GameElement.Name;
        if (paddleName is not ("PlayerPaddle" or "CPU Paddle"))
            return;

        if (_activePaddleContact == paddleName)
            return;

        var paddleSide = paddleName == "PlayerPaddle" ? -1f : 1f;
        if (_incomingXVelocity * paddleSide <= 0f)
            return;

        ApplyPaddleHit(collision.GameElement, paddleName, paddleSide);
    }

    private void ApplyPaddleHit(GameElement paddle, string paddleName, float paddleSide)
    {
        var verticalOffset = (_element.Transform.Position.Y - paddle.Transform.Position.Y)
            / (PongTuning.PaddleHalfHeight + PongTuning.BallRadius);
        verticalOffset = Math.Clamp(verticalOffset, -1f, 1f);

        var horizontalSpeed = MathF.Min(
            MathF.Abs(_incomingXVelocity) * PongTuning.PaddleSpeedMultiplier + PongTuning.PaddleSpeedIncrease,
            PongTuning.BallMaxPaddleSpeed);
        var verticalSpeed = verticalOffset * PongTuning.PaddleVerticalBounceSpeed
            + RandomRange(-PongTuning.PaddleBounceVariation, PongTuning.PaddleBounceVariation);
        verticalSpeed = EnsureMinimumVerticalSpeed(verticalSpeed);

        var targetVelocity = new Vector3(-paddleSide * horizontalSpeed, verticalSpeed, 0f);
        _body.LinearVelocity = Vector3.Zero;
        _body.ApplyCentralImpulse(targetVelocity * PongTuning.BallMass);
        _body.RigidBody.Activate(true);
        _activePaddleContact = paddleName;
    }

    private float EnsureMinimumVerticalSpeed(float verticalSpeed)
    {
        if (MathF.Abs(verticalSpeed) >= PongTuning.MinimumVerticalSpeed)
            return verticalSpeed;

        var direction = verticalSpeed == 0f
            ? (_random.Next(2) == 0 ? -1f : 1f)
            : MathF.Sign(verticalSpeed);
        return direction * PongTuning.MinimumVerticalSpeed;
    }

    private float RandomRange(float min, float max)
        => min + (float)_random.NextDouble() * (max - min);

    // The engine behavior interface requires hooks that this ball behavior does not use.
    public void OnInit(BaseScene scene, Viewport viewport, IRenderDevice renderer) { }
    public void OnShadowPass(BaseScene scene, Viewport viewport, IRenderDevice renderer) { }
    public void OnRender(BaseScene scene, Viewport viewport, IRenderDevice renderer, Camera camera) { }
    public void OnDispose(BaseScene scene, IRenderDevice renderer) { }
}

/// <summary>Shared gameplay values used by the game loop and its behaviors.</summary>
internal static class PongTuning
{
    public const float HalfWidth = 10f;
    public const float HalfHeight = 6f;
    public const float PaddleHalfHeight = 1.15f;
    public const float PaddleX = 8.7f;
    public const float BallRadius = 0.32f;

    public const float PlayerPaddleSpeed = 8.5f;
    public const float CpuPaddleSpeed = 7f;
    public const float CpuMaximumAimError = 2.6f;
    public const float CpuMinimumErrorDuration = 0.45f;
    public const float CpuErrorDurationVariation = 0.5f;
    public const float CpuPredictionTime = 0.18f;
    public const float CpuTargetLimit = 4.75f;

    public const float BallMass = 1f;
    public const float BallServeSpeed = 8f;
    public const float BallServeVerticalRange = 2.8f;
    public const float BallMaxPaddleSpeed = 15.5f;
    public const float BallRestitution = 0.98f;
    public const float RailRestitution = 0.98f;
    public const float PaddleSpeedMultiplier = 1.045f;
    public const float PaddleSpeedIncrease = 0.25f;
    public const float PaddleVerticalBounceSpeed = 7.2f;
    public const float PaddleBounceVariation = 0.225f;
    public const float MinimumVerticalSpeed = 1.4f;
    public const float PaddleContactReleaseDistance = 0.8f;

    public const float MaximumFrameDelta = 0.04f;
    public const float InitialServeDelay = 1f;
    public const float PointPause = 0.85f;
    public const int ScoreToWin = 7;
}
