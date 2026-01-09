// See https://aka.ms/new-console-template for more information

using Pong;
using Pong.Components;
using Pong.Systems;

using Yaeger.Audio;
using Yaeger.ECS;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

using var window = Window.Create();
var world = new World();
var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world);

// Example: How to use the sound system (requires .wav audio files)
// Uncomment the following lines to play sounds:
//
// var soundBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/beep.wav");
// var soundSource = SoundSource.Create(window.AudioContext);
// soundSource.SetBuffer(soundBuffer);
// soundSource.Play();
//
// To play a sound when the ball hits a paddle:
// soundSource.Play();
//
// To loop background music:
// var musicSource = SoundSource.Create(window.AudioContext);
// musicSource.Looping = true;
// musicSource.SetBuffer(musicBuffer);
// musicSource.Play();

var updateSystems = new List<IUpdateSystem>
{
    new InputSystem(world),
    new MoveSystem(world),
    new PhysicsSystem(world),
};

var entityFactory = new EntityFactory(world);

int fps = 0;
double lastFpsCount = 0;
Stack<int> fpsCounts = new();

entityFactory.SpawnLeftPaddle();
entityFactory.SpawnRightPaddle();
entityFactory.SpawnBall();
entityFactory.SpawnBackground();

window.OnLoad += OnLoad;
window.OnResize += size => Console.WriteLine($"Window resized to {size.X}x{size.Y}");
window.OnUpdate += Update;
window.OnRender += Render;
window.OnClosing += () => Console.WriteLine("Average FPS: " + fpsCounts.Average());

Keyboard.AddKeyDown(Keys.Escape, () =>
{
    window.Close();
});

window.Run();

Console.WriteLine("Window closed");
return;

void OnLoad()
{
    var ball = world.GetStore<Ball>().All().First().Key;
    var leftPlayer = world.GetStore<Player>().All().First(e => e.Value == Player.Left).Key;
    var rightPlayer = world.GetStore<Player>().All().First(e => e.Value == Player.Right).Key;

    updateSystems.Add(new ScoringSystem(world, ball, leftPlayer, rightPlayer));
    updateSystems.Add(new PrintScoreSystem(world, leftPlayer, rightPlayer));
    updateSystems.Add(new ResetBallSystem(world, ball));
}

void Update(double delta)
{
    var deltaTime = (float)delta;

    foreach (var system in updateSystems)
    {
        system.Update(deltaTime);
    }
}

void Render(double delta)
{
    fps++;
    lastFpsCount += delta;

    if (lastFpsCount >= 1.0)
    {
        fpsCounts.Push(fps);
        Console.WriteLine($"FPS: {fps}");
        fps = 0;
        lastFpsCount = 0;
    }

    renderSystem.Render();
}