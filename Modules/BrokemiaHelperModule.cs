// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Components;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Mods.BrokemiaHelper.Entities.Barriers;
using Celeste.Mods.BrokemiaHelper.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Geometry;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.BrokemiaHelper.Modules
{
    public sealed class BrokemiaHelperModule : SourceEverestModule
    {
        private readonly List<IDisposable> hooks = [];
        private readonly HashSet<Type> extraMoveBlockTypes = [];
        private readonly HashSet<MethodInfo> hookedMoveChecks = [];

        public BrokemiaHelperModule(ISourceEngineContext engine) : base(engine)
        {
        }

        public override void Load()
        {
            hooks.Add(new Celeste.Mods.BrokemiaHelper.Runtime.Walls.CaveWallProcess(SourceEngine));
            hooks.Add(new Hook(
                typeof(OriginalLevelLoaderScene).GetMethod("LoadingThread",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                (Action<Action<OriginalLevelLoaderScene>, OriginalLevelLoaderScene>)LoadRenderer));
            RegisterMoveCheck(typeof(MoveBlockState));
            foreach (var method in new[]
                     {
                         nameof(SourcePlatformEntityState.MoveHCollideSolids),
                         nameof(SourcePlatformEntityState.MoveVCollideSolids)
                     })
                hooks.Add(new Hook(typeof(SourcePlatformEntityState).GetMethod(method),
                    (Func<Func<SourcePlatformEntityState, float, bool, Action<Vector2, Vector2, ISourcePlatformEntity>,
                            bool>,
                        SourcePlatformEntityState, float, bool, Action<Vector2, Vector2, ISourcePlatformEntity>, bool>)
                    MovePlatform));
            foreach (var method in new[]
                         { nameof(SourceActorEntityState.MoveHExact), nameof(SourceActorEntityState.MoveVExact) })
                hooks.Add(new Hook(typeof(SourceActorEntityState).GetMethod(method,
                        [typeof(int), typeof(SourceCollisionHandler), typeof(ISourceEntityCollider)]),
                    (Func<Func<SourceActorEntityState, int, SourceCollisionHandler, ISourceEntityCollider, bool>,
                        SourceActorEntityState, int, SourceCollisionHandler, ISourceEntityCollider, bool>)MoveActor));
        }

        public override void Initialize()
        {
            // Resolve only installed implementations once; no legacy assembly ABI facade and no hot-path reflection.
            string[] names =
            [
                "Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks.ConnectedMoveBlock",
                "Celeste.Mods.CommunalHelper.Entities.MoveSwapBlock",
                "Celeste.Mods.CommunalHelper.Entities.CassetteMoveBlock",
                "Celeste.Mods.CommunalHelper.Entities.DreamMoveBlock",
                "Celeste.Mods.CommunalHelper.Entities.EquationMoveBlock",
                "Celeste.Mods.Crystalline.Platforms.VitMoveBlock",
                "Celeste.Mods.DzhakeHelper.Entities.CustomMoveBlock"
            ];
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var name in names)
            {
                var type = assembly.GetType(name, false);
                if (type != null) RegisterMoveBlockType(type);
            }
        }

        public void RegisterMoveBlockType(Type type)
        {
            if (!typeof(SourcePlatformEntityState).IsAssignableFrom(type))
                throw new ArgumentException("A move block must be a shared platform entity.", nameof(type));
            if (extraMoveBlockTypes.Add(type)) RegisterMoveCheck(type);
        }

        private void RegisterMoveCheck(Type type)
        {
            var method = type.GetMethod("MoveCheck",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [typeof(Vector2)], null);
            if (method != null && hookedMoveChecks.Add(method))
                hooks.Add(new Hook(method,
                    (Func<Func<SceneEntityState, Vector2, bool>, SceneEntityState, Vector2, bool>)MoveCheck));
        }

        public override void Unload()
        {
            for (var i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            hookedMoveChecks.Clear();
            extraMoveBlockTypes.Clear();
        }

        private void LoadRenderer(Action<OriginalLevelLoaderScene> original, OriginalLevelLoaderScene loader)
        {
            if (ReferenceEquals(loader.Level.EngineContext, SourceEngine))
                loader.Level.Scheduler.QueueSourceEntity(new MoveBlockBarrierRenderer(SourceEngine));
            original(loader);
        }

        private List<object> Barriers(object scene)
        {
            if (scene is not OriginalLevelSceneRoot level ||
                !ReferenceEquals(level.EngineContext, SourceEngine)) return null;
            return ((ISourceScene)level).SourceScene.Entities.TryGetTrackedEntities(typeof(MoveBlockBarrier),
                       out var list)
                   && list.Count != 0
                ? list
                : null;
        }

        private static bool FirstCollidable(List<object> barriers) => ((MoveBlockBarrier)barriers[0]).Collidable;

        private static void SetCollidable(List<object> barriers, bool value)
        {
            for (var i = 0; i < barriers.Count; i++) ((MoveBlockBarrier)barriers[i]).Collidable = value;
        }

        private bool MoveCheck(Func<SceneEntityState, Vector2, bool> original, SceneEntityState block, Vector2 amount)
        {
            var barriers = Barriers(block.Scene);
            if (barriers == null) return original(block, amount);
            SetCollidable(barriers, true);
            try
            {
                return original(block, amount);
            }
            finally
            {
                SetCollidable(barriers, false);
            }
        }

        private bool MovePlatform(
            Func<SourcePlatformEntityState, float, bool, Action<Vector2, Vector2, ISourcePlatformEntity>, bool>
                original,
            SourcePlatformEntityState block, float amount, bool thruDashBlocks,
            Action<Vector2, Vector2, ISourcePlatformEntity> onCollide)
        {
            if (block is not MoveBlockState && !extraMoveBlockTypes.Contains(block.GetType()))
                return original(block, amount, thruDashBlocks, onCollide);
            var barriers = Barriers(block.Scene);
            if (barriers == null) return original(block, amount, thruDashBlocks, onCollide);
            var prior = FirstCollidable(barriers);
            SetCollidable(barriers, true);
            try
            {
                return original(block, amount, thruDashBlocks, onCollide);
            }
            finally
            {
                SetCollidable(barriers, prior);
            }
        }

        private bool MoveActor(
            Func<SourceActorEntityState, int, SourceCollisionHandler, ISourceEntityCollider, bool> original,
            SourceActorEntityState actor, int amount, SourceCollisionHandler onCollide, ISourceEntityCollider pusher)
        {
            var barriers = Barriers(actor.Scene);
            if (barriers == null) return original(actor, amount, onCollide, pusher);
            var prior = FirstCollidable(barriers);
            var isDebris = actor.GetType() == typeof(MoveBlockDebrisState);
            for (var i = 0; i < barriers.Count; i++)
            {
                var barrier = (MoveBlockBarrier)barriers[i];
                barrier.Collidable = isDebris && barrier.BlockDebris;
            }

            try
            {
                return original(actor, amount, onCollide, pusher);
            }
            finally
            {
                SetCollidable(barriers, prior);
            }
        }
    }
}
