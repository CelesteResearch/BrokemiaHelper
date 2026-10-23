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
        private void OnLoseLeader()
        {
            if (!collected && ReturnHomeWhenLost)
            {
                SourceAlarmState.Set(process.AlarmPool, OwnedComponents, 0.15f, delegate
                {
                    Vector2 displacement = process.VectorArithmetic.SafeNormalize(start - Position);
                    float dist = process.VectorArithmetic.Distance(Position, start);
                    float scaleFactor = ScalarMath.ClampedMap(dist, 16f, 120f, 16f, 96f);
                    Vector2 control = start + displacement * 16f + Vector2Geometry.Perpendicular(displacement) *
                        scaleFactor * RandomSelection.Choose(process.SpriteRandom, 1, -1);
                    SimpleCurve curve = new SimpleCurve(Position, start, control);
                    SourceTweenState tween = SourceTweenState.Create(process.TweenPool,
                        SourceTweenState.TweenMode.Oneshot, process.EasingArithmetic.SineOut,
                        ((ISourceExtremaEnvironment)process).ExtremaArithmetic.Max(dist / 100f, 0.4f), start: true);
                    tween.OnUpdate = delegate(SourceTweenState f)
                    {
                        Position = process.CurveArithmetic.GetPoint(curve, f.Eased);
                    };
                    tween.OnComplete = delegate { Depth = 0; };
                    OwnedComponents.Add(tween);
                });
            }
        }
    }
}
