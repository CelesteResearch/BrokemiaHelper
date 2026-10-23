using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Content.Tiles.Autotiling;
using Celeste.Graphics.Rendering;
using Celeste.Simulation.Gameplay.Collision;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Tiles;
using Celeste.Simulation.Gameplay.Walls.Concealment;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Displacement;

namespace Celeste.Mods.BrokemiaHelper.Entities.Walls
{
    [SceneTracked(false)]
    [CustomEntity("BrokemiaHelper/caveWall")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("BrokemiaHelper.CaveWall")]
    public partial class CaveWall : SceneEntityState, IGroupedOverlayTileRegion
    {
        private readonly ISourceEngineContext engine;
        private readonly char fillTile;
        private readonly bool disableTransitionFading, blocksDisplacement;
        private TileGrid tiles;
        private bool fadeOut, fadeIn = true, deadBodyCollided;
        private readonly EffectCutoutState cutout;
        private float transitionStartAlpha;
        private bool transitionFade;
        private CaveWall master;
        public List<CaveWall> Group;
        public IntPoint GroupBoundsMin, GroupBoundsMax;
        public bool HasGroup { get; private set; }
        public bool MasterOfGroup { get; private set; }
        public float Alpha => tiles.Alpha;
        public bool FadeIn => fadeIn;
        char IGroupedOverlayTileRegion.FillTile => fillTile;
        private ISecretWallScene Level => (ISecretWallScene)Scene;

        public CaveWall(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            fillTile = EntityAttributeReader.Char(data.Values, "tiletype", '3');
            disableTransitionFading = EntityAttributeReader.Bool(data.Values, "disableTransitionFading", false);
            blocksDisplacement = EntityAttributeReader.Bool(data.Values, "blockDisplacement", true);
            Collider = new HitboxColliderGeometry(data.Width, data.Height);
            Depth = -13001;
            OwnedComponents.Add(cutout = new EffectCutoutState());
            OwnedComponents.Add(new DisplacementRenderHookState(RenderDisplacement));
        }

        public static CaveWall Load(OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        void IGroupedOverlayTileRegion.SetGeneratedOverlay(AutotileGeneration generation)
        {
            tiles = generation.TileGrid;
            OwnedComponents.Add(tiles);
            OwnedComponents.Add(new TileInterceptorState(tiles, false));
        }

        internal static List<object> Tracked(object scene)
        {
            ((ISourceScene)scene).SourceScene.Entities.TryGetTrackedEntities(typeof(CaveWall), out var list);
            return list;
        }

        private RoomPlayerSession CollidePlayer() => SourceSceneCollisionQueries.First(this,
                ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(typeof(RoomPlayerSession))) as
            RoomPlayerSession;

        internal static CaveWall AtPoint(object scene, Vector2 point)
        {
            var list = Tracked(scene);
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var wall = (CaveWall)list[i];
                if (wall.Collidable && SourceEntityGeometryCollision.Point(wall, point)) return wall;
            }

            return null;
        }

        internal void DeadBodyAt(Vector2 position)
        {
            if (position.X >= Left && position.X <= Right && position.Y >= Top && position.Y <= Bottom)
                (MasterOfGroup ? this : master).deadBodyCollided = true;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (!HasGroup)
            {
                MasterOfGroup = true;
                Group = new List<CaveWall>();
                GroupBoundsMin = new IntPoint((int)X, (int)Y);
                GroupBoundsMax = new IntPoint((int)Right, (int)Bottom);
                AddToGroupAndFindChildren(this);
                var level = Level;
                GroupedOverlayTileGeneration.Generate(Group, level.SolidsData, level.TileBounds,
                    level.ForegroundAutotiler);
            }

            if (CollidePlayer() != null)
                foreach (var wall in Group)
                {
                    wall.tiles.Alpha = 0;
                    wall.fadeOut = true;
                    wall.fadeIn = false;
                    wall.cutout.Alpha = 0;
                }

            if (!disableTransitionFading)
                OwnedComponents.Add(new RoomTransitionListener
                {
                    OnOut = OnTransitionOut, OnOutBegin = OnTransitionOutBegin, OnIn = OnTransitionIn,
                    OnInBegin = OnTransitionInBegin
                });
        }

        private void AddToGroupAndFindChildren(CaveWall from)
        {
            if (from.X < GroupBoundsMin.X) GroupBoundsMin.X = (int)from.X;
            if (from.Y < GroupBoundsMin.Y) GroupBoundsMin.Y = (int)from.Y;
            if (from.Right > GroupBoundsMax.X) GroupBoundsMax.X = (int)from.Right;
            if (from.Bottom > GroupBoundsMax.Y) GroupBoundsMax.Y = (int)from.Bottom;
            from.HasGroup = true;
            Group.Add(from);
            if (from != this)
            {
                from.master = this;
                from.Group = Group;
            }

            foreach (CaveWall wall in Tracked(Scene))
                if (!wall.HasGroup &&
                    (SourceSceneCollision.CheckRect(
                         new IntRect((int)from.X - 1, (int)from.Y, (int)from.Width + 2, (int)from.Height), wall) ||
                     SourceSceneCollision.CheckRect(
                         new IntRect((int)from.X, (int)from.Y - 1, (int)from.Width, (int)from.Height + 2), wall)))
                    AddToGroupAndFindChildren(wall);
        }

        private void OnTransitionOutBegin()
        {
            transitionFade = SourceEntityCollision.CheckRect(this, Level.Bounds);
            if (transitionFade) transitionStartAlpha = tiles.Alpha;
        }

        private void OnTransitionOut(float p)
        {
            if (transitionFade) tiles.Alpha = transitionStartAlpha * (1f - p);
        }

        private void OnTransitionIn(float p)
        {
            if (transitionFade) tiles.Alpha = p;
        }

        private void OnTransitionInBegin()
        {
            if (!MasterOfGroup) return;
            var level = Level;
            RoomPlayerSession player = null;
            foreach (var wall in Group)
            {
                wall.transitionFade = level.PreviousBounds.HasValue &&
                                      SourceEntityCollision.CheckRect(wall, level.PreviousBounds.Value);
                if (wall.transitionFade) wall.tiles.Alpha = 0;
                player = wall.CollidePlayer();
                if (player != null) break;
            }

            if (player != null)
                foreach (var wall in Group)
                {
                    wall.transitionFade = false;
                    wall.tiles.Alpha = 0;
                }
        }

        public override void Update()
        {
            base.Update();
            cutout.Alpha = tiles.Alpha;
            if (fadeOut)
            {
                tiles.Alpha = ScalarMath.Approach(tiles.Alpha, 0, 3f * engine.DeltaTime,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
                if (tiles.Alpha <= 0) tiles.Alpha = 0;
            }
            else if (fadeIn)
            {
                tiles.Alpha = ScalarMath.Approach(tiles.Alpha, 1, 3f * engine.DeltaTime,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
                if (tiles.Alpha >= 1) tiles.Alpha = 1;
            }

            if (!MasterOfGroup) return;
            RoomPlayerSession player = null;
            foreach (var wall in Group)
            {
                player = wall.CollidePlayer();
                if (player != null) break;
            }

            if ((player != null && player.Machine.State != 9) || deadBodyCollided)
            {
                fadeOut = true;
                fadeIn = false;
                foreach (var wall in Group)
                {
                    wall.fadeOut = true;
                    wall.fadeIn = false;
                }
            }
            else if (fadeOut)
            {
                fadeOut = false;
                fadeIn = true;
                foreach (var wall in Group)
                {
                    wall.fadeOut = false;
                    wall.fadeIn = true;
                }
            }
        }

        public void RenderDisplacement()
        {
            if (!MasterOfGroup) return;
            var context = ((ISourceRenderEnvironment)engine).CurrentContext;
            foreach (var wall in Group)
                if (wall.Alpha > .2f && wall.blocksDisplacement)
                    SourcePrimitiveDrawing.Rect(context, wall, wall.X, wall.Y, wall.Width, wall.Height,
                        new Celeste.Core.Display.PackedColor(.5f, .5f, 0f, 1f));
        }

        public override void Render(ISourceRenderContext context)
        {
            SecretWallDrawing.RenderShakeMargins(this, tiles, Level, context);
            base.Render(context);
        }
    }
}
