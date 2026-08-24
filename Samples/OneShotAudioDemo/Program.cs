using System.Numerics;
using Yaeger.Audio;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// OneShotAudioDemo — stress-tests AudioSystem.PlayOneShot (#200): fires many overlapping
// positioned one-shots per second against a deliberately small voice budget (8, well under
// OpenAL Soft's typical 32-256 source ceiling), so the pool's stealing policy is exercised
// continuously instead of only under a rare worst case. Every one-shot spawns a small fading
// circle at its position so bursts are visible, not just audible. About one impact in ten is
// marked high-priority (drawn gold) to demonstrate that it is never stolen by ambient chatter.
// Controls: Space fires a manual burst of 12, Up/Down changes the auto-fire rate, 1-4 swap the
// steal policy live (Quietest/MostDistant/Oldest/DropNew), ESC exits.

using var window = Window.Create();
var world = new World();
var renderer = new Renderer(window);
var renderSystem = new UnifiedRenderSystem(renderer, null, world);

using var impactBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/impact.wav");
var audioSystem = new AudioSystem(world, window.AudioContext, oneShotVoiceBudget: 8);

var random = new Random(1);
var autoFireRate = 24f; // one-shots per second
var timeSinceLastFire = 0f;

var policies = new[]
{
    VoiceStealPolicy.Quietest,
    VoiceStealPolicy.MostDistant,
    VoiceStealPolicy.Oldest,
    VoiceStealPolicy.DropNew,
};

Console.WriteLine(
    $"Voice budget: {audioSystem.OneShotVoiceBudget}, policy: {audioSystem.OneShotStealPolicy}"
);
Console.WriteLine("Space: burst  Up/Down: rate  1-4: steal policy  ESC: quit");

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(Keys.Space, () => FireBurst(12));
Keyboard.AddKeyDown(Keys.Num1, () => SetPolicy(policies[0]));
Keyboard.AddKeyDown(Keys.Num2, () => SetPolicy(policies[1]));
Keyboard.AddKeyDown(Keys.Num3, () => SetPolicy(policies[2]));
Keyboard.AddKeyDown(Keys.Num4, () => SetPolicy(policies[3]));
Keyboard.AddKeyDown(Keys.Up, () => SetAutoFireRate(autoFireRate + 8f));
Keyboard.AddKeyDown(Keys.Down, () => SetAutoFireRate(autoFireRate - 8f));

window.OnUpdate += delta =>
{
    var dt = (float)delta;
    audioSystem.Update(dt);
    UpdateFlashes(dt);

    if (autoFireRate <= 0f)
        return;

    timeSinceLastFire += dt;
    var interval = 1f / autoFireRate;
    while (timeSinceLastFire >= interval)
    {
        timeSinceLastFire -= interval;
        FireOneShot();
    }
};

window.OnRender += _ => renderSystem.Render();

window.Run();

void SetPolicy(VoiceStealPolicy policy)
{
    audioSystem.OneShotStealPolicy = policy;
    Console.WriteLine($"Steal policy: {policy}");
}

void SetAutoFireRate(float rate)
{
    autoFireRate = Math.Clamp(rate, 0f, 200f);
    Console.WriteLine($"Auto-fire rate: {autoFireRate}/s");
}

void FireBurst(int count)
{
    for (var i = 0; i < count; i++)
        FireOneShot();
}

void FireOneShot()
{
    var position = new Vector2(
        (float)(random.NextDouble() * 1.8 - 0.9),
        (float)(random.NextDouble() * 1.8 - 0.9)
    );
    var gain = 0.2f + (float)random.NextDouble() * 0.8f;

    // Roughly one impact in ten is a "scripted" hit that must never go silent under load —
    // protected from stealing by ambient chatter via VoiceRequest.Priority.
    var highPriority = random.NextDouble() < 0.1;
    var priority = highPriority ? 10 : 0;

    audioSystem.PlayOneShot(impactBuffer, position, gain, AudioGroup.Sfx, priority);
    SpawnFlash(position, gain, highPriority);
}

void SpawnFlash(Vector2 position, float gain, bool highPriority)
{
    var scale = new Vector2(0.04f + gain * 0.05f);
    var color = highPriority ? new Color(255, 210, 60) : new Color(120, 200, 255);

    var entity = world.CreateEntity();
    world.AddComponent(entity, new Sprite("Assets/circle.png", color));
    world.AddComponent(entity, new Transform2D(position, 0f, scale));
    world.AddComponent(entity, new FlashLife(0.3f, 0.3f, scale));
}

void UpdateFlashes(float dt)
{
    List<Entity>? expired = null;

    foreach (var (entity, life) in world.GetStore<FlashLife>().All())
    {
        var remaining = life.Remaining - dt;
        if (remaining <= 0f)
        {
            (expired ??= []).Add(entity);
            continue;
        }

        world.AddComponent(entity, life with { Remaining = remaining });

        if (world.TryGetComponent<Transform2D>(entity, out var transform))
        {
            var t = remaining / life.Duration;
            world.AddComponent(entity, transform with { Scale = life.InitialScale * t });
        }
    }

    if (expired is null)
        return;

    foreach (var entity in expired)
        world.DestroyEntity(entity);
}

readonly record struct FlashLife(float Remaining, float Duration, Vector2 InitialScale);
