// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Platforms.FloatySpaceBlocks;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.FloatySpaceBlock;

namespace Celeste.Mods.BrokemiaHelper.Entities.Platforms.Floating
{
    [CustomEntity("BrokemiaHelper/floatierSpaceBlock")]
    [RuntimeTypeAlias("BrokemiaHelper.FloatierSpaceBlock")]
    [EverestTrackedAs(typeof(FloatySpaceBlockState))]
    public class FloatierSpaceBlock : FloatySpaceBlockState
    {
        public float floatinessBoost;
        public float dashEaseMultiplier;
        public float dashOffsetMultiplier;
        private float naturalFloatiness;
        private int sinkAmount;
        private float unsinkDelay;
        private float sinkSpeed;
        private float unsinkSpeed;
        private readonly ISourceEngineContext engine;

        public static FloatierSpaceBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, level.SolidWorldFactory, level.EngineContext);

        public FloatierSpaceBlock(EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds,
            ISourceEngineContext engine) : base(data, offset, worlds)
        {
            this.engine = engine;
            floatinessBoost = data.Float("floatinessMultiplier", data.Has("naturalFloatiness") ? 0f : 1f);
            dashEaseMultiplier = data.Float("bounceBackMultiplier", 1f);
            dashOffsetMultiplier = data.Float("dashOffsetMultiplier", floatinessBoost);
            naturalFloatiness = data.Float("naturalFloatiness", 0f);
            sinkAmount = data.Int("sinkAmount", 0);
            unsinkDelay = data.Float("unsinkDelay", 0f);
            sinkSpeed = data.Float("sinkSpeed", 1f);
            unsinkSpeed = data.Float("unsinkSpeed", 1f);
        }

        public override void Update()
        {
            bool master = MasterOfGroup;
            FloatierStateAccess.SetMaster(this, false);
            base.Update();
            FloatierStateAccess.SetMaster(this, master);
            if (MasterOfGroup)
            {
                bool riding = false;
                foreach (var block in Group)
                    if (block.HasPlayerRider())
                    {
                        riding = true;
                        break;
                    }

                if (!riding)
                    foreach (var jumpthru in Jumpthrus)
                        if (jumpthru.HasPlayerRider())
                        {
                            riding = true;
                            break;
                        }

                float timer = FloatierStateAccess.SinkTimer(this);
                if (riding) FloatierStateAccess.SetSinkTimer(this, timer = .3f * floatinessBoost + unsinkDelay);
                else if (timer > 0f) FloatierStateAccess.SetSinkTimer(this, timer -= engine.DeltaTime);
                float lerp = FloatierStateAccess.YLerp(this);
                FloatierStateAccess.SetYLerp(this,
                    ScalarMath.Approach(lerp, timer > 0f ? 1f : 0f,
                        (timer > 0f ? sinkSpeed : unsinkSpeed) * engine.DeltaTime,
                        ((ISourceExtremaEnvironment)engine).ExtremaArithmetic));
                FloatierStateAccess.SetSineWave(this, FloatierStateAccess.SineWave(this) + engine.DeltaTime);
                FloatierStateAccess.SetDashEase(this,
                    ScalarMath.Approach(FloatierStateAccess.DashEase(this), 0f,
                        engine.DeltaTime * 1.5f * dashEaseMultiplier,
                        ((ISourceExtremaEnvironment)engine).ExtremaArithmetic));
                MoveToTarget();
            }

            LiftSpeed = Vector2.Zero;
        }

        private void MoveToTarget()
        {
            float sine = (float)Math.Sin(FloatierStateAccess.SineWave(this)) * 4f;
            Vector2 displacement =
                ScalarMath.YoYo(
                    ((ISourceEasingEnvironment)engine).EasingArithmetic.QuadIn(FloatierStateAccess.DashEase(this))) *
                FloatierStateAccess.DashDirection(this) * 8f * dashOffsetMultiplier;
            FloatyPlatformGroupMotion.Move(ref Moves, sine, displacement, new Target(this));
        }

        private readonly struct Target : IFloatyPlatformTarget
        {
            private readonly FloatierSpaceBlock owner;
            public Target(FloatierSpaceBlock owner) => this.owner = owner;

            public float GetTargetY(float origin, float sine, float dash)
            {
                float destination = origin + 12f * owner.floatinessBoost + owner.sinkAmount;
                float bobbing =
                    ScalarMath.Lerp(origin, destination,
                        ((ISourceEasingEnvironment)owner.engine).EasingArithmetic.SineInOut(
                            FloatierStateAccess.YLerp(owner)),
                        ((ISourceScalarEnvironment)owner.engine).ScalarArithmetic) + sine * owner.floatinessBoost +
                    sine * owner.naturalFloatiness;
                return bobbing + dash;
            }
        }
    }
}
