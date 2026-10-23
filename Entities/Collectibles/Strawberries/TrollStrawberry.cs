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
    [CustomEntity("BrokemiaHelper/trollStrawberry")]
    [RegisterStrawberry(false, false)]
    [RuntimeTypeAlias("BrokemiaHelper.TrollStrawberry")]
    public partial class TrollStrawberry : SceneEntityState, IStrawberrySeeded, ISourceStrawberryCollectible
    {
        private readonly IOriginalStrawberryProcess process;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        public static TrollStrawberry Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, new MapEntityId(room.Name, data.ID),
            (IOriginalStrawberryProcess)level.EngineContext);

        private void Play(string sound) => process.AudioSession.Play(sound, Position, process.SaveContext);
        public MapEntityId ID;

        private Sprite sprite;

        public FollowerState Follower;

        private WigglerComponent wiggler;

        private WigglerComponent rotateWiggler;

        private BloomPointState bloom;

        private VertexLightState light;

        private SourceTweenState lightTween;

        private float wobble;

        private Vector2 start;

        private float collectTimer;

        private bool collected;

        private bool flyingAway;

        private float flapSpeed;

        public bool ReturnHomeWhenLost = true;

        private bool reappear;

        protected bool waitingOnSeeds;

        protected List<GenericStrawberrySeed> seeds;

        private bool hadWings;

        public bool Winged { get; private set; }

        public bool Moon { get; private set; }

        public List<GenericStrawberrySeed> Seeds => seeds;

        public string gotSeedFlag => "collected_seeds_of_" + ID.ToString();

        public bool WaitingOnSeeds => waitingOnSeeds;

        public TrollStrawberry(EntityData data, Vector2 offset, MapEntityId gid, IOriginalStrawberryProcess process)
            : base(((ISourceEntitySourceDataEnvironment)process).EntitySourceDataRegistration, data.Position + offset)
        {
            this.process = process;
            ID = gid;
            Position = (start = data.Position + offset);
            hadWings = Winged = EntityAttributeReader.Bool(data.Values, "winged", false);
            Moon = EntityAttributeReader.Bool(data.Values, "moon");
            reappear = EntityAttributeReader.Bool(data.Values, "reappear", false);
            Depth = -100;
            Collider = new HitboxColliderGeometry(14f, 14f, -7f, -7f);
            OwnedComponents.Add(new PlayerColliderComponentState(OnPlayer));
            OwnedComponents.Add(new MirrorReflectionState());
            OwnedComponents.Add(Follower =
                new FollowerState(process, OriginalSceneTags.Persistent, ID, onLoseLeader: OnLoseLeader));
            Follower.FollowDelay = 0.3f;
            if (Winged)
            {
                OwnedComponents.Add(new DashListenerState
                {
                    OnDash = new Action<Vector2>(OnDash)
                });
            }

            if (data.Nodes != null && data.Nodes.Length != 0)
            {
                seeds = [];
                for (int i = 0; i < data.Nodes.Length; i++)
                {
                    Seeds.Add(new GenericStrawberrySeed(this, offset + data.Nodes[i], i, false, process));
                }
            }
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            if (Moon)
            {
                sprite = process.GameGraphics.SpriteFactory.Create("moonberry");
            }
            else
            {
                sprite = process.GameGraphics.SpriteFactory.Create("strawberry");
            }

            OwnedComponents.Add(sprite);

            if (Winged)
            {
                sprite.Play("flap");
            }

            sprite.OnFrameChange = OnAnimate;
            OwnedComponents.Add(wiggler = process.WigglerPool.Create(0.4f, 4f,
                delegate(float v) { sprite.Scale = Vector2.One * (1f + v * 0.35f); }));
            OwnedComponents.Add(rotateWiggler = process.WigglerPool.Create(0.5f, 4f,
                delegate(float v) { sprite.Rotation = v * 30f * 0.0174532924f; }));
            OwnedComponents.Add(bloom = new BloomPointState(Moon ? 0.5f : 1f, 12f));
            OwnedComponents.Add(light = new VertexLightState(process, Vector2.Zero, PackedColor.White, 1f, 16, 24));
            OwnedComponents.Add(lightTween = light.CreatePulseTween(process));
            if (Seeds != null && Seeds.Count > 0 && !((OriginalLevelSceneRoot)scene).Session.GetFlag(gotSeedFlag))
            {
                foreach (GenericStrawberrySeed seed in Seeds)
                {
                    ((ISourceScene)scene).SourceScene.QueueSourceEntity(seed);
                }

                Visible = false;
                Collidable = false;
                waitingOnSeeds = true;
                bloom.Visible = (light.Visible = false);
            }

            if (((OriginalLevelSceneRoot)scene).Session.BloomBaseAdd > 0.1f)
            {
                bloom.Alpha *= 0.5f;
            }
        }
    }
}
