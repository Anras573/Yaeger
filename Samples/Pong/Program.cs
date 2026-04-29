// See https://aka.ms/new-console-template for more information

using Pong;
using Pong.Systems;
using Yaeger.ECS;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

using var window = Window.Create();
var world = new World();
var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world);
var textRenderer = new TextRenderer(window);
var textRenderSystem = new TextRenderSystem(textRenderer, world);

var updateSystems = new List<IUpdateSystem>
{
    new InputSystem(world),
    new MoveSystem(world),
    new PhysicsSystem(world),
};

var entityFactory = new EntityFactory(world);

int fps = 0;
double lastFpsCount = 0;
int fpsSum = 0;
int fpsSnapshots = 0;

entityFactory.SpawnLeftPaddle();
entityFactory.SpawnRightPaddle();
entityFactory.SpawnBall();
entityFactory.SpawnBackground();
entityFactory.SpawnScoreBoard();

window.OnLoad += OnLoad;
window.OnResize += size => Console.WriteLine($"Window resized to {size.X}x{size.Y}");
window.OnUpdate += Update;
window.OnRender += Render;
window.OnClosing += () =>
    Console.WriteLine("Average FPS: " + (fpsSnapshots > 0 ? (double)fpsSum / fpsSnapshots : 0));

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.Run();

Console.WriteLine("Window closed");
return;

void OnLoad()
{
    updateSystems.Add(new ScoringSystem(world));
    updateSystems.Add(new PrintScoreSystem(world));
    updateSystems.Add(new ResetBallSystem(world));
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
        fpsSum += fps;
        fpsSnapshots++;
        Console.WriteLine($"FPS: {fps}");
        fps = 0;
        lastFpsCount = 0;
    }

    renderSystem.Render();
    textRenderSystem.Render();
}
