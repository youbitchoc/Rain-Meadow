using System.Collections.Generic;
using UnityEngine;

namespace RainMeadow
{
    /*
    [DeltaSupport(level = StateHandler.DeltaSupport.FollowsContainer)]
    public class DaddyTentacleState : OnlineState
    {
        [OnlineFieldHalf]
        public float health;
        [OnlineField]
        public Vector2 pos;
        [OnlineField]
        public byte task;
        [OnlineField(nullable = true)]
        public BodyChunkRef? grabChunk;

        public DaddyTentacleState() { }
    }
    */

    public class RealizedDaddyLongLegsState : RealizedCreatureState
    {
        //[OnlineField]
        //public DaddyTentacleState[] tentacles;
        [OnlineField]
        public List<float> health;
        [OnlineField]
        public List<Vector2> pos;
        [OnlineField]
        public List<byte> task;
        [OnlineField]
        public Vector2 moveDirection;

        public RealizedDaddyLongLegsState() { }
        public RealizedDaddyLongLegsState(OnlineCreature onlineEntity) : base(onlineEntity)
        {
            DaddyLongLegs dll = (DaddyLongLegs)onlineEntity.realizedCreature;
            //tentacles = new DaddyTentacleState[dll.tentacles.Length];
            health = new();
            pos = new();
            task = new();
            for (var i = 0; i < dll.tentacles.Length; i++)
            {
                var tentacle = dll.tentacles[i];
                /*
                tentacles[i] = new();
                tentacles[i].pos = tentacle.Tip.pos;
                tentacles[i].task = (byte)tentacle.task;
                tentacles[i].grabChunk = BodyChunkRef.FromBodyChunk(tentacle.grabChunk);
                tentacles[i].health = (dll.State as DaddyLongLegs.DaddyState)?.tentacleHealth?[i] ?? 1f;
                */
                health.Add(((DaddyLongLegs.DaddyState)dll.State).tentacleHealth[i]);
                pos.Add(tentacle.Tip.pos);
                task.Add((byte)tentacle.task);
            }
        }

        public override void ReadTo(OnlineEntity onlineEntity)
        {
            base.ReadTo(onlineEntity);
            if ((onlineEntity as OnlineCreature).apo.realizedObject is not DaddyLongLegs dll)
            {
                RainMeadow.Error("target not realized: " + onlineEntity);
                return;
            }

            for (var i = 0; i < health.Count; i++)
            {
                ((DaddyLongLegs.DaddyState)dll.State).tentacleHealth[i] = health[i];
                var tentacle = dll.tentacles[i];
                tentacle.Tip.pos = pos[i];
                tentacle.task = new DaddyTentacle.Task(DaddyTentacle.Task.values.GetEntry(task[i]));
                //tentacle.grabChunk = tentacles[i].grabChunk?.ToBodyChunk();
            }
        }
    }
}

