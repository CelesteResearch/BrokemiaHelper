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
        public void OnPlayer(RoomPlayerSession player)
        {
            if (Follower.Leader == null && !collected && !WaitingOnSeeds)
            {
                ReturnHomeWhenLost = true;
                if (Winged)
                {
                    OriginalLevelSceneRoot level = Level;
                    Winged = false;
                    sprite.Rate = 0f;
                    SourceAlarmState.Set(process.AlarmPool, OwnedComponents, Follower.FollowDelay, delegate
                    {
                        sprite.Rate = 1f;
                        sprite.Play("idle", false, false);
                        level.Particles.Emit(process.ParticleTypes.Strawberry.P_WingsBurst, 8,
                            Position + new Vector2(8f, 0f), new Vector2(4f, 2f));
                        level.Particles.Emit(process.ParticleTypes.Strawberry.P_WingsBurst, 8,
                            Position - new Vector2(8f, 0f), new Vector2(4f, 2f));
                    });
                }

                Play("event:/game/general/strawberry_touch");
                player.Leader.GainFollower(Follower);
                wiggler.Start();
                Depth = -1000000;
            }
        }

        public void OnCollect()
        {
            if (collected)
            {
                return;
            }

            int collectIndex = 0;
            collected = true;
            if (Follower.Leader != null)
            {
                RoomPlayerSession p = Follower.Leader.Entity as RoomPlayerSession;
                collectIndex = p.StrawberryCollectIndex;
                p.StrawberryCollectIndex++;
                p.StrawberryCollectResetTimer = 2.5f;
                Follower.Leader.LoseFollower(Follower);
            }

            ChapterCompletionSession session = Level.Session;
            if (!reappear)
            {
                session.DoNotLoad.Add(ID);
            }

            session.UpdateLevelStartDashes();
            OwnedComponents.Add(new SimulationCoroutine(process, CollectRoutine(collectIndex)));
        }

        public void CollectedSeeds()
        {
            waitingOnSeeds = false;
            Visible = true;
            Collidable = true;
            bloom.Visible = (light.Visible = true);
            Level.Session.SetFlag(gotSeedFlag);
        }

        private IEnumerator CollectRoutine(int collectIndex)
        {
            OriginalLevelSceneRoot level = Level;
            Tags = OriginalSceneTags.TransitionUpdate;
            Depth = -2000010;
            Play("event:/game/general/seed_poof");
            Collidable = false;
            sprite.Scale = Vector2.One * 2f;
            yield return 0.05f;
            process.RumbleSession.Rumble(1, 1);
            for (int i = 0; i < 6; i++)
            {
                float num = SourceRandomMath.NextFloat(process.SpriteRandom, (float)Math.PI * 2f);
                level.ParticlesFG.Emit(process.ParticleTypes.StrawberrySeed.P_Burst, 1,
                    Position + Vector2Geometry.AngleToVector(num, 4f), Vector2.Zero, num);
            }

            var bloomWasVisible = bloom.Visible;
            var lightWasVisible = light.Visible;
            bloom.Visible = false;
            light.Visible = false;
            Visible = false;
            // Yes I know it's invisible, I just don't want to work out the timing manually
            sprite.Play("collect");
            while (sprite.Animating)
            {
                yield return null;
            }

            if (!reappear || ID.Level != level.Session.Level)
            {
                RemoveSelf();
                yield break;
            }

            // Reappear            
            yield return 0.2f;
            bloom.Visible = bloomWasVisible;
            light.Visible = lightWasVisible;
            Position = start;
            sprite.Reverse("collect", true);
            while (sprite.Animating)
            {
                yield return null;
            }

            Winged = hadWings;
            if (Winged)
            {
                sprite.Play("flap");
            }
            else
            {
                sprite.Play("idle");
            }

            process.AudioSession.Play("event:/game/general/seed_reappear", Position, process.SaveContext, "count", 0);
            sprite.Scale = Vector2.One;
            Visible = true;
            Collidable = true;
            collected = false;
            Follower.Leader = null;
            Depth = -100;
            bloom.Visible = bloomWasVisible;
            light.Visible = lightWasVisible;
            ((IOriginalStrawberryScene)level).AddStrawberryDisplacementBurst(Position, 0.2f, 8f, 28f, 0.2f);
        }
    }
}
