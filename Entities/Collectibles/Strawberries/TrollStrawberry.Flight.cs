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
        private IEnumerator FlyAwayRoutine()
        {
            rotateWiggler.Start();
            flapSpeed = -200f;
            SourceTweenState tween = SourceTweenState.Create(process.TweenPool, SourceTweenState.TweenMode.Oneshot,
                process.EasingArithmetic.CubeOut, 0.25f, start: true);
            tween.OnUpdate = delegate(SourceTweenState t)
            {
                flapSpeed = process.ScalarArithmetic.Lerp(-200f, 0f, t.Eased);
            };
            OwnedComponents.Add(tween);
            yield return 0.1f;
            Play("event:/game/general/strawberry_laugh");
            yield return 0.2f;
            if (!Follower.HasLeader)
            {
                Play("event:/game/general/strawberry_flyaway");
            }

            tween = SourceTweenState.Create(process.TweenPool, SourceTweenState.TweenMode.Oneshot, null, 0.5f,
                start: true);
            tween.OnUpdate = delegate(SourceTweenState t)
            {
                flapSpeed = process.ScalarArithmetic.Lerp(0f, -200f, t.Eased);
            };
            OwnedComponents.Add(tween);
        }
    }
}
