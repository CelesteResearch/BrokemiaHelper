using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Enemies.Seeker;
using Celeste.Core.Rendering;

namespace Celeste.Mods.BrokemiaHelper.Rendering
{
    [SceneTracked]
    public sealed class MoveBlockBarrierRenderer : SeekerBarrierRendererState
    {
        public MoveBlockBarrierRenderer(ISourceEngineContext engine)
            : base(engine, (ISourceRenderEnvironment)engine, PackedColor.Purple, 0.65f, 0.75f)
        {
        }
    }
}
