using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Components.Animation;
using Celeste.Core.Components.Timing;
using Celeste.Core.Display;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Content.Extensions.Collectibles;
using Celeste.Everest.Gameplay.Collectibles.Strawberries.Seeds;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Graphics.Animation;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Scene.Following;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collectibles.Strawberry;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Simulation.Runtime.Session;

namespace Celeste.Mods.BrokemiaHelper.Entities.Collectibles.Strawberries
{
    public partial class TrollStrawberry
    {
        public override void Update()
        {
            if (WaitingOnSeeds)
            {
                return;
            }

            if (!collected)
            {
                if (!Winged)
                {
                    wobble += process.DeltaTime * 4f;
                    sprite.Y = (bloom.Y = (light.Y = (float)Math.Sin(wobble) * 2f));
                }

                int followIndex = Follower.FollowIndex;
                if (Follower.Leader != null && Follower.DelayTimer <= 0f &&
                    ((ISourceInstalledStrawberryRegistryEnvironment)process).StrawberryRegistry.IsFirstStrawberry(this))
                {
                    bool collectable = false;
                    if (Follower.Leader.Entity is RoomPlayerSession player && player.Scene != null &&
                        !player.StrawberriesBlocked)
                    {
                        collectable = player.OnSafeGround && (!Moon || player.Machine.State != 13);
                    }

                    if (collectable)
                    {
                        collectTimer += process.DeltaTime;
                        if (collectTimer > 0.15f)
                        {
                            OnCollect();
                        }
                    }
                    else
                    {
                        collectTimer = Math.Min(collectTimer, 0f);
                    }
                }
                else
                {
                    if (followIndex > 0)
                    {
                        collectTimer = -0.15f;
                    }

                    if (Winged)
                    {
                        Y += flapSpeed * process.DeltaTime;
                        if (flyingAway)
                        {
                            if (Y < (Level.Bounds.Top - 16))
                            {
                                RemoveSelf();
                            }
                        }
                        else
                        {
                            flapSpeed = ScalarMath.Approach(flapSpeed, 20f, 170f * process.DeltaTime);
                            if (Y < start.Y - 5f)
                            {
                                Y = start.Y - 5f;
                            }
                            else if (Y > start.Y + 5f)
                            {
                                Y = start.Y + 5f;
                            }
                        }
                    }
                }
            }

            base.Update();
            if (Follower.Leader != null && ((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.08f, process))
            {
                ParticleTypePresentation type = Moon
                    ? process.ParticleTypes.Strawberry.P_MoonGlow
                    : process.ParticleTypes.Strawberry.P_Glow;
                Level.ParticlesFG.Emit(type,
                    Position + SourceRandomMath.Range(process.SpriteRandom, -Vector2.One * 6f, Vector2.One * 6f));
            }
        }

        private void OnDash(Vector2 dir)
        {
            if (!flyingAway && Winged && !WaitingOnSeeds)
            {
                Depth = -1000000;
                OwnedComponents.Add(new SimulationCoroutine(process, FlyAwayRoutine()));
                flyingAway = true;
            }
        }

        private void OnAnimate(string id)
        {
            if (!flyingAway && id == "flap" && sprite.CurrentAnimationFrame % 9 == 4)
            {
                Play("event:/game/general/strawberry_wingflap");
                flapSpeed = -50f;
            }

            int pulseFrame = (id == "flap") ? 25 : ((!Moon) ? 35 : 30);
            if (sprite.CurrentAnimationFrame == pulseFrame)
            {
                lightTween.Start();
                if (!collected && (CollideCheck("Celeste.FakeWall") || CollideCheck("Celeste.Solid")))
                {
                    Play("event:/game/general/strawberry_pulse");
                    ((IOriginalStrawberryScene)Level).AddStrawberryDisplacementBurst(Position, 0.6f, 4f, 28f, 0.1f);
                    return;
                }

                Play("event:/game/general/strawberry_pulse");
                ((IOriginalStrawberryScene)Level).AddStrawberryDisplacementBurst(Position, 0.6f, 4f, 28f, 0.2f);
            }
        }
    }
}
