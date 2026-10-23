using Celeste.Core.Rendering;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Display;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;

namespace Celeste.Mods.BrokemiaHelper.Presentation.Platforms.Moving
{
    public static class HighlightedSolidDrawing
    {
        public static void DrawBorder(SceneEntityState owner, ISourceEngineContext engine,
            TileGrid highlight, ISourceRenderContext context)
        {
            if (highlight.Alpha > 0f && highlight.Alpha < 1f)
            {
                int num = (int)((1f - highlight.Alpha) * 16f);
                IntRect rect = new IntRect((int)owner.X, (int)owner.Y, (int)owner.Width, (int)owner.Height);
                rect.X = unchecked(rect.X - num);
                rect.Y = unchecked(rect.Y - num);
                rect.Width = unchecked(rect.Width + num * 2);
                rect.Height = unchecked(rect.Height + num * 2);
                SourcePrimitiveDrawing.HollowRect(context, owner, rect,
                    SourceColorInterpolation.Lerp(PackedColor.Purple, PackedColor.Pink, 0.7f,
                        ((ISourceScalarEnvironment)engine).ScalarArithmetic));
            }
        }
    }
}
