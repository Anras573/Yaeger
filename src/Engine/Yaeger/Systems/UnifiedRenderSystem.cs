using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Windowing;

namespace Yaeger.Systems;

/// <summary>
/// Renders sprites, sprite sheets, and text in a shared deterministic order using
/// <see cref="RenderLayer"/>, <see cref="Entity.Id"/>, and command kind as sort keys.
/// </summary>
public class UnifiedRenderSystem(
    IRenderSurface renderer,
    ITextRenderSurface textRenderer,
    World world,
    Window? window = null
)
{
    private readonly List<RenderCommand> _commands = [];

    public void Render()
    {
        UpdateCamera();
        BuildCommandQueue();

        renderer.BeginFrame();

        foreach (var command in _commands)
        {
            switch (command.Kind)
            {
                case RenderCommandKind.Sprite:
                    renderer.SubmitQuad(command.Transform, command.TexturePath!, command.Color);
                    break;
                case RenderCommandKind.SpriteSheet:
                    renderer.SubmitQuad(
                        command.Transform,
                        command.TexturePath!,
                        command.UvMin,
                        command.UvMax,
                        command.Color
                    );
                    break;
                case RenderCommandKind.Text:
                    renderer.FlushQueuedQuads();
                    if (command.TextComponent.TryGetNativeFont(out var nativeFont))
                    {
                        textRenderer.DrawText(
                            command.TextComponent.Content,
                            command.Transform,
                            nativeFont,
                            command.TextComponent.FontSize,
                            command.TextComponent.Color
                        );
                    }
                    else
                    {
                        textRenderer.DrawText(
                            command.TextComponent.Content,
                            command.Transform,
                            command.TextComponent.FontHandle,
                            command.TextComponent.FontSize,
                            command.TextComponent.Color
                        );
                    }
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown render command kind: {command.Kind}"
                    );
            }
        }

        renderer.EndFrame();
    }

    private void BuildCommandQueue()
    {
        _commands.Clear();

        var renderLayerStore = world.GetStore<RenderLayer>();
        var spriteSheetStore = world.GetStore<SpriteSheet>();
        var animationStateStore = world.GetStore<AnimationState>();

        foreach (
            (Entity entity, Sprite sprite, Transform2D transform) in world.Query<
                Sprite,
                Transform2D
            >()
        )
        {
            if (spriteSheetStore.TryGet(entity, out _) && animationStateStore.TryGet(entity, out _))
                continue;

            _commands.Add(
                RenderCommand.ForSprite(
                    entity,
                    GetRenderLayerValue(renderLayerStore, entity),
                    transform.TransformMatrix,
                    sprite
                )
            );
        }

        foreach (
            (
                Entity entity,
                SpriteSheet sheet,
                AnimationState state,
                Transform2D transform
            ) in world.Query<SpriteSheet, AnimationState, Transform2D>()
        )
        {
            if (sheet.FrameCount <= 0)
                continue;

            var frameIndex = Math.Clamp(state.CurrentFrameIndex, 0, sheet.FrameCount - 1);
            var (uvMin, uvMax) = sheet.GetFrameUv(frameIndex);

            _commands.Add(
                RenderCommand.ForSpriteSheet(
                    entity,
                    GetRenderLayerValue(renderLayerStore, entity),
                    transform.TransformMatrix,
                    sheet.TexturePath,
                    uvMin,
                    uvMax,
                    sheet.Tint.ToVector4()
                )
            );
        }

        foreach (
            (Entity entity, Text text, Transform2D transform) in world.Query<Text, Transform2D>()
        )
        {
            _commands.Add(
                RenderCommand.ForText(
                    entity,
                    GetRenderLayerValue(renderLayerStore, entity),
                    transform.TransformMatrix,
                    text
                )
            );
        }

        _commands.Sort(
            static (a, b) =>
            {
                var layerComparison = a.Layer.CompareTo(b.Layer);
                if (layerComparison != 0)
                    return layerComparison;

                var entityComparison = a.Entity.Id.CompareTo(b.Entity.Id);
                if (entityComparison != 0)
                    return entityComparison;

                return a.Kind.CompareTo(b.Kind);
            }
        );
    }

    private static int GetRenderLayerValue(
        ComponentStorage<RenderLayer> renderLayerStore,
        Entity entity
    ) => renderLayerStore.TryGet(entity, out var renderLayer) ? renderLayer.Value : 0;

    private void UpdateCamera()
    {
        // Without a Window we can't derive aspect ratio; leave the renderer's view-projection alone.
        if (window is null)
            return;

        foreach (var (_, camera) in world.GetStore<Camera2D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0 ? size.X / size.Y : 1f;
            renderer.SetCamera(camera.ViewProjection(aspectRatio));
            return;
        }

        renderer.SetCamera(Matrix4x4.Identity);
    }

    private enum RenderCommandKind
    {
        Sprite = 0,
        SpriteSheet = 1,
        Text = 2,
    }

    private readonly record struct RenderCommand(
        Entity Entity,
        int Layer,
        RenderCommandKind Kind,
        Matrix4x4 Transform,
        string? TexturePath,
        Vector2 UvMin,
        Vector2 UvMax,
        Vector4 Color,
        Text TextComponent
    )
    {
        public static RenderCommand ForSprite(
            Entity entity,
            int layer,
            Matrix4x4 transform,
            Sprite sprite
        ) =>
            new(
                entity,
                layer,
                RenderCommandKind.Sprite,
                transform,
                sprite.TexturePath,
                Vector2.Zero,
                Vector2.One,
                sprite.Tint.ToVector4(),
                default
            );

        public static RenderCommand ForSpriteSheet(
            Entity entity,
            int layer,
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) =>
            new(
                entity,
                layer,
                RenderCommandKind.SpriteSheet,
                transform,
                texturePath,
                uvMin,
                uvMax,
                color,
                default
            );

        public static RenderCommand ForText(
            Entity entity,
            int layer,
            Matrix4x4 transform,
            Text text
        ) =>
            new(
                entity,
                layer,
                RenderCommandKind.Text,
                transform,
                null,
                Vector2.Zero,
                Vector2.One,
                Vector4.Zero,
                text
            );
    }
}
