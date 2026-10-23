using Celeste.Core.Randomness;
using Celeste.Core.Scheduling;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Content.Tiles.Autotiling;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Tiles;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.BrokemiaHelper.Presentation.Platforms.Moving
{
    public static class HighlightedSolidTiles
    {
        public static void Add(SceneEntityState owner, ISourceEngineContext engine, char tileType,
            char highlightTileType, out TileGrid sprite, out TileGrid highlight)
        {
            int newSeed = ((ISourceRandomEnvironment)engine).Random.Next();
            ((ISourceRandomProcessEnvironment)engine).RandomProcess.PushRandom(newSeed);
            sprite = ((ISourceAutotilingResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics)
                .ForegroundAutotiler.GenerateBox(tileType, (int)owner.Width / 8, (int)owner.Height / 8).TileGrid;
            owner.Add(sprite);
            ((ISourceRandomProcessEnvironment)engine).RandomProcess.PopRandom();
            ((ISourceRandomProcessEnvironment)engine).RandomProcess.PushRandom(newSeed);
            highlight = ((ISourceAutotilingResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics)
                .ForegroundAutotiler.GenerateBox(highlightTileType, (int)(owner.Width / 8f), (int)owner.Height / 8)
                .TileGrid;
            highlight.Alpha = 0f;
            owner.Add(highlight);
            ((ISourceRandomProcessEnvironment)engine).RandomProcess.PopRandom();
            owner.Add(new TileInterceptorState(sprite, false));
            owner.Add(new LightOccludeState());
        }
    }
}
