using LibGFX.Assets;
using LibGFX.Core;
using LibGFX.Core.GameElements;
using LibGFX.Graphics;
using LibGFX.Graphics.Enviroment;
using LibGFX.Graphics.Lights;
using LibGFX.Graphics.Materials;
using LibGFX.Graphics.Primitives;
using LibGFX.Graphics.RenderTarget;
using LibGFX.Graphics.Renderer.OpenGL;
using LibGFX.Physics;
using LibGFX.Physics.Behaviors3D;
using LibGFX.UI;
using OpenTK.Mathematics;
using Label = LibGFX.UI.Label;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace NeonPong3D;

/// <summary>Owns Pong's scene, input, scoring, and render loop.</summary>
internal sealed class PongGame : Game
{
    private readonly Random _random = new();

    private Scene3D _scene = null!;
    private PerspectiveCamera _camera = null!;
    private PhysicsHandler3D _physics = null!;
    private Canvas2D _hud = null!;

    private Primitive _player = null!;
    private Primitive _opponent = null!;
    private Primitive _ball = null!;
    private PongBallBehavior _ballBehavior = null!;
    private Label _scoreLabel = null!;
    private Label _messageLabel = null!;

    private float _aiTargetY;
    private float _serveTimer = 1f;
    private int _playerScore;
    private int _opponentScore;
    private bool _roundOver;

    /// <summary>Controls CPU aim error: 0 is precise and 1 is highly unreliable.</summary>
    public float AiErrorProneness { get; set; } = 0.65f;

    public override void LoadContent(AssetManager assets)
    {
        assets.Add("cube", Cube.GetMesh());
        assets.Add("sphere", Sphere.GetMesh());

        assets.Add("white", new SGMaterial("White", new Vector4(0.83f, 0.9f, 1f, 1f)));
        assets.Add("cyan", new SGMaterial("Cyan", new Vector4(0.08f, 0.92f, 1f, 1f)));
        assets.Add("pink", new SGMaterial("Pink", new Vector4(1f, 0.16f, 0.48f, 1f)));
        assets.Add("ball", new SGMaterial("Ball", new Vector4(1f, 0.93f, 0.55f, 1f)));
        assets.Add("floor", new SGMaterial("Floor", new Vector4(0.025f, 0.045f, 0.1f, 1f)));
    }

    public override void Initialize(IRenderDevice renderer)
    {
        CreateCamera();
        CreateScene();
        CreateArena();
        CreatePaddles();
        CreateBall();
        CreateHud(renderer);

        _scene.Init(Viewport, renderer);
        _hud.Init(renderer);
        Serve();
    }

    private void CreateCamera()
    {
        _camera = new PerspectiveCamera(
            new Vector3(0f, 1f, 19f),
            new Vector2(Viewport.Width, Viewport.Height));
        _camera.SetAsCurrent();
    }

    private void CreateScene()
    {
        _physics = new PhysicsHandler3D(Vector3.Zero);
        _scene = new Scene3D
        {
            Enviroment = new ProceduralSky(),
            DirectionalLight = new DirectionalLight3D(
                new Vector3(-0.3f, 0.7f, 1f), Vector4.One, 1.2f),
            PhysicsHandler = _physics
        };
    }

    private void CreateArena()
    {
        var cube = GetAsset<Mesh>("cube");
        var floor = GetAsset<SGMaterial>("floor");
        var cyan = GetAsset<SGMaterial>("cyan");
        var white = GetAsset<SGMaterial>("white");

        // Keep the backdrop behind the game pieces; a solid cube would occlude them.
        AddBox("Arena", cube, floor, new Vector3(0f, 0f, -0.55f), new Vector3(20.5f, 12.5f, 0.12f));
        AddBox("PlayerGoal", cube, floor, new Vector3(-10.4f, 0f, 0f), new Vector3(0.35f, 12.5f, 0.6f));
        AddBox("OpponentGoal", cube, floor, new Vector3(10.4f, 0f, 0f), new Vector3(0.35f, 12.5f, 0.6f));

        CreateCenterMarkings(cube, white);
        CreateRail(cube, cyan, "TopRail", new Vector3(0f, 6.08f, 0f));
        CreateRail(cube, cyan, "BottomRail", new Vector3(0f, -6.08f, 0f));
    }

    private void CreateCenterMarkings(Mesh cube, SGMaterial material)
    {
        for (var y = -5.4f; y <= 5.4f; y += 0.9f)
        {
            AddBox(
                "CenterDash", cube, material,
                new Vector3(0f, y, 0.08f),
                new Vector3(0.075f, 0.42f, 0.08f));
        }
    }

    private void CreateRail(Mesh cube, SGMaterial material, string name, Vector3 position)
    {
        var rail = AddStaticBox(
            name, cube, material, position,
            new Vector3(20f, 0.12f, 0.5f));

        var body = rail.GetBehavior<BoxRigidBody>();
        body.Restitution = PongTuning.RailRestitution;
        body.Friction = 0f;
    }

    private void CreatePaddles()
    {
        var cube = GetAsset<Mesh>("cube");
        var playerMaterial = GetAsset<SGMaterial>("cyan");
        var cpuMaterial = GetAsset<SGMaterial>("pink");
        var paddleScale = new Vector3(0.38f, 2.3f, 0.45f);

        _player = AddBox(
            "PlayerPaddle", cube, playerMaterial,
            new Vector3(-PongTuning.PaddleX, 0f, 0.25f), paddleScale);
        _opponent = AddBox(
            "CPU Paddle", cube, cpuMaterial,
            new Vector3(PongTuning.PaddleX, 0f, 0.25f), paddleScale);

        AddPaddlePhysics(_player, isPlayer: true);
        AddPaddlePhysics(_opponent, isPlayer: false);
    }

    private void AddPaddlePhysics(Primitive paddle, bool isPlayer)
    {
        var body = paddle.AddBehavior(new BoxRigidBody(_physics));
        body.CreateRigidBody(0f);

        var speed = isPlayer ? PongTuning.PlayerPaddleSpeed : PongTuning.CpuPaddleSpeed;
        paddle.AddBehavior(new PaddleBehavior(
            Window,
            body,
            () => isPlayer ? paddle.Transform.Position.Y : _aiTargetY,
            speed,
            PongTuning.HalfHeight - PongTuning.PaddleHalfHeight,
            isPlayer,
            AiErrorProneness));
    }

    private void CreateBall()
    {
        _ball = AddBox(
            "Ball",
            GetAsset<Mesh>("sphere"),
            GetAsset<SGMaterial>("ball"),
            Vector3.Zero,
            new Vector3(PongTuning.BallRadius * 2f));

        var body = _ball.AddBehavior(new SphereRigidBody(_physics));
        body.CreateRigidBody(PongTuning.BallMass, PongTuning.BallRadius);
        body.Restitution = PongTuning.BallRestitution;
        body.Friction = 0f;
        body.SetLinearFactor(new Vector3(1f, 1f, 0f));
        body.SetAngularFactor(Vector3.Zero);
        body.EnableContinuousCollisionDetection(0.2f, 0.25f);

        _ballBehavior = _ball.AddBehavior(new PongBallBehavior(body));
    }

    private void CreateHud(IRenderDevice renderer)
    {
        var font = renderer.LoadFont(Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts", "IBMPlexMono-Regular.ttf"), 64);
        _hud = new Canvas2D(0, 0, Viewport.Width, Viewport.Height);

        _scoreLabel = new Label(
            "Score", "00     :     00",
            new Vector2(0, -54), new Vector2(460, 60), font, 0.68f);
        _messageLabel = new Label(
            "Status", "GET READY",
            new Vector2(0, 270), new Vector2(380, 34), font, 0.29f);

        _hud.AddControl(_scoreLabel);
        _hud.AddControl(new Label(
            "Controls", "W / S MOVE  |  SPACE RESET BALL  |  FIRST TO 7  |  R NEW MATCH",
            new Vector2(0, 330), new Vector2(760, 30), font, 0.19f));
        _hud.AddControl(_messageLabel);
    }

    private T GetAsset<T>(string name) where T : class => AssetManager.Get<T>(name)!;

    private Primitive AddBox(string name, Mesh mesh, SGMaterial material, Vector3 position, Vector3 scale)
    {
        var element = new Primitive(name, mesh, material);
        element.Transform.Position = position;
        element.Transform.Scale = scale;
        _scene.AddGameElement(element);
        return element;
    }

    private Primitive AddStaticBox(string name, Mesh mesh, SGMaterial material, Vector3 position, Vector3 scale)
    {
        var element = AddBox(name, mesh, material, position, scale);
        element.AddBehavior(new BoxRigidBody(_physics)).CreateRigidBody(0f);
        return element;
    }

    public override void OnStart()
    {
        // The scene is ready before the first update begins.
    }

    public override void Update(float deltaTime)
    {
        var dt = MathF.Min(deltaTime, PongTuning.MaximumFrameDelta);

        if (Window.IsKeyDown(Keys.Escape))
        {
            Window.Close();
            return;
        }

        if (_roundOver && Window.IsKeyPressed(Keys.R))
            ResetMatch();

        if (!_roundOver && Window.IsKeyPressed(Keys.Space))
            ResetBall();

        if (!_roundOver)
            UpdateRally(dt);

        // Behaviors first sync controlled paddles; Bullet then advances the rigid bodies.
        _scene.Update(dt);
        _scene.UpdatePhysics(dt);
        UpdateHud();
    }

    private void UpdateRally(float dt)
    {
        if (_serveTimer > 0f)
        {
            _serveTimer -= dt;
            if (_serveTimer <= 0f)
                LaunchBall();
            return;
        }

        UpdateBallTarget();
        CheckForScore();
    }

    private void LaunchBall()
    {
        var direction = _random.Next(2) == 0 ? -1f : 1f;
        _ballBehavior.Launch(direction);
        _messageLabel.Text = string.Empty;
    }

    private void UpdateBallTarget()
    {
        var ballPosition = _ball.Transform.Position;
        var ballVelocity = _ballBehavior.Velocity;
        _aiTargetY = Math.Clamp(
            ballPosition.Y + ballVelocity.Y * PongTuning.CpuPredictionTime,
            -PongTuning.CpuTargetLimit,
            PongTuning.CpuTargetLimit);
    }

    private void CheckForScore()
    {
        var ballX = _ball.Transform.Position.X;

        if (ballX > PongTuning.HalfWidth + PongTuning.BallRadius)
        {
            _playerScore++;
            HandlePointScored();
        }
        else if (ballX < -PongTuning.HalfWidth - PongTuning.BallRadius)
        {
            _opponentScore++;
            HandlePointScored();
        }
    }

    private void HandlePointScored()
    {
        _ballBehavior.ResetPosition();

        if (_playerScore >= PongTuning.ScoreToWin || _opponentScore >= PongTuning.ScoreToWin)
        {
            _roundOver = true;
            _messageLabel.Text = _playerScore > _opponentScore
                ? "YOU WIN  //  PRESS R"
                : "CPU WINS  //  PRESS R";
            return;
        }

        _messageLabel.Text = "POINT  //  NEXT SERVE";
        _serveTimer = PongTuning.PointPause;
        _ballBehavior.Stop();
    }

    private void Serve()
    {
        _serveTimer = PongTuning.InitialServeDelay;
        _ballBehavior.ResetPosition();
        _aiTargetY = 0f;
    }

    private void ResetBall()
    {
        _ballBehavior.ResetPosition();
        _serveTimer = PongTuning.PointPause;
        _aiTargetY = 0f;
        _messageLabel.Text = "BALL RESET  //  NEXT SERVE";
    }

    private void ResetMatch()
    {
        _playerScore = 0;
        _opponentScore = 0;
        _roundOver = false;

        _player.Transform.Position = new Vector3(-PongTuning.PaddleX, 0f, 0.25f);
        _opponent.Transform.Position = new Vector3(PongTuning.PaddleX, 0f, 0.25f);
        _messageLabel.Text = "NEW MATCH  //  GET READY";
        Serve();
    }

    private void UpdateHud()
    {
        _hud.Update(Window);
        _scoreLabel.Text = $"{_playerScore:00}     :     {_opponentScore:00}";
    }

    public override void Render()
    {
        _scene.BuildShadowMaps(Viewport, RenderDevice, _camera);
        _scene.Render(Viewport, RenderDevice, _camera);
        RenderDevice.DrawRenderTarget((MSAARenderTarget2D)_scene.RenderTarget, GLRenderer.Backbuffer);

        _hud.Render(Viewport, RenderDevice);
        RenderDevice.DrawRenderTarget((RenderTarget2D)_hud.RenderTarget, GLRenderer.Backbuffer);
    }

    public override void OnFrameEnd() => _scene.EnqueElements();

    public override void Dispose()
    {
        _scene.DisposeScene(RenderDevice);
        _hud.Dispose(RenderDevice);
    }
}

