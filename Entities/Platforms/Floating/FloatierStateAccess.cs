// SPDX-License-Identifier: MIT

using System;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Celeste.Game.Gameplay.Platforms.FloatySpaceBlocks;

namespace Celeste.Mods.BrokemiaHelper.Entities.Platforms.Floating
{
    internal static class FloatierStateAccess
    {
        internal static readonly Func<FloatySpaceBlockState, float> SinkTimer = Getter<float>("sinkTimer");
        internal static readonly Action<FloatySpaceBlockState, float> SetSinkTimer = Setter<float>("sinkTimer");
        internal static readonly Func<FloatySpaceBlockState, float> YLerp = Getter<float>("yLerp");
        internal static readonly Action<FloatySpaceBlockState, float> SetYLerp = Setter<float>("yLerp");
        internal static readonly Func<FloatySpaceBlockState, float> SineWave = Getter<float>("sineWave");
        internal static readonly Action<FloatySpaceBlockState, float> SetSineWave = Setter<float>("sineWave");
        internal static readonly Func<FloatySpaceBlockState, float> DashEase = Getter<float>("dashEase");
        internal static readonly Action<FloatySpaceBlockState, float> SetDashEase = Setter<float>("dashEase");
        internal static readonly Func<FloatySpaceBlockState, Vector2> DashDirection = Getter<Vector2>("dashDirection");
        internal static readonly Action<FloatySpaceBlockState, bool> SetMaster = MasterSetter();

        private static Func<FloatySpaceBlockState, T> Getter<T>(string name)
        {
            var owner = Expression.Parameter(typeof(FloatySpaceBlockState));
            return Expression
                .Lambda<Func<FloatySpaceBlockState, T>>(
                    Expression.Field(owner,
                        typeof(FloatySpaceBlockState).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)),
                    owner).Compile();
        }

        private static Action<FloatySpaceBlockState, T> Setter<T>(string name)
        {
            var owner = Expression.Parameter(typeof(FloatySpaceBlockState));
            var value = Expression.Parameter(typeof(T));
            return Expression.Lambda<Action<FloatySpaceBlockState, T>>(
                Expression.Assign(
                    Expression.Field(owner,
                        typeof(FloatySpaceBlockState).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)),
                    value), owner, value).Compile();
        }

        private static Action<FloatySpaceBlockState, bool> MasterSetter()
        {
            var owner = Expression.Parameter(typeof(FloatySpaceBlockState));
            var value = Expression.Parameter(typeof(bool));
            return Expression.Lambda<Action<FloatySpaceBlockState, bool>>(
                Expression.Call(owner, typeof(FloatySpaceBlockState).GetProperty("MasterOfGroup").GetSetMethod(true),
                    value), owner, value).Compile();
        }
    }
}
