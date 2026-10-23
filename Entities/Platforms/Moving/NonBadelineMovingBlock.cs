// Licensed under the MIT License.

using Celeste.Core.Display;
using System.Linq;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Scene.Tracking;
using Celeste.Core.Rendering;
using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Components.Animation;
using Celeste.Core.Components.Timing;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Randomness;
using Celeste.Core.Scheduling;
using Celeste.Scene.Entities;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.FinalBossMovingBlock;
using Celeste.Simulation.Gameplay.Actors.Debris;
using Celeste.Simulation.Gameplay.Tiles;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Content.Tiles.Autotiling;
using Celeste.Graphics.Display;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;

namespace Celeste.Mods.BrokemiaHelper.Entities.Platforms.Moving
{
    [CustomEntity("BrokemiaHelper/nonBadelineMovingBlock")]
    [SceneTracked(false)]
    [RuntimeTypeAlias("BrokemiaHelper.NonBadelineMovingBlock")]
    public class NonBadelineMovingBlock : SourceSolidEntityState
    {
        protected ISourceEngineContext Engine => SourceWorld.EngineContext;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;
        private IOriginalGameplayProcess Process => (IOriginalGameplayProcess)Engine;

        protected virtual void MoveAlongTween(Vector2 from, Vector2 to, float eased)
            => MoveTo(Vector2Interpolation.Lerp(from, to, eased, ((ISourceScalarEnvironment)Engine).ScalarArithmetic));

        private ParticleTypePresentation P_Stop => Process.ParticleTypes.FinalBossMovingBlock.P_Stop;

        private ParticleTypePresentation P_Break => Process.ParticleTypes.FinalBossMovingBlock.P_Break;

        protected float travelTime;
        protected float[] nodeTravelTimes;
        protected int stopNode;
        protected string startFlag;
        private bool firstUpdate = true;
        private Func<float, float> easer;

        private float startDelay;

        private int nodeIndex;

        private Vector2[] nodes;

        private TileGrid sprite;

        private TileGrid highlight;

        private SimulationCoroutine moveCoroutine;

        private bool isHighlighted;

        public NonBadelineMovingBlock(Vector2[] nodes, float width, float height, char tileType,
            char highlightTileType, string flag, float delay, float travelTime, string perNodeTravelTimes,
            int surfaceSoundIndex, string easeType, int stopNode, ISourceEngineContext engine,
            ISourceSolidWorldFactory worlds)
            : base(nodes[0], width, height, false, worlds.Create(nodes[0], false))
        {
            SurfaceSoundIndex = surfaceSoundIndex;
            this.nodes = nodes;
            startFlag = flag;
            this.travelTime = travelTime;
            if (!string.IsNullOrWhiteSpace(perNodeTravelTimes))
                nodeTravelTimes = perNodeTravelTimes.Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            this.stopNode = stopNode;
            easer = ((ISourceNamedEasingArithmetic)((ISourceEasingEnvironment)engine).EasingArithmetic)
                .NamedEasers[easeType];
            startDelay = delay;
            startDelay -= engine.DeltaTime;
            global::Celeste.Mods.BrokemiaHelper.Presentation.Platforms.Moving.HighlightedSolidTiles.Add(this, engine,
                tileType, highlightTileType, out sprite, out highlight);
        }

        public NonBadelineMovingBlock(EntityData data, Vector2 offset, ISourceEngineContext engine,
            ISourceSolidWorldFactory worlds)
            : this(data.NodesWithPosition(offset), data.Width, data.Height, data.Char("tiletype", 'g'),
                data.Char("highlightTiletype", 'G'), data.Attr("startFlag", null), data.Float("startDelay", 0f),
                data.Float("travelTime", .8f), data.Attr("perNodeTravelTimes", null),
                data.Int("surfaceSoundIndex", 8), data.Attr("easing", "CubeIn"), data.Int("stopNode", -1),
                engine, worlds)
        {
        }

        public static NonBadelineMovingBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(data, offset, level.EngineContext,
            level.SolidWorldFactory);

        public override void Update()
        {
            base.Update();
            if (firstUpdate)
            {
                firstUpdate = false;
                return;
            }

            if (moveCoroutine == null && (string.IsNullOrWhiteSpace(startFlag) || Level.Session.GetFlag(startFlag)))
                StartMoving();
        }

        public override void OnShake(Vector2 amount)
        {
            base.OnShake(amount);
            sprite.Position = amount;
        }

        public void StartMoving()
        {
            OwnedComponents.Add(moveCoroutine = new SimulationCoroutine(Engine, MoveSequence()));
        }

        private IEnumerator MoveSequence()
        {
            while (true)
            {
                StartShaking(0.2f + startDelay);
                if (!isHighlighted)
                {
                    for (float p = 0f; p < 1f; p += Engine.Frame.DeltaTime / (0.2f + startDelay + 0.2f))
                    {
                        highlight.Alpha = ((ISourceEasingEnvironment)Engine).EasingArithmetic.CubeIn(p);
                        sprite.Alpha = 1f - highlight.Alpha;
                        yield return null;
                    }

                    highlight.Alpha = 1f;
                    sprite.Alpha = 0f;
                    isHighlighted = true;
                }
                else
                {
                    yield return 0.2f + startDelay + 0.2f;
                }

                startDelay = 0f;
                nodeIndex++;
                nodeIndex %= nodes.Length;
                Vector2 from = Position;
                Vector2 to = nodes[nodeIndex];
                float actualTravelTime = nodeTravelTimes != null ? nodeTravelTimes[nodeIndex] : travelTime;
                SourceTweenState tween = SourceTweenState.Create(Engine.TweenPool, SourceTweenState.TweenMode.Oneshot,
                    easer, actualTravelTime, start: true);
                tween.OnUpdate = delegate(SourceTweenState t) { MoveAlongTween(from, to, t.Eased); };
                tween.OnComplete = delegate(SourceTweenState t)
                {
                    if (CollideCheck("Celeste.SolidTiles",
                            Position + ((ISourceVectorEnvironment)Engine).VectorArithmetic.SafeNormalize(to - from) *
                            2f))
                    {
                        Process.AudioSession.Play("event:/game/06_reflection/fallblock_boss_impact", base.Center,
                            Process.SaveContext);
                        ImpactParticles(to - from);
                    }
                    else
                    {
                        StopParticles(to - from);
                    }
                };
                OwnedComponents.Add(tween);
                yield return actualTravelTime;
                if (stopNode == nodeIndex) yield break;
            }
        }

        private void StopParticles(Vector2 moved)
            => global::Celeste.Game.Presentation.Platforms.Effects.MovingSolidParticleEffects.Stop(this, Engine, moved);

        private void BreakParticles()
        {
            for (int i = 0; (float)i < base.Width; i += 4)
            {
                for (int j = 0; (float)j < base.Height; j += 4)
                {
                    var particles = Level.Particles;
                    var particle = P_Break;
                    Vector2 emission = Position + new Vector2(2 + i, 2 + j);
                    Vector2 range = Vector2.One * 2f;
                    Vector2 direction = Position + new Vector2(2 + i, 2 + j) - Center;
                    particles.Emit(particle, 1, emission, range, (float)Math.Atan2(direction.Y, direction.X));
                }
            }
        }

        private void ImpactParticles(Vector2 moved)
            => global::Celeste.Game.Presentation.Platforms.Effects.MovingSolidParticleEffects.Impact(this, Engine,
                moved);

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = Position;
            Position += base.Shake;
            base.Render(context);
            global::Celeste.Mods.BrokemiaHelper.Presentation.Platforms.Moving.HighlightedSolidDrawing.DrawBorder(this,
                Engine, highlight, context);

            Position = position;
        }

        private void Finish()
        {
            Vector2 vector = base.CenterRight + Vector2.UnitX * 10f;
            for (int i = 0; (float)i < base.Width / 8f; i++)
            {
                for (int j = 0; (float)j < base.Height / 8f; j++)
                {
                    Level.Scheduler.QueueSourceEntity(Engine.EntityPools
                        .Create<DebrisState>(() => new DebrisState(Engine, SourceWorld.TargetMotionPolicy))
                        .Init(Position + new Vector2(4 + i * 8, 4 + j * 8), 'f').BlastFrom(vector));
                }
            }

            BreakParticles();
            DestroyStaticMovers();
            RemoveSelf();
        }

        public void Destroy(float delay)
        {
            if (base.Scene != null)
            {
                if (moveCoroutine != null)
                {
                    OwnedComponents.Remove(moveCoroutine);
                }

                if (delay <= 0f)
                {
                    Finish();
                    return;
                }

                StartShaking(delay);
                SourceAlarmState.Set(Engine.AlarmPool, OwnedComponents, delay, Finish);
            }
        }
    }
}
