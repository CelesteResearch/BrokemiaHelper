// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Display;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Mods.BrokemiaHelper.Rendering;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Enemies.Seeker;

namespace Celeste.Mods.BrokemiaHelper.Entities.Barriers
{
    [CustomEntity("BrokemiaHelper/moveBlockBarrier")]
    [SceneTracked]
    public sealed class MoveBlockBarrier : SeekerBarrierState
    {
        public bool BlockDebris { get; set; }

        public static MoveBlockBarrier Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level, data, offset);

        private MoveBlockBarrier(OriginalLevelSceneRoot level, EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height,
                level.SolidWorldFactory.Create(data.Position + offset, false))
        {
            BlockDebris = data.Bool("blockDebris");
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            var registry = ((ISourceScene)scene).SourceScene.Entities;
            registry.GetTrackedEntity<SeekerBarrierRendererState>("Celeste.SeekerBarrierRenderer").Untrack(this);
            registry.GetTrackedEntity<MoveBlockBarrierRenderer>().Track(this);
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntity<MoveBlockBarrierRenderer>().Untrack(this);
        }

        public override void Render(ISourceRenderContext context)
        {
            var color = PackedColor.Pink * .5f;
            foreach (var particle in ParticlePositions)
            {
                var image = new AtlasTextureDraw(((ISourceDrawScope)context).Draw.Pixel,
                    Position + particle, Vector2.Zero, color, Vector2.One);
                ((ISourceRenderCommandSink<AtlasTextureDraw>)context).Draw(in image, this);
            }

            if (Flashing)
                SourcePrimitiveDrawing.Rect(context, this, Collider.AbsoluteLeft, Collider.AbsoluteTop,
                    Collider.Width, Collider.Height, PackedColor.Pink * Flash * .5f);
        }
    }
}
