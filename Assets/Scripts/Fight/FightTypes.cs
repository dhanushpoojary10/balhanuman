using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalHanuman.Fight
{
    public enum FState { Idle, Walk, Crouch, Jump, Block, Attack, Hitstun, Blockstun, Launched, Knockdown, Getup, Dodge, Grabbed, Stunned, Cinematic, Dead }

    public enum Expr { Neutral, Focus, Shout, Hurt, Smirk, Roar, Shocked, Laugh, Furious, Desperate, Dizzy, Serene }

    public enum PowerId { Vayu, Flight, FireTail, Shrink, Vajra, Growth, Panchmukhi }

    /// <summary>Mid hits are blocked standing or crouching, Low only crouching, Overhead only standing, Unblockable never.</summary>
    public enum HitLevel { Mid, Low, Overhead, Unblockable }

    public enum FxKind { Light, Heavy, Fire, Dark, Gold }

    /// <summary>One damaging window inside a move: a circle that follows a puppet joint while t0 &lt;= t &lt; t1.</summary>
    public class HitWindow
    {
        public float t0, t1;
        public Puppet.J joint = Puppet.J.HandF;
        public Vector2 offset;              // in the joint's local space (rig units)
        public float radius = 0.45f;
        public float damage = 40f;
        public Vector2 knock = new Vector2(2.5f, 0f);   // x = away from the attacker
        public float stun = 0.32f;
        public bool knockdown, launch;
        public float hitstop = 0.06f;
        public float shake = 0.04f;
        public HitLevel level = HitLevel.Mid;
        public FxKind fx = FxKind.Light;
        public bool heavy;
        public bool burn;                   // sets the target on fire for a moment
        public bool grab;                   // a grab: on contact the target is held instead of hit
    }

    public class MoveEvent
    {
        public float t; public Action<Fighter> act;
        public MoveEvent(float t, Action<Fighter> a) { this.t = t; act = a; }
    }

    /// <summary>Data for one attack or special: animation, damage windows, root motion and timed events.</summary>
    public class MoveDef
    {
        public string id, name;
        public PuppetClip clip;
        public float cancelFrom = 99f;      // after this time the next command may cancel the move
        public readonly List<HitWindow> hits = new List<HitWindow>();
        public readonly List<MoveEvent> events = new List<MoveEvent>();
        public float lungeSpeed, lungeT0, lungeT1;      // forward root motion
        public float gravityMul = 1f;       // e.g. 0.15 = hang in the air during air combos
        public bool air;                    // starts and stays airborne
        public bool armor;                  // cannot be interrupted
        public bool invulnerable;           // e.g. power activations
        public float invulnT0, invulnT1 = -1f;
        public float meterGain = 0.06f;
        public string sfx;                  // whoosh played at the first hit window
        public Expr expr = Expr.Shout;
        public bool telegraph;              // flashes before it lands (AI only: a cue to parry)
        public MoveDef throwMove;           // a grab that connects switches to this move (the throw)
        public bool endOnLand;              // air moves (dive slam) end the moment the fighter touches the floor
        public Action<Fighter> onLand;      // ...and run this (shockwave etc.)
        public float speedMul = 1f;         // animation/timing speed of this move
        public Puppet.J holdJoint = Puppet.J.HandF;     // where a grabbed opponent is carried during the throw
        public Vector2 holdOffset = new Vector2(0f, -0.1f);

        public float Length => clip != null ? clip.Length : 0.4f;

        public MoveDef Hit(HitWindow h) { hits.Add(h); return this; }
        public MoveDef At(float t, Action<Fighter> a) { events.Add(new MoveEvent(t, a)); events.Sort((x, y) => x.t.CompareTo(y.t)); return this; }
        public MoveDef Lunge(float speed, float t0, float t1) { lungeSpeed = speed; lungeT0 = t0; lungeT1 = t1; return this; }
    }

    public struct HitResult
    {
        public bool connected, blocked, parried, dodged;
    }
}
