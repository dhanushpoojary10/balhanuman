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

    /// <summary>Boss tell colours: yellow = blockable, red = grab / unblockable, purple = projectile or spell.</summary>
    public enum TeleClass { None, Blockable, Grab, Projectile }

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
        public int priority = -1;           // clash order: 0 normal, 1 heavy, 2 armored, 3 unblockable. -1 = derived from the window (unblockable 3, heavy 1, else 0)
        public int Prio => priority >= 0 ? priority : (level == HitLevel.Unblockable ? 3 : heavy ? 1 : 0);
        /// <summary>Impact tier from the window itself: 0 light, 1 medium, 2 heavy (launch / knockdown / big heavy), 3 ultimate. Drives hit-stop, shake, camera, flash, sound, haptics and effect size.</summary>
        public int Tier => Prio >= 3 || damage >= 200f ? 3 : (launch || knockdown || (heavy && damage >= 60f)) ? 2 : (damage >= 40f || heavy || knock.x >= 3f) ? 1 : 0;
        public bool frameTip;               // frame-art moves: the hit circle sits on the weapon / foot / tail end measured from the current frame (+ offset.x forward, offset.y up)
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
        public int armorHits;               // absorbs this many hits (at half damage, no reaction) once armorStart has passed; 99 = cannot be interrupted
        public float armorStart;
        public bool armor { get => armorHits > 0; set => armorHits = value ? 99 : 0; }     // legacy flag: fully armored
        public float endLag;                // extra recovery after the animation ends (the last picture holds): commitment without new art
        public float whiffLag = 0.12f;      // extra wait before the next command may cancel this move when it connected with nothing
        public float holdAt, holdLen;       // boss tells: the move clock pauses at holdAt for holdLen seconds (the wind-up picture holds)
        public TeleClass tele;              // colour/sound of the tell shown when the hold starts
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

        // frame-by-frame art: this move plays frames [frameFrom..frameTo] of HanumanFrames clip `frameClip` (the skeleton is only a fallback)
        public string frameClip; public int frameFrom, frameTo = -1;
        public bool frameLoop;              // visual only: the move keeps its authored length instead of being timed by the frames. By default the frames cycle
        public bool airStall;               // air attacks: starting the move tames the vertical speed (no free lift, no dive-bomb) so it floats, then falls
        public bool frameHold;              // (with frameLoop) play the frames once at the clip's fps, then hold the last one
        public bool frameStretch;           // (with frameLoop) spread the frames over the move's whole authored length
        public Vector3[] holdPath;          // frame-art throws: x = time into the move, (y, z) = held opponent's centre (forward, up) from the fighter's root

        public float Length
        {
            get
            {
                if (frameClip != null && !frameLoop)
                {
                    var c = HanumanFrames.Get(frameClip);
                    if (c != null) { int to = frameTo >= 0 ? frameTo : c.frames.Length - 1; return (to - frameFrom + 1) / c.fps; }
                }
                return clip != null ? clip.Length : 0.4f;
            }
        }

        public MoveDef Hit(HitWindow h) { hits.Add(h); return this; }
        public MoveDef At(float t, Action<Fighter> a) { events.Add(new MoveEvent(t, a)); events.Sort((x, y) => x.t.CompareTo(y.t)); return this; }
        public MoveDef Lunge(float speed, float t0, float t1) { lungeSpeed = speed; lungeT0 = t0; lungeT1 = t1; return this; }
    }

    public struct HitResult
    {
        public bool connected, blocked, parried, dodged, clashed;
    }
}
