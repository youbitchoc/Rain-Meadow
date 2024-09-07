﻿using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using UnityEngine;

namespace RainMeadow
{
    public class EggbugController : GroundCreatureController
    {
        public static void EnableEggbug()
        {
            On.EggBug.Update += EggBug_Update;
            On.EggBug.Act += EggBug_Act;
            On.EggBug.Swim += EggBug_Swim;

            On.EggBugAI.Update += EggBugAI_Update;
            On.EggBug.Run += EggBug_Run;

            IL.EggBug.Swim += EggBug_Swim1;
            IL.EggBug.Update += EggBug_Update1;
        }

        private static void EggBug_Update1(ILContext il)
        {
            try
            {
                var c = new ILCursor(il);

                // if (base.graphicsModule != null && this.room != null etc etc
                // then do the leg pulling thing if out-of-medium
                // instead skip that if controlled and submerged

                ILLabel skip = null;
                c.GotoNext(i => i.MatchCallOrCallvirt<InsectoidCreature>("Update"));
                c.GotoNext(moveType: MoveType.After,
                    i => i.MatchLdarg(0),
                    i => i.MatchCall<PhysicalObject>("get_graphicsModule"),
                    i => i.MatchBrfalse(out skip)
                    );
                c.MoveAfterLabels();
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((EggBug self) => {
                    if (creatureControllers.TryGetValue(self, out var controller))
                    {
                        if (self.mainBodyChunk.submersion >= 1)
                        {
                            return false;
                        }
                    }
                    return true;
                });
                c.Emit(OpCodes.Brfalse, skip);
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        private static void EggBug_Swim1(ILContext il)
        {
            try
            {
                var c = new ILCursor(il);

                // tricky to describe but decompiled code says "if (!(movementConnection != default(movementConnection)))"
                // but in practice it's the opposite, just a much longer if body
                // if ((movementConnection != default(MovementConnection)))
                // becomes
                // if (meadowOverride || (movementConnection != default(MovementConnection)))

                c.GotoNext(i => i.MatchCallOrCallvirt<PhysicalObject>("get_graphicsModule"));
                c.GotoPrev(moveType: MoveType.After,
                    i => i.MatchLdloc(0),
                    i => i.MatchLdloca(1),
                    i => i.MatchInitobj<MovementConnection>(),
                    i => i.MatchLdloc(1),
                    i => i.MatchCall(out _),
                    i => i.MatchBrfalse(out _)
                    );
                var skip = c.MarkLabel();
                c.Index -= 6;
                c.MoveAfterLabels();
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloca, 0);
                c.EmitDelegate((EggBug self, ref MovementConnection movementConnection) => {
                    if (creatureControllers.TryGetValue(self, out var controller))
                    {
                        var coord = controller.creature.coord;
                        var to = controller.creature.abstractCreature.abstractAI.RealAI.pathFinder.destination;
                        movementConnection = new MovementConnection(MovementConnection.MovementType.Standard, coord, to, 1);

                        if(self.mainBodyChunk.submersion >= 1)
                        {
                            self.mainBodyChunk.vel += 1.2f * controller.inputDir; // here some help
                        }

                        if (Input.GetKey(KeyCode.L)) RainMeadow.Debug("overriding");
                        return false;
                    }
                    return true;
                });
                c.Emit(OpCodes.Brfalse, skip);
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        private static void EggBug_Swim(On.EggBug.orig_Swim orig, EggBug self)
        {
            if (Input.GetKey(KeyCode.L)) RainMeadow.Debug("swim");
            orig(self);
        }

        private static void EggBug_Run(On.EggBug.orig_Run orig, EggBug self, MovementConnection followingConnection)
        {
            if (Input.GetKey(KeyCode.L)) RainMeadow.Debug("run: " + followingConnection);
            if (creatureControllers.TryGetValue(self, out var c))
            {
                if (followingConnection.startCoord == self.abstractCreature.abstractAI.destination)
                    return;
            }
            orig(self, followingConnection);
        }

        private static void EggBugAI_Update(On.EggBugAI.orig_Update orig, EggBugAI self)
        {
            if (creatureControllers.TryGetValue(self.creature.realizedCreature, out var p))
            {
                p.AIUpdate(self);
            }
            else
            {
                orig(self);
            }
        }

        private static void EggBug_Act(On.EggBug.orig_Act orig, EggBug self)
        {
            if (creatureControllers.TryGetValue(self, out var p))
            {
                p.ConsciousUpdate();
            }
            orig(self);
        }

        private static void EggBug_Update(On.EggBug.orig_Update orig, EggBug self, bool eu)
        {
            if (creatureControllers.TryGetValue(self, out var p))
            {
                p.Update(eu);
            }
            orig(self, eu);
        }

        public EggbugController(EggBug creature, OnlineCreature oc, int playerNumber, MeadowAvatarCustomization customization) : base(creature, oc, playerNumber, customization)
        {
            if (creature.grasps == null) creature.grasps = new Creature.Grasp[1];
            eggbug = creature;

            jumpFactor = 1.2f;
        }

        EggBug eggbug;

        public override bool HasFooting => eggbug.Footing;
        public override bool OnGround => IsTileGround(0, 0, -1) || IsTileGround(1, 0, -1);
        public override bool OnPole => HasFooting && !OnGround && GetTile(0).AnyBeam;
        public override bool OnCorridor => eggbug.currentlyClimbingCorridor;

        protected override void GripPole(Room.Tile tile0)
        {
            if (eggbug.footingCounter < 10)
            {
                creature.room.PlaySound(SoundID.Egg_Bug_Scurry, creature.mainBodyChunk);
                for (int i = 0; i < creature.bodyChunks.Length; i++)
                {
                    creature.bodyChunks[i].vel *= 0.25f;
                }
                creature.mainBodyChunk.vel += 0.2f * (creature.room.MiddleOfTile(tile0.X, tile0.Y) - creature.mainBodyChunk.pos);
                eggbug.footingCounter = 20;
            }
        }

        protected override void OnJump()
        {
            eggbug.footingCounter = 0;
        }

        protected override void MovementOverride(MovementConnection movementConnection)
        {
            eggbug.specialMoveCounter = 15;
            eggbug.specialMoveDestination = movementConnection.DestTile;
        }

        protected override void ClearMovementOverride()
        {
            eggbug.specialMoveCounter = 0;
        }

        protected override void LookImpl(Vector2 pos)
        {
            var dir = Custom.DirVec(eggbug.mainBodyChunk.pos, pos);
            eggbug.travelDir = dir;
            eggbug.bodyChunks[0].vel += dir;
            eggbug.bodyChunks[1].vel -= dir;
        }

        protected override void Moving(float magnitude)
        {
            eggbug.runSpeed = Custom.LerpAndTick(eggbug.runSpeed, magnitude, 0.2f, 0.05f);
        }

        protected override void Resting()
        {
            eggbug.runSpeed = Custom.LerpAndTick(eggbug.runSpeed, 0, 0.4f, 0.1f);
        }

        internal override void ConsciousUpdate()
        {
            base.ConsciousUpdate();
            if (eggbug.specialMoveCounter > 0 && !eggbug.room.aimap.TileAccessibleToCreature(eggbug.mainBodyChunk.pos, eggbug.Template) && !eggbug.room.aimap.TileAccessibleToCreature(eggbug.bodyChunks[1].pos, eggbug.Template))
            {
                eggbug.footingCounter = 0;
            }
            if (superLaunchJump > 10 && (eggbug.room.aimap.getAItile(eggbug.bodyChunks[1].pos).acc == AItile.Accessibility.Floor && !eggbug.IsTileSolid(0, 0, 1) && !eggbug.IsTileSolid(1, 0, 1)))
            {
                eggbug.mainBodyChunk.vel.y -= 1f;
                eggbug.mainBodyChunk.vel.x += flipDirection;
                eggbug.bodyChunks[1].vel.y += 1f;
                eggbug.bodyChunks[1].vel.x -= flipDirection;
            }
        }

        protected override void OnCall()
        {
            if(eggbug.graphicsModule is EggBugGraphics ebg && !ebg.culled)
            {
                Vector2 tohead = Custom.DirVec(eggbug.bodyChunks[1].pos, eggbug.bodyChunks[0].pos);
                for (int i = 0; i < ebg.legs.GetLength(0); i++)
                {
                    ebg.legs[0, i].vel += tohead;
                }
                eggbug.shake = Math.Max(eggbug.shake, 5);
                if (voice.Display) ebg.Squirt(voice.Volume * 0.33f);
            }
        }
    }
}
