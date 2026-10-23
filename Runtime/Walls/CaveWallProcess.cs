using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Game.Components.Talking;
using Celeste.Mods.BrokemiaHelper.Entities.Walls;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Walls.Concealment;
using Celeste.Simulation.Presentation.Player.Death;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.BrokemiaHelper.Runtime.Walls
{
    public sealed class CaveWallProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();

        public CaveWallProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            hooks.Add(new Hook(typeof(OriginalTalkUiEntity).GetMethod(nameof(OriginalTalkUiEntity.Awake)),
                (Action<Action<OriginalTalkUiEntity, object>, OriginalTalkUiEntity, object>)TalkAwake));
            hooks.Add(new Hook(typeof(OriginalTalkUiEntity).GetMethod(nameof(OriginalTalkUiEntity.Update)),
                (Action<Action<OriginalTalkUiEntity>, OriginalTalkUiEntity>)TalkUpdate));
            hooks.Add(new Hook(typeof(PlayerDeathPresentation).GetMethod(nameof(PlayerDeathPresentation.Awake)),
                (Action<Action<PlayerDeathPresentation, object>, PlayerDeathPresentation, object>)DeadAwake));
        }

        private bool Owns(object scene) =>
            scene is ISourceEngineContextScene owner && ReferenceEquals(owner.EngineContext, engine);

        private void TalkAwake(Action<OriginalTalkUiEntity, object> orig, OriginalTalkUiEntity self, object scene)
        {
            orig(self, scene);
            if (!Owns(scene)) return;
            if (self.Handler.Entity == null ||
                CaveWall.AtPoint(scene, ((ISourceEntityPosition)self.Handler.Entity).Position) != null)
                self.SourceAlpha = 0;
        }

        private void TalkUpdate(Action<OriginalTalkUiEntity> orig, OriginalTalkUiEntity self)
        {
            orig(self);
            if (!Owns(self.Scene) || self.Handler.Entity == null) return;
            var wall = CaveWall.AtPoint(self.Scene, ((ISourceEntityPosition)self.Handler.Entity).Position);
            if (wall != null && (wall.Alpha >= 1 || wall.FadeIn))
            {
                var scene = (ISourceScene)self.Scene;
                float factor = SourceSceneCollision.FirstPoint<FakeWallState>(scene.SourceScene.Entities,
                    "Celeste.FakeWall", ((ISourceEntityPosition)self.Handler.Entity).Position) != null
                    ? 2f
                    : 4f;
                self.SourceAlpha = ScalarMath.Approach(self.SourceAlpha, 0, factor * engine.DeltaTime,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
            }
        }

        private void DeadAwake(Action<PlayerDeathPresentation, object> orig, PlayerDeathPresentation self, object scene)
        {
            orig(self, scene);
            if (!Owns(scene)) return;
            var walls = CaveWall.Tracked(scene);
            if (walls == null) return;
            foreach (CaveWall wall in walls) wall.DeadBodyAt(self.Position);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
