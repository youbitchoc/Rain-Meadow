﻿using RWCustom;
using System;
using System.Linq;
using UnityEngine;

namespace RainMeadow
{
    public abstract class GroundCreatureController : CreatureController
    {
        public GroundCreatureController(Creature creature, OnlineCreature oc, int playerNumber) : base(creature, oc, playerNumber)
        {
            this._wallClimber = creature.Template.AccessibilityResistance(AItile.Accessibility.Wall).Allowed;
        }

        public float jumpBoost;
        public int forceJump;
        public int canCorridorBoost;
        public int canGroundJump;
        public int canPoleJump;
        public int canClimbJump;
        public int superLaunchJump;

        public abstract bool HasFooting { get; }
        public abstract bool OnGround { get; }
        public abstract bool OnPole { get; }
        public abstract bool OnCorridor { get; }

        private readonly bool _wallClimber;
        protected bool canZeroGClimb;
        public bool WallClimber => _wallClimber || (creature.room.gravity == 0f && canZeroGClimb);

        public Room.Tile GetTile(int bChunk)
        {
            return creature.room.GetTile(creature.room.GetTilePosition(creature.bodyChunks[bChunk].pos));
        }

        public Room.Tile GetTile(int bChunk, int relativeX, int relativeY)
        {
            return creature.room.GetTile(creature.room.GetTilePosition(creature.bodyChunks[bChunk].pos) + new IntVector2(relativeX, relativeY));
        }

        public AItile GetAITile(int bChunk)
        {
            return creature.room.aimap.getAItile(creature.room.GetTilePosition(creature.bodyChunks[bChunk].pos));
        }

        public bool IsTileGround(int bChunk, int relativeX, int relativeY)
        {
            switch (creature.room.GetTile(creature.room.GetTilePosition(creature.bodyChunks[bChunk].pos) + new IntVector2(relativeX, relativeY)).Terrain)
            {
                case Room.Tile.TerrainType.Solid:
                case Room.Tile.TerrainType.Floor:
                case Room.Tile.TerrainType.Slope:
                    return true;
            }
            return false;
        }

        protected abstract void JumpImpl();

        internal override bool FindDestination(WorldCoordinate basecoord, out WorldCoordinate toPos, out float magnitude)
        {
            if (base.FindDestination(basecoord, out toPos, out magnitude)) return true;

            var room = creature.room;
            var chunks = creature.bodyChunks;
            var nc = chunks.Length;

            var template = creature.Template;

            var tile0 = creature.room.GetTile(chunks[0].pos);
            var tile1 = creature.room.GetTile(chunks[1].pos);

            bool localTrace = Input.GetKey(KeyCode.L);

            toPos = WorldCoordinate.AddIntVector(basecoord, IntVector2.FromVector2(this.inputDir.normalized * 1.42f));
            magnitude = 0.5f;
            var previousAccessibility = room.aimap.getAItile(basecoord).acc;

            var currentTile = room.GetTile(basecoord);
            var currentAccessibility = room.aimap.getAItile(toPos).acc;
            var currentLegality = template.AccessibilityResistance(currentAccessibility).legality;

            if (localTrace) RainMeadow.Debug($"moving from {basecoord.Tile} towards {toPos.Tile}");
            
            if (this.forceJump > 0) // jumping
            {
                this.MovementOverride(new MovementConnection(MovementConnection.MovementType.Standard, basecoord, toPos, 2));
                return true;
            }

            // problematic when climbing
            if (this.input[0].y > 0 && (tile0.AnyBeam || tile1.AnyBeam) && !HasFooting)
            {
                RainMeadow.Debug("grip!");
                GripPole(tile0.AnyBeam ? tile0 : tile1);
            }

            // climb
            if (this.input[0].y > 0 && previousAccessibility <= AItile.Accessibility.Climb || currentTile.WaterSurface)
            {
                bool climbing = false;
                for (int i = 0; i < 3; i++)
                {
                    int num = (i > 0) ? ((i == 1) ? -1 : 1) : 0;
                    var tile = room.GetTile(basecoord + new IntVector2(num, 1));
                    var aitile = room.aimap.getAItile(tile.X, tile.Y);
                    if (!tile.Solid && (tile.verticalBeam || aitile.acc == AItile.Accessibility.Climb))
                    {
                        if (localTrace) RainMeadow.Debug("pole close");
                        toPos = WorldCoordinate.AddIntVector(basecoord, new IntVector2(num, 1));
                        climbing = true;
                        break;
                    }
                }
                if(!climbing || inputDir.y > 0.75f) // not found yet, OR pulling the stick hard
                {
                    for (int i = 0; i < 3; i++)
                    {
                        int num = (i > 0) ? ((i == 1) ? -1 : 1) : 0;
                        var tileup1 = room.GetTile(basecoord + new IntVector2(num, 1));
                        var tileup2 = room.GetTile(basecoord + new IntVector2(num, 2));
                        var aitile = room.aimap.getAItile(tileup2.X, tileup2.Y);
                        if (!tileup1.Solid && (tileup2.verticalBeam || aitile.acc == AItile.Accessibility.Climb))
                        {
                            if (localTrace) RainMeadow.Debug("pole far");
                            toPos = WorldCoordinate.AddIntVector(basecoord, new IntVector2(num, 2));
                            climbing = true;
                            break;
                        }
                    }
                }
                if (climbing) return true;
            }

            var targetAccessibility = currentAccessibility;

            // run once at current accessibility level
            // if not found and any higher accessibility level available, run again once
            while (true)
            {
                
                if (this.input[0].x != 0) // to sides
                {
                    if (localTrace) RainMeadow.Debug("sides");
                    if (currentLegality <= PathCost.Legality.Unwanted)
                    {
                        if (localTrace) RainMeadow.Debug("ahead");
                    }
                    else if (room.aimap.TileAccessibleToCreature(toPos.Tile + new IntVector2(0, 1), creature.Template)) // try up
                    {
                        if (localTrace) RainMeadow.Debug("up");
                        toPos = WorldCoordinate.AddIntVector(toPos, new IntVector2(0, 1));
                        currentAccessibility = room.aimap.getAItile(toPos).acc;
                        currentLegality = template.AccessibilityResistance(currentAccessibility).legality;
                    }
                    else if (room.aimap.TileAccessibleToCreature(toPos.Tile + new IntVector2(0, -1), creature.Template)) // try down
                    {
                        if (localTrace) RainMeadow.Debug("down");
                        toPos = WorldCoordinate.AddIntVector(toPos, new IntVector2(0, -1));
                        currentAccessibility = room.aimap.getAItile(toPos).acc;
                        currentLegality = template.AccessibilityResistance(currentAccessibility).legality;
                    }

                    if (inputDir.magnitude > 0.75f)
                    {
                        // if can reach further out, it goes faster and smoother
                        WorldCoordinate furtherOut = WorldCoordinate.AddIntVector(basecoord, IntVector2.FromVector2(this.inputDir.normalized * 3f));
                        if (room.aimap.TileAccessibleToCreature(furtherOut.Tile, creature.Template) && QuickConnectivity.Check(room, creature.Template, basecoord.Tile, furtherOut.Tile, 6) > 0)
                        {
                            if (localTrace) RainMeadow.Debug("reaching further");
                            toPos = furtherOut;
                            magnitude = 1f;
                            currentAccessibility = room.aimap.getAItile(toPos).acc;
                            currentLegality = template.AccessibilityResistance(currentAccessibility).legality;
                        }
                        else if (room.aimap.TileAccessibleToCreature(furtherOut.Tile + new IntVector2(0, 1), creature.Template) && QuickConnectivity.Check(room, creature.Template, basecoord.Tile, furtherOut.Tile + new IntVector2(0, 1), 6) > 0)
                        {
                            if (localTrace) RainMeadow.Debug("further up");
                            toPos = WorldCoordinate.AddIntVector(furtherOut, new IntVector2(0, 1));
                            magnitude = 1f;
                            currentAccessibility = room.aimap.getAItile(toPos).acc;
                            currentLegality = template.AccessibilityResistance(currentAccessibility).legality;
                        }
                        else if (room.aimap.TileAccessibleToCreature(furtherOut.Tile + new IntVector2(0, -1), creature.Template) && QuickConnectivity.Check(room, creature.Template, basecoord.Tile, furtherOut.Tile + new IntVector2(0, -1), 6) > 0)
                        {
                            if (localTrace) RainMeadow.Debug("further down");
                            toPos = WorldCoordinate.AddIntVector(furtherOut, new IntVector2(0, -1));
                            magnitude = 1f;
                            currentAccessibility = room.aimap.getAItile(toPos).acc;
                            currentLegality = template.AccessibilityResistance(currentAccessibility).legality;
                        }
                    }
                }
                else
                {
                    if (localTrace) RainMeadow.Debug("vertical");

                    if (currentLegality <= PathCost.Legality.Unwanted)
                    {
                        if (localTrace) RainMeadow.Debug("ahead");
                    }
                    else if (room.aimap.TileAccessibleToCreature(toPos.Tile + new IntVector2(1, 0), creature.Template)) // right
                    {
                        if (localTrace) RainMeadow.Debug("right");
                        toPos = WorldCoordinate.AddIntVector(toPos, new IntVector2(1, 0));
                        currentAccessibility = room.aimap.getAItile(toPos).acc;
                        currentLegality = template.AccessibilityResistance(currentAccessibility).legality;
                    }
                    else if (room.aimap.TileAccessibleToCreature(toPos.Tile + new IntVector2(-1, 0), creature.Template)) // left
                    {
                        if (localTrace) RainMeadow.Debug("left");
                        toPos = WorldCoordinate.AddIntVector(toPos, new IntVector2(-1, 0));
                        currentAccessibility = room.aimap.getAItile(toPos).acc;
                        currentLegality = template.AccessibilityResistance(currentAccessibility).legality;
                    }
                    if (inputDir.magnitude > 0.75f)
                    {
                        WorldCoordinate furtherOut = WorldCoordinate.AddIntVector(basecoord, IntVector2.FromVector2(this.inputDir.normalized * 2.2f));
                        if (!room.GetTile(toPos).Solid && !room.GetTile(furtherOut).Solid && room.aimap.TileAccessibleToCreature(furtherOut.Tile, creature.Template)) // ahead unblocked, move further
                        {
                            if (localTrace) RainMeadow.Debug("reaching");
                            toPos = furtherOut;
                            magnitude = 1f;
                            currentAccessibility = room.aimap.getAItile(toPos).acc;
                            currentLegality = template.AccessibilityResistance(currentAccessibility).legality;
                        }
                    }
                }

                // ended up unused, need better engineering of "stick to same acc mode unless not available"
                //// any higher accessibilities to check?
                //bool higherAcc = false;
                //while (targetAccessibility < AItile.Accessibility.Solid) higherAcc |= template.AccessibilityResistance(++targetAccessibility).Allowed;
                //if (currentLegality > PathCost.Legality.Unwanted && higherAcc)
                //{
                //    // not found, run again
                //    continue;
                //}
                break;
            }

            if (currentLegality <= PathCost.Legality.Unwanted) // found
            {
                return true;
            }
            else
            {
                // no pathing
                if (localTrace) RainMeadow.Debug("unpathable");
                
                if (!OnPole // don't let go of beams/walls/ceilings
                    && room.aimap.getAItile(toPos).acc < AItile.Accessibility.Solid // no
                    && (input[0].y != 1 || input[0].x != 0)) // not straight up
                {
                    // force movement
                    if (localTrace) RainMeadow.Debug("forced move to " + toPos.Tile);
                    magnitude = 1f;
                    this.MovementOverride(new MovementConnection(MovementConnection.MovementType.Standard, basecoord, toPos, 2));
                    return true;
                }
                else
                {
                    if (localTrace) RainMeadow.Debug("unable to move");
                    return false;
                }
            }
        }

        internal override void ConsciousUpdate()
        {
            base.ConsciousUpdate();
            bool localTrace = UnityEngine.Input.GetKey(KeyCode.L);
            
            if(HasFooting)
            {
                if (OnCorridor)
                {
                    if (localTrace) RainMeadow.Debug("can corridor boost");
                    this.canCorridorBoost = 5;
                }
                if (OnGround)
                {
                    if(localTrace) RainMeadow.Debug("can ground jump");
                    this.canGroundJump = 5;
                }
                else if (OnPole)
                {
                    if (localTrace) RainMeadow.Debug("can pole jump");
                    this.canPoleJump = 5;
                }
                else
                {
                    if (localTrace) RainMeadow.Debug("can climb jump");
                    this.canClimbJump = 5;
                }
            }
            else
            {
                if (localTrace) RainMeadow.Debug("no footing");
            }
            
            if (this.canGroundJump > 0)
            {
                if (this.input[0].jmp && (this.superLaunchJump > 10 || (this.input[0].x == 0 && this.input[0].y <= 0)))
                {
                    if (localTrace) RainMeadow.Debug("charging pounce");
                    this.wantToJump = 0;
                    if (this.superLaunchJump <= 20)
                    {
                        this.superLaunchJump++;
                    }
                    if (this.superLaunchJump > 10)
                    {
                        lockInPlace = true;
                    }
                }
                else if (this.superLaunchJump > 0) this.superLaunchJump--;
                if (!this.input[0].jmp && this.input[1].jmp)
                {
                    if(this.superLaunchJump >= 20)
                    {
                        this.wantToJump = 1;
                    } 
                    else if (this.superLaunchJump <= 10) // regular jump attempt
                    {
                        this.wantToJump = 5;
                    }
                }
            }
            else if (this.superLaunchJump > 0) this.superLaunchJump--;

            if (this.wantToJump > 0 && (this.canClimbJump > 0 || this.canPoleJump > 0 || this.canGroundJump > 0))
            {
                if (localTrace) RainMeadow.Debug("jumping");
                this.JumpImpl();
                this.canClimbJump = 0;
                this.canPoleJump = 0;
                this.canGroundJump = 0;
                this.superLaunchJump = 0;
                this.wantToJump = 0;
            }

            if (this.jumpBoost > 0f && (this.input[0].jmp || this.forceBoost > 0))
            {
                this.jumpBoost -= 1.5f;
                var chunks = creature.bodyChunks;
                var nc = chunks.Length;
                chunks[0].vel.y += (this.jumpBoost + 1f) * 0.3f;
                for (int i = 1; i < nc; i++)
                {
                    chunks[i].vel.y += (this.jumpBoost + 1f) * 0.25f;
                }
            }
            else
            {
                this.jumpBoost = 0f;
            }

            this.flipDirection = GetFlip();
        }

        protected virtual int GetFlip()
        {
            BodyChunk[] chunks = creature.bodyChunks;
            // facing
            if (Mathf.Abs(Vector2.Dot(Vector2.right, (chunks[0].pos - chunks[1].pos).normalized)) > 0.5f)
            {
                return (chunks[0].pos.x > chunks[1].pos.x) ? 1 : -1;
            }
            else if (input[0].x != 0)
            {
                return this.input[0].x;
            }
            else if (Mathf.Abs(specialInput[0].direction.x) > 0.2f)
            {
                return (int)Mathf.Sign(specialInput[0].direction.x);
            }
            return flipDirection;
        }

        protected abstract void GripPole(Room.Tile tile0);
        protected abstract void ClearMovementOverride();
        protected abstract void MovementOverride(MovementConnection movementConnection);

        internal override void Update(bool eu)
        {
            base.Update(eu);

            if (this.canClimbJump > 0) this.canClimbJump--;
            if (this.canPoleJump > 0) this.canPoleJump--;
            if (this.canGroundJump > 0) this.canGroundJump--;
            if (this.forceJump > 0) this.forceJump--;
            if (this.forceBoost > 0) this.forceBoost--;
        }

        public int forceBoost;
    }
}