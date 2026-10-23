using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Graphics.Animation;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.Puffer;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.BrokemiaHelper.Entities.Springs
{
    [CustomEntity("BrokemiaHelper/dashSpring", "BrokemiaHelper/wallDashSpringRight",
        "BrokemiaHelper/wallDashSpringLeft", "BrokemiaHelper/dashSpringDown")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("BrokemiaHelper.DashSpring")]
    public class DashSpring : OriginalSpringState
    {
        private readonly bool ignoreRedBoosters;

        public DashSpring(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(data.Position + offset,
                GetOrientationFromName(data.Name) == (SpringOrientation)3
                    ? SpringOrientation.Floor
                    : GetOrientationFromName(data.Name), EntityAttributeReader.Bool(data.Values, "playerCanUse", true),
                (IOriginalSpringProcess)engine)
        {
            Orientation = GetOrientationFromName(data.Name);
            ignoreRedBoosters = EntityAttributeReader.Bool(data.Values, "ignoreRedBoosters", false);
            if (EntityAttributeReader.Bool(data.Values, "ignoreHoldables", false))
                OwnedComponents.Remove(OwnedComponents.Get<HoldableColliderState>());
            OwnedComponents.Remove(OwnedComponents.Get<PlayerColliderComponentState>());
            OwnedComponents.Add(new PlayerColliderComponentState(OnDashCollide));
            var sprite = SpringSprite;
            sprite.Reset(SpringProcess.GameGraphics.GameAtlas,
                EntityAttributeReader.Attr(data.Values, "spritePath", "objects/BrokemiaHelper/dashSpring/"));
            sprite.Add("idle", "", 0f, 0);
            sprite.Add("bounce", "", .07f, "idle", 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 4, 5);
            sprite.Add("disabled", "white", .07f);
            sprite.Play("idle");
            sprite.Origin.X = sprite.Width / 2f;
            sprite.Origin.Y = sprite.Height;
            if (Orientation == (SpringOrientation)3)
            {
                SpringStaticMover.SolidChecker = s => SourceEntityCollision.CheckAt(this, s, Position - Vector2.UnitY);
                SpringStaticMover.JumpThruChecker =
                    s => SourceEntityCollision.CheckAt(this, s, Position - Vector2.UnitY);
                Collider = new HitboxColliderGeometry(16f, 6f, -8f, 0f);
                OwnedComponents.Get<PufferColliderState>().Collider = new HitboxColliderGeometry(16f, 10f, -8f, 0f);
                sprite.Rotation = (float)Math.PI;
            }
        }

        public static SpringOrientation GetOrientationFromName(string name)
            => name switch
            {
                "BrokemiaHelper/dashSpring" => SpringOrientation.Floor,
                "BrokemiaHelper/wallDashSpringRight" => SpringOrientation.WallRight,
                "BrokemiaHelper/wallDashSpringLeft" => SpringOrientation.WallLeft,
                "BrokemiaHelper/dashSpringDown" => (SpringOrientation)3,
                _ => throw new Exception("Dash Spring name doesn't correlate to a valid Orientation!")
            };

        private void OnDashCollide(RoomPlayerSession player)
        {
            if (player.Machine.State == 9 || !SpringPlayerCanUse || !player.DashAttacking) return;
            if (ignoreRedBoosters && player.Machine.State == 5) return;
            switch (Orientation)
            {
                case SpringOrientation.Floor:
                    if (player.Data.Motion.Normal.Speed.Y >= 0f)
                    {
                        AnimateSpringBounce();
                        player.SuperBounce(Top);
                    }

                    return;
                case SpringOrientation.WallLeft:
                    if (player.SideBounce(1, Right, CenterY)) AnimateSpringBounce();
                    return;
                case SpringOrientation.WallRight:
                    if (player.SideBounce(-1, Left, CenterY)) AnimateSpringBounce();
                    return;
                case (SpringOrientation)3:
                    if (player.Data.Motion.Normal.Speed.Y <= 0f)
                    {
                        AnimateSpringBounce();
                        player.SuperBounce(Bottom + player.Height);
                        player.Data.Motion.Normal.VariableJumpSpeed = player.Data.Motion.Normal.Speed.Y = 185f;
                        ((Celeste.Simulation.Presentation.ScreenEffects.ISourceLevelShakeScene)Scene).DirectionalShake(
                            Vector2.UnitY, .1f);
                    }

                    return;
                default: throw new Exception("Orientation not supported!");
            }
        }

        public static DashSpring Load(OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset);
    }
}
