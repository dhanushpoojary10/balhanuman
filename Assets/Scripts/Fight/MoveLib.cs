using System.Collections.Generic;
using UnityEngine;
using J = BalHanuman.Fight.Puppet.J;

namespace BalHanuman.Fight
{
    /// <summary>
    /// Every animation and attack, authored as key poses (anticipation -> strike -> follow-through -> recovery).
    /// Angles: limbs 0 = hanging down, + = forward/up; Lean(torso, hips) + = forward. Feet(...) plants feet with IK.
    /// </summary>
    public static class MoveLib
    {
        static Puppet.Dims D;
        static Pose P() => new Pose();
        static (float, Pose, Ease) K(float t, Pose p, Ease e = Ease.Smooth) => (t, p, e);
        static PuppetClip C(bool loop, float len, params (float, Pose, Ease)[] k) => PuppetClip.Make(D, loop, len, k);
        static FightDirector Dir => FightDirector.I;

        static HitWindow H(float t0, float t1, J joint, Vector2 off, float r, float dmg, Vector2 knock, float stun = 0.32f,
            HitLevel lvl = HitLevel.Mid, FxKind fx = FxKind.Light, bool heavy = false, bool kd = false, bool launch = false,
            float hitstop = 0.06f, float shake = 0.04f, bool burn = false, bool grab = false)
        {
            return new HitWindow
            {
                t0 = t0, t1 = t1, joint = joint, offset = off, radius = r, damage = dmg, knock = knock, stun = stun, level = lvl, fx = fx,
                heavy = heavy, knockdown = kd, launch = launch, hitstop = hitstop, shake = shake, burn = burn, grab = grab
            };
        }

        static MoveDef M(string id, string name, PuppetClip clip) => new MoveDef { id = id, name = name, clip = clip };

        // ======================================================================================= HANUMAN
        public static Puppet.Dims HanumanDims() => new Puppet.Dims();

        public static Puppet.Style HanumanStyle() => new Puppet.Style
        {
            skin = new Color(0.93f, 0.58f, 0.27f), face = new Color(0.99f, 0.8f, 0.62f), cloth = new Color(0.86f, 0.2f, 0.1f),
            cloth2 = new Color(1f, 0.58f, 0.12f), gold = new Color(1f, 0.8f, 0.25f), hair = new Color(0.14f, 0.08f, 0.06f),
            tail = true, muzzle = true, weapon = 1, bulk = 1.08f
        };

        static Pose HStance() => P().Lean(8, 4).Head(-6).Hip(0, -0.12f).Feet(0.4f, -0.32f).ArmF(40, 70).Wpn(60).ArmB(35, 85).Tail(-100);

        static Pose HWalk(float ff, float fy, float bf, float by, float hy, float lean) =>
            HStance().Hip(0.04f, hy).Feet(ff, bf, fy, by).Lean(lean, 4).ArmF(45, 70).ArmB(30, 80);

        public static Dictionary<string, MoveDef> Hanuman(Dictionary<string, PuppetClip> clips)
        {
            D = HanumanDims();
            var m = new Dictionary<string, MoveDef>();
            Pose S() => HStance();
            float L = D.weaponLen;
            var tip = new Vector2(0, -L);

            // ---------------------------------------------------------------- base clips
            clips["idle"] = C(true, 1.3f, K(0, S()), K(0.65f, S().Hip(0, -0.17f).Lean(10, 5).ArmF(44, 74).ArmB(38, 90).Head(-4).Tail(-92)));
            var s0 = HWalk(0.42f, 0, -0.36f, 0, -0.16f, 12); var s1 = HWalk(0.03f, 0, 0.0f, 0.18f, -0.08f, 12);
            var s2 = HWalk(-0.36f, 0, 0.42f, 0, -0.16f, 12); var s3 = HWalk(0.0f, 0.18f, 0.03f, 0, -0.08f, 12);
            clips["walkf"] = C(true, 0.56f, K(0, s0, Ease.Linear), K(0.14f, s1, Ease.Linear), K(0.28f, s2, Ease.Linear), K(0.42f, s3, Ease.Linear));
            var b0 = HWalk(0.42f, 0, -0.36f, 0, -0.16f, 4); var b1 = HWalk(0.03f, 0, 0.0f, 0.18f, -0.08f, 4);
            var b2 = HWalk(-0.36f, 0, 0.42f, 0, -0.16f, 4); var b3 = HWalk(0.0f, 0.18f, 0.03f, 0, -0.08f, 4);
            clips["walkb"] = C(true, 0.62f, K(0, b0, Ease.Linear), K(0.155f, b3, Ease.Linear), K(0.31f, b2, Ease.Linear), K(0.465f, b1, Ease.Linear));
            var crouch = S().Hip(0, -0.55f).Feet(0.48f, -0.42f).Lean(22, 6).ArmF(30, 80).Wpn(70).ArmB(30, 95).Tail(-80);
            clips["crouch"] = C(true, 1f, K(0, crouch), K(0.5f, crouch.Copy().Hip(0, -0.58f).Lean(24, 6)));
            var block = S().Lean(-2, 0).Hip(-0.05f, -0.16f).ArmF(70, 60).Wpn(55).ArmB(62, 95).Head(4).Tail(-120);
            clips["block"] = C(true, 1f, K(0, block), K(0.5f, block.Copy().Hip(-0.05f, -0.18f)));
            var cblock = crouch.Copy().Lean(8, 4).ArmF(75, 55).Wpn(55).ArmB(65, 95).Head(6);
            clips["crouchblock"] = C(true, 1f, K(0, cblock), K(0.5f, cblock));
            clips["jump"] = C(false, 0.3f, K(0, S().Hip(0, -0.3f)), K(0.12f, P().Lean(10, 0).Head(-10).LegF(75, -105).LegB(25, -70).ArmF(110, 40).Wpn(40).ArmB(120, 30).Tail(-70)));
            clips["fall"] = C(true, 0.8f, K(0, P().Lean(4).LegF(35, -45).LegB(-10, -35).ArmF(70, 40).Wpn(60).ArmB(95, 20).Tail(-120)),
                K(0.4f, P().Lean(6).LegF(30, -40).LegB(-14, -30).ArmF(75, 35).Wpn(62).ArmB(100, 15).Tail(-125)));
            var recoil = S().Lean(-22, -8).Head(-25).Hip(-0.12f, -0.1f).ArmF(-15, 40).Wpn(80).ArmB(-25, 30).Tail(-60);
            clips["hit"] = C(false, 0.42f, K(0, recoil, Ease.Out), K(0.1f, recoil.Copy().Lean(-26, -10).Head(-30)), K(0.42f, S()));
            var hitLow = S().Lean(35, 10).Head(20).Hip(0.05f, -0.3f).ArmF(20, 60).ArmB(10, 50);
            clips["hitlow"] = C(false, 0.42f, K(0, hitLow, Ease.Out), K(0.12f, hitLow), K(0.42f, S()));
            var flail0 = P().Lean(-30, -20).Head(-30).LegF(50, -70).LegB(-10, -40).ArmF(150, 30).Wpn(30).ArmB(170, 20).Tail(-60);
            var flail1 = P().Lean(-35, -25).Head(-35).LegF(40, -90).LegB(0, -20).ArmF(130, 50).Wpn(40).ArmB(190, 10).Tail(-40);
            clips["launched"] = C(true, 0.5f, K(0, flail0), K(0.25f, flail1));
            var down = P().Lean(0, -88).Head(-10).Hip(0.2f, 0.32f - D.HipBase).LegF(8, -15).LegB(14, -25).ArmF(-60, 10).Wpn(90).ArmB(-80, 0).Tail(20);
            clips["down"] = C(true, 1f, K(0, down), K(0.5f, down.Copy().Lean(-3, -88)));
            var sit = P().Lean(35, -50).Head(10).Hip(0, 0.45f - D.HipBase).LegF(60, -120).LegB(40, -100).ArmF(-30, 40).Wpn(80).ArmB(-50, 20).Tail(-40);
            clips["getup"] = C(false, 0.42f, K(0, down), K(0.16f, sit), K(0.3f, crouch), K(0.42f, S()));
            var tuck = P().Lean(30, 0).Head(15).Hip(0, 0.25f).LegF(100, -130).LegB(90, -130).ArmF(60, 100).Wpn(40).ArmB(60, 100).Tail(-40);
            clips["dodgeb"] = C(false, 0.38f, K(0, S().Hip(0, -0.3f)), K(0.06f, tuck), K(0.26f, tuck.Copy().Lean(30, -300)), K(0.38f, S().Lean(8, -356)));
            var rollTuck = tuck.Copy().Hip(0, -0.45f);
            clips["dodgef"] = C(false, 0.38f, K(0, S().Hip(0, -0.3f)), K(0.06f, rollTuck), K(0.28f, rollTuck.Copy().Lean(30, 300)), K(0.38f, S().Lean(8, 364)));
            var dz0 = S().Lean(-8, -4).Head(-15).Hip(-0.05f, -0.2f).ArmF(10, 20).Wpn(100).ArmB(5, 15).Tail(-50);
            clips["stunned"] = C(true, 1.0f, K(0, dz0), K(0.5f, dz0.Copy().Lean(6, 4).Head(12).Hip(0.06f, -0.22f)));
            var vic = P().Feet(0.3f, -0.3f).Hip(0, -0.05f).Lean(-4).Head(-12).ArmF(20, 140).Wpn(36).ArmB(160, -40).Tail(-160);
            clips["victory"] = C(true, 1.6f, K(0, vic), K(0.8f, vic.Copy().Hip(0, -0.09f).Head(-8).Tail(-150)));
            var roar = S().Lean(-18, -6).Head(-28).Hip(0, -0.25f).Feet(0.5f, -0.45f).ArmF(90, 95).Wpn(90).ArmB(85, 100).Tail(-160);
            clips["roar"] = C(true, 0.5f, K(0, roar), K(0.25f, roar.Copy().Hip(0, -0.27f).Head(-30)));
            var fly0 = P().Lean(55, 15).Head(-35).Hip(0, 0).LegF(-15, -25).LegB(-30, -15).ArmF(150, 10).Wpn(0).ArmB(60, 40).Tail(-170);
            clips["fly"] = C(true, 1.0f, K(0, fly0), K(0.5f, fly0.Copy().Lean(52, 15).LegF(-10, -35).LegB(-35, -10)));
            var kneel = P().Hip(0, 0.72f - D.HipBase).Feet(0.45f, 0f).LegB(-10, -100).Lean(25, 0).Head(15).ArmF(40, 60).Wpn(80).ArmB(60, 40).Tail(-60);
            clips["kneel"] = C(true, 1.2f, K(0, kneel), K(0.6f, kneel.Copy().Lean(28, 0)));
            var charge = S().Lean(-10, -4).Hip(-0.1f, -0.25f).Feet(0.5f, -0.4f).ArmF(170, 40).Wpn(60).ArmB(160, 30).Tail(-150).Head(-15);
            clips["charge"] = C(true, 0.24f, K(0, charge), K(0.12f, charge.Copy().Hip(-0.08f, -0.27f).ArmF(172, 42)));

            // ---------------------------------------------------------------- mace (gada)
            var m1w = S().Lean(2, 0).Hip(-0.06f, -0.14f).ArmF(-60, 110).Wpn(-150).ArmB(20, 60).Head(-4);
            var m1s = S().Lean(20, 6).Hip(0.12f, -0.2f).Feet(0.62f, -0.3f).ArmF(85, 10).Wpn(21).ArmB(-10, 40);
            var m1f = m1s.Copy().Lean(24, 8).ArmF(115, 0).Wpn(55);
            m["m1"] = M("m1", "Gada Swing", C(false, 0.42f, K(0, m1w), K(0.07f, m1w, Ease.Out), K(0.13f, m1s, Ease.Out), K(0.22f, m1f), K(0.42f, S())))
                .Hit(H(0.09f, 0.17f, J.Weapon, tip, 0.55f, 45, new Vector2(1.2f, 0), 0.38f)).Lunge(3f, 0.02f, 0.12f);
            m["m1"].cancelFrom = 0.16f; m["m1"].sfx = "whoosh";

            var m2w = S().Lean(25, 8).Hip(0, -0.3f).ArmF(10, 15).Wpn(-160).ArmB(40, 70);
            var m2s = S().Lean(-8, -4).Hip(0.1f, -0.08f).Feet(0.6f, -0.3f).ArmF(150, 10).Wpn(10).ArmB(20, 40).Head(-12);
            m["m2"] = M("m2", "Rising Backhand", C(false, 0.44f, K(0, m2w), K(0.08f, m2w, Ease.Out), K(0.14f, m2s, Ease.Out), K(0.22f, m2s.Copy().ArmF(170, 5).Wpn(30).Lean(-12, -4)), K(0.44f, S())))
                .Hit(H(0.09f, 0.18f, J.Weapon, tip, 0.6f, 50, new Vector2(1.4f, 0), 0.4f)).Lunge(3.5f, 0.04f, 0.13f);
            m["m2"].cancelFrom = 0.17f;

            var m3w = S().Lean(-18, -8).Hip(-0.1f, -0.02f).ArmF(175, 30).Wpn(60).ArmB(170, 20).Head(-15);
            var m3s = S().Lean(38, 14).Hip(0.2f, -0.42f).Feet(0.75f, -0.35f).ArmF(70, 0).Wpn(50).ArmB(60, 10).Head(10);
            m["m3"] = M("m3", "Earth-Splitter Smash", C(false, 0.62f, K(0, m3w), K(0.16f, m3w.Copy().Hip(-0.12f, 0f), Ease.In), K(0.24f, m3s, Ease.Out), K(0.4f, m3s), K(0.62f, S())))
                .Hit(H(0.2f, 0.28f, J.Weapon, tip, 0.7f, 80, new Vector2(3.5f, 6), 0.5f, HitLevel.Overhead, FxKind.Heavy, true, true, false, 0.12f, 0.16f))
                .At(0.24f, f => { var p = f.WeaponPoint; FightFx.Shockwave(new Vector3(p.x, 0, 0), 1.6f * f.scale, new Color(1f, 0.8f, 0.4f)); FightSfx.Play("slam", 1.1f, 0.8f); })
                .Lunge(4f, 0.12f, 0.24f);
            m["m3"].sfx = "whoosh_heavy"; m["m3"].cancelFrom = 0.45f;

            var thw = S().Lean(-6, 0).Hip(-0.15f, -0.15f).ArmF(-20, 120).Wpn(-30).ArmB(30, 80);
            var ths = S().Lean(25, 8).Hip(0.3f, -0.25f).Feet(0.8f, -0.4f).ArmF(95, -5).Wpn(33).ArmB(-20, 20).Tail(-130);
            m["thrust"] = M("thrust", "Vajra Thrust", C(false, 0.5f, K(0, thw), K(0.09f, thw, Ease.Out), K(0.17f, ths, Ease.Out), K(0.3f, ths), K(0.5f, S())))
                .Hit(H(0.12f, 0.24f, J.Weapon, tip, 0.6f, 55, new Vector2(6f, 1f), 0.4f, HitLevel.Mid, FxKind.Heavy, true)).Lunge(7.5f, 0.08f, 0.2f);
            m["thrust"].cancelFrom = 0.3f;

            var wh = new List<(float, Pose, Ease)>();
            float[] arm = { 0, 60, 150, 240, 330, 420 };
            for (int i = 0; i < arm.Length; i++) wh.Add(K(i * 0.1f, S().Lean(5, 0).Hip(0, -0.2f).Feet(0.5f, -0.4f).ArmF(arm[i], 0).Wpn(0).ArmB(arm[i] - 160, 20).Tail(-100 + i * 30), Ease.Linear));
            wh.Add(K(0.7f, S()));
            m["whirl"] = M("whirl", "Whirlwind Gada", C(false, 0.7f, wh.ToArray()))
                .Hit(H(0.06f, 0.2f, J.Weapon, tip, 0.8f, 40, new Vector2(2f, 2f), 0.4f))
                .Hit(H(0.3f, 0.46f, J.Weapon, tip, 0.8f, 45, new Vector2(3.5f, 4f), 0.45f, HitLevel.Mid, FxKind.Heavy, true, true));
            m["whirl"].sfx = "whoosh_heavy"; m["whirl"].cancelFrom = 0.55f;

            var lw = S().Lean(20, 8).Hip(0, -0.45f).Feet(0.45f, -0.4f).ArmF(30, 10).Wpn(18).ArmB(30, 60);
            var ls = S().Lean(-10, -6).Hip(0, 0.05f).Feet(0.3f, -0.2f, 0, 0.1f).ArmF(175, 0).Wpn(0).ArmB(120, 30).Head(-25);
            var lair = P().Lean(-5, 0).Head(-20).LegF(60, -90).LegB(10, -40).ArmF(170, 10).Wpn(10).ArmB(110, 40).Tail(-60);
            m["launcher"] = M("launcher", "Sky Launcher", C(false, 0.55f, K(0, lw), K(0.1f, lw.Copy().Hip(0, -0.5f), Ease.Out), K(0.18f, ls, Ease.Out), K(0.3f, lair), K(0.55f, lair)))
                .Hit(H(0.12f, 0.24f, J.Weapon, tip, 0.75f, 50, new Vector2(1.2f, 15.5f), 0.5f, HitLevel.Mid, FxKind.Heavy, true, false, true, 0.09f, 0.08f))
                .At(0.17f, f => { f.vel.y = 13.5f; f.grounded = false; FightFx.Dust(f.transform.position, 5, 2f); });
            m["launcher"].cancelFrom = 0.3f;

            var sww = S().Lean(30, 10).Hip(-0.05f, -0.6f).Feet(0.5f, -0.45f).ArmF(-10, 20).Wpn(-80).ArmB(20, 60).Tail(-60);
            var sws = sww.Copy().Hip(0.1f, -0.62f).ArmF(70, 10).Wpn(45).ArmB(-20, 30);
            m["sweep"] = M("sweep", "Ground Sweep", C(false, 0.5f, K(0, sww), K(0.08f, sww, Ease.Out), K(0.16f, sws, Ease.Out), K(0.28f, sws), K(0.5f, S())))
                .Hit(H(0.1f, 0.2f, J.Weapon, tip, 0.65f, 45, new Vector2(2f, 5f), 0.4f, HitLevel.Low, FxKind.Light, false, true));
            m["sweep"].cancelFrom = 0.3f;

            var chs = S().Lean(40, 15).Hip(0.25f, -0.48f).Feet(0.8f, -0.38f).ArmF(65, 0).Wpn(55).ArmB(55, 10).Head(12).Tail(-170);
            for (int lvl = 1; lvl <= 2; lvl++)
            {
                float big = lvl == 2 ? 1.5f : 1f;
                string id = "charge" + lvl;
                m[id] = M(id, lvl == 2 ? "Mountain-Breaker (MAX)" : "Mountain-Breaker", C(false, 0.7f, K(0, charge, Ease.Out), K(0.12f, chs, Ease.Out), K(0.4f, chs), K(0.7f, S())))
                    .Hit(H(0.08f, 0.2f, J.Weapon, tip, 1.0f * big, 90 * big, new Vector2(5f, 8f), 0.6f, HitLevel.Overhead, FxKind.Gold, true, true, false, 0.14f, 0.22f * big))
                    .At(0.12f, f => { var p = f.WeaponPoint; FightFx.Shockwave(new Vector3(p.x, 0, 0), 2.6f * big * f.scale, new Color(1f, 0.85f, 0.35f)); FightSfx.Play("slam", 0.9f); Dir?.GroundPound(f, p.x, 2.2f * big * f.scale, 35 * big, false); })
                    .Lunge(5f, 0f, 0.1f);
                m[id].sfx = "whoosh_heavy"; m[id].expr = Expr.Roar;
            }

            // ---------------------------------------------------------------- air
            var airLegs = P().LegF(50, -80).LegB(10, -50).Tail(-60);
            Pose A() => airLegs.Copy();
            var a1w = A().Lean(5).ArmF(-40, 90).Wpn(-120).ArmB(40, 60);
            var a1s = A().Lean(18).ArmF(90, 10).Wpn(10).ArmB(-10, 40);
            m["air1"] = M("air1", "Air Swing", C(false, 0.32f, K(0, a1w), K(0.05f, a1w, Ease.Out), K(0.1f, a1s, Ease.Out), K(0.32f, a1s.Copy().ArmF(110, 0))))
                .Hit(H(0.06f, 0.14f, J.Weapon, tip, 0.65f, 35, new Vector2(1.5f, 4.5f), 0.4f)).At(0f, f => { f.vel.y = Mathf.Max(f.vel.y, 2.5f); });
            m["air1"].air = true; m["air1"].gravityMul = 0.12f; m["air1"].cancelFrom = 0.14f;
            var a2w = A().Lean(20).ArmF(20, 10).Wpn(-160).ArmB(60, 40);
            var a2s = A().Lean(-8).ArmF(160, 0).Wpn(10).ArmB(10, 40);
            m["air2"] = M("air2", "Air Backhand", C(false, 0.34f, K(0, a2w), K(0.05f, a2w, Ease.Out), K(0.11f, a2s, Ease.Out), K(0.34f, a2s)))
                .Hit(H(0.06f, 0.15f, J.Weapon, tip, 0.65f, 38, new Vector2(1.5f, 5.5f), 0.4f)).At(0f, f => { f.vel.y = Mathf.Max(f.vel.y, 2.5f); });
            m["air2"].air = true; m["air2"].gravityMul = 0.12f; m["air2"].cancelFrom = 0.15f;
            var a3w = A().Lean(-15).ArmF(180, 30).Wpn(60).ArmB(170, 20);
            var a3s = A().Lean(30).ArmF(60, 0).Wpn(40).ArmB(50, 10);
            m["air3"] = M("air3", "Meteor Spike", C(false, 0.45f, K(0, a3w), K(0.08f, a3w, Ease.In), K(0.14f, a3s, Ease.Out), K(0.45f, a3s)))
                .Hit(H(0.1f, 0.2f, J.Weapon, tip, 0.75f, 60, new Vector2(2f, -12f), 0.5f, HitLevel.Overhead, FxKind.Heavy, true, false, true, 0.11f, 0.12f));
            m["air3"].air = true; m["air3"].gravityMul = 0.2f; m["air3"].sfx = "whoosh_heavy"; m["air3"].cancelFrom = 0.3f;

            var dvw = A().Lean(35, 10).ArmF(170, 20).Wpn(70).ArmB(160, 20);
            var dvs = P().Lean(45, 10).LegF(70, -110).LegB(40, -100).ArmF(90, 0).Wpn(80).ArmB(80, 10).Tail(-170);
            foreach (var dive in new[] { ("dive", "Diving Slam", 1f), ("meteor", "Meteor Dive", 1.6f) })
            {
                float k = dive.Item3;
                m[dive.Item1] = M(dive.Item1, dive.Item2, C(false, 1.2f, K(0, dvw), K(0.08f, dvs, Ease.Out), K(1.2f, dvs)))
                    .Hit(H(0.06f, 1.2f, J.Weapon, tip, 0.8f * k, 55 * k, new Vector2(3f, 5f), 0.5f, HitLevel.Overhead, FxKind.Heavy, true, true, false, 0.1f, 0.1f))
                    .At(0.06f, f => { f.vel = new Vector2(f.facing * 5f * k, -20f * k); if (k > 1f) f.flying = false; FightSfx.Play("whoosh_heavy", 0.8f); });
                var md = m[dive.Item1];
                md.air = true; md.endOnLand = true; md.gravityMul = 1.5f;
                md.onLand = f =>
                {
                    float x = f.transform.position.x + f.facing * 0.8f * f.scale;
                    FightFx.Shockwave(new Vector3(x, 0, 0), 2.2f * k * f.scale, k > 1f ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.8f, 0.4f));
                    FightSfx.Play("slam", k > 1f ? 0.75f : 1f);
                    Dir?.GroundPound(f, x, 2.4f * k * f.scale, 45 * k, true);
                    Dir?.Shake(0.18f * k, 0.3f);
                    f.StartMove(f.GetMove("diveland"));       // crouch-and-recover frames (no-op when that sheet is missing)
                };
            }

            // ---------------------------------------------------------------- kicks and tail
            var k1w = S().Lean(-4, 0).LegF(85, -110).ArmF(40, 80).Wpn(70);
            var k1s = S().Lean(-18, -10).Hip(-0.05f, -0.08f).LegF(92, -5, 20).ArmF(30, 90).Wpn(80).ArmB(10, 60).Tail(-140);
            m["k1"] = M("k1", "Vanara Kick", C(false, 0.38f, K(0, k1w), K(0.07f, k1w, Ease.Out), K(0.13f, k1s, Ease.Out), K(0.22f, k1s), K(0.38f, S())))
                .Hit(H(0.1f, 0.2f, J.FootF, new Vector2(0.25f, 0), 0.45f, 35, new Vector2(1.6f, 0), 0.38f));
            m["k1"].sfx = "kick"; m["k1"].cancelFrom = 0.18f;
            var k2w = S().Lean(-10, -20).LegF(60, -120).ArmF(40, 80).Wpn(70);
            var k2s = S().Lean(-30, -25).Hip(-0.1f, -0.02f).LegF(125, -5, 10).ArmF(20, 100).Wpn(80).ArmB(-20, 60).Tail(-160).Head(10);
            m["k2"] = M("k2", "Roundhouse", C(false, 0.46f, K(0, k2w), K(0.1f, k2w, Ease.Out), K(0.18f, k2s, Ease.Out), K(0.28f, k2s), K(0.46f, S())))
                .Hit(H(0.14f, 0.26f, J.FootF, new Vector2(0.25f, 0), 0.5f, 45, new Vector2(3.5f, 1f), 0.36f));
            m["k2"].sfx = "kick"; m["k2"].cancelFrom = 0.26f;

            var trip = crouch.Copy().Lean(25, 10).Hip(0, -0.55f);
            m["tailtrip"] = M("tailtrip", "Tail Trip", C(false, 0.5f, K(0, trip.Copy().Tail(-100)), K(0.1f, trip.Copy().Tail(-60), Ease.Out), K(0.22f, trip.Copy().Tail(75), Ease.Out), K(0.32f, trip.Copy().Tail(70)), K(0.5f, S())))
                .Hit(H(0.14f, 0.27f, J.Tail, new Vector2(0, -0.55f), 0.65f, 35, new Vector2(1.5f, 5f), 0.4f, HitLevel.Low, FxKind.Light, false, true));
            m["tailtrip"].sfx = "whoosh"; m["tailtrip"].cancelFrom = 0.3f;

            var tg = S().Lean(10, 4);
            var throwT = M("tailthrow", "Tail Throw", C(false, 0.85f, K(0, tg.Copy().Tail(100)), K(0.25f, tg.Copy().Lean(-12, -6).Tail(200).Head(-20), Ease.Out), K(0.45f, tg.Copy().Lean(15, 6).Tail(290), Ease.Out), K(0.85f, S())))
                .At(0.45f, f => { f.ReleaseHeld(new Vector2(-7f, -2f), 75f); FightSfx.Play("slam"); Dir?.Shake(0.15f, 0.25f); });
            throwT.holdJoint = J.Tail; throwT.holdOffset = new Vector2(0, -0.95f); throwT.armor = true;
            m["tailgrab"] = M("tailgrab", "Tail Grab", C(false, 0.5f, K(0, tg.Copy().Tail(-100)), K(0.16f, tg.Copy().Tail(100), Ease.Out), K(0.3f, tg.Copy().Tail(95)), K(0.5f, S())))
                .Hit(H(0.1f, 0.24f, J.Tail, new Vector2(0, -0.7f), 0.55f, 0, Vector2.zero, grab: true));
            m["tailgrab"].throwMove = throwT; m["tailgrab"].sfx = "whoosh";

            var reach = S().Lean(20, 6).Hip(0.15f, -0.2f).Feet(0.6f, -0.3f).ArmF(85, 0).Wpn(100).ArmB(85, 0);
            var lift = S().Lean(-15, -6).Hip(0, -0.1f).ArmF(170, 10).Wpn(100).ArmB(170, 10).Head(-20);
            var slam = S().Lean(35, 12).Hip(0.15f, -0.4f).Feet(0.7f, -0.35f).ArmF(60, 0).Wpn(100).ArmB(60, 0);
            var liftSlam = M("liftslam", "Lift and Body Slam", C(false, 0.9f, K(0, reach), K(0.3f, lift, Ease.Out), K(0.42f, lift), K(0.52f, slam, Ease.Out), K(0.9f, S())))
                .At(0.52f, f => { f.ReleaseHeld(new Vector2(3f, -14f), 95f); FightSfx.Play("slam", 0.85f); Dir?.Shake(0.22f, 0.3f); FightFx.Shockwave(f.transform.position + Vector3.right * f.facing * 1.2f, 2f, new Color(1f, 0.8f, 0.4f)); });
            liftSlam.holdJoint = J.HandB; liftSlam.holdOffset = new Vector2(0, 0.1f); liftSlam.armor = true; liftSlam.expr = Expr.Roar;
            m["bodyslam"] = M("bodyslam", "Body Slam Grab", C(false, 0.45f, K(0, S()), K(0.12f, reach, Ease.Out), K(0.25f, reach), K(0.45f, S())))
                .Hit(H(0.1f, 0.22f, J.HandB, Vector2.zero, 0.55f, 0, Vector2.zero, grab: true));
            m["bodyslam"].throwMove = liftSlam;

            var fk = P().Lean(-25, -10).LegF(95, -5).LegB(-20, -60).ArmF(40, 80).Wpn(90).ArmB(20, 60).Tail(-150);
            m["flykick"] = M("flykick", "Flying Kick", C(false, 0.7f, K(0, A().Lean(-5).ArmF(40, 80).Wpn(90)), K(0.06f, fk, Ease.Out), K(0.7f, fk)))
                .Hit(H(0.04f, 0.7f, J.FootF, new Vector2(0.25f, 0), 0.55f, 50, new Vector2(4f, 5f), 0.4f, HitLevel.Overhead, FxKind.Heavy, true, true))
                .At(0.04f, f => { f.vel = new Vector2(f.facing * 9f, -3f); });
            m["flykick"].air = true; m["flykick"].gravityMul = 0.4f; m["flykick"].endOnLand = true; m["flykick"].sfx = "kick";

            var fw = S().Lean(15, 6).Hip(0.05f, -0.2f).Feet(0.55f, -0.35f);
            m["firewhip"] = M("firewhip", "Fire Tail Whip", C(false, 0.55f, K(0, fw.Copy().Tail(-110)), K(0.07f, fw.Copy().Tail(-130), Ease.Out), K(0.15f, fw.Copy().Tail(110), Ease.Out), K(0.3f, fw.Copy().Tail(60)), K(0.55f, S())))
                .Hit(H(0.09f, 0.22f, J.Tail, new Vector2(0, -0.9f), 1.0f, 60, new Vector2(4f, 3f), 0.45f, HitLevel.Mid, FxKind.Fire, true, false, false, 0.08f, 0.08f, true))
                .At(0.1f, f => { for (int i = 0; i < f.puppet.TailCount; i++) FightFx.Flames(f.puppet.TailSeg(i).position, 2, 0.9f); FightSfx.Play("fire"); });
            m["firewhip"].sfx = "whoosh";

            // ---------------------------------------------------------------- powers
            m["roar"] = M("roar", "Divine Roar", clips["roar"]);
            m["roar"].clip = C(false, 0.6f, K(0, crouch), K(0.15f, roar, Ease.Out), K(0.45f, roar), K(0.6f, S()));
            m["roar"].invulnerable = true; m["roar"].armor = true; m["roar"].expr = Expr.Roar;

            var vd = S().Lean(45, 10).Hip(0, -0.3f).Feet(0.7f, -0.6f).ArmF(90, 0).Wpn(10).ArmB(-30, 20).Tail(-170);
            var vayu = M("vayudash", "Pawan Dash", C(false, 0.55f, K(0, S().Hip(0, -0.35f)), K(0.05f, vd, Ease.Out), K(0.38f, vd), K(0.55f, S())));
            for (int i = 0; i < 5; i++)
                vayu.Hit(H(0.05f + i * 0.06f, 0.11f + i * 0.06f, J.Torso, new Vector2(0.3f, 0.5f), 1.0f, 16, new Vector2(0.6f, 0), 0.5f, HitLevel.Mid, FxKind.Light, false, false, false, 0.03f, 0.03f));
            vayu.Hit(H(0.33f, 0.42f, J.Weapon, tip, 1.0f, 30, new Vector2(4f, 3f), 0.5f, HitLevel.Mid, FxKind.Heavy, true, true));
            vayu.Lunge(15f, 0.05f, 0.33f); vayu.invulnerable = true; vayu.invulnT1 = 0.4f; vayu.sfx = "wind";
            vayu.At(0.05f, f => FightFx.Streaks(f.Center, f.facing, 8, new Color(0.7f, 0.95f, 1f)));
            m["vayudash"] = vayu;

            m["rise"] = M("rise", "Rising Giant Uppercut", C(false, 0.55f, K(0, lw, Ease.Out), K(0.12f, ls, Ease.Out), K(0.3f, lair), K(0.55f, lair)))
                .Hit(H(0.05f, 0.22f, J.Weapon, tip, 0.9f, 90, new Vector2(1f, 16f), 0.6f, HitLevel.Mid, FxKind.Gold, true, false, true, 0.12f, 0.14f))
                .At(0f, f => { f.scaleTarget = 1f; FightFx.Sparkle(f.Center, 14, 1.2f, new Color(0.6f, 0.95f, 1f), false); FightSfx.Play("grow", 1.6f, 0.7f); })
                .At(0.1f, f => { f.vel.y = 12f; f.grounded = false; });
            m["rise"].sfx = "whoosh_heavy";

            m["stomp"] = M("stomp", "Titan Stomp", C(false, 0.75f, K(0, S()), K(0.2f, S().LegF(70, -70).Lean(-6, -4), Ease.In), K(0.3f, S().Feet(0.6f, -0.35f).Hip(0, -0.25f).Lean(15, 4), Ease.Out), K(0.75f, S())))
                .At(0.3f, f =>
                {
                    float x = f.transform.position.x + f.facing * 0.6f * f.scale;
                    FightFx.Shockwave(new Vector3(x, 0, 0), 3.2f * f.scale, new Color(1f, 0.75f, 0.3f));
                    FightSfx.Play("slam", 0.7f); Dir?.Shake(0.3f, 0.4f);
                    Dir?.GroundPound(f, x, 2.4f * f.scale, 70, true);
                });
            m["stomp"].expr = Expr.Roar;

            var fb0 = charge.Copy();
            m["finalblow"] = M("finalblow", "Panchmukhi Strike", C(false, 1.0f, K(0, fb0), K(0.3f, fb0, Ease.In), K(0.5f, chs, Ease.Out), K(0.85f, chs), K(1.0f, S())))
                .Hit(H(0.4f, 0.6f, J.Weapon, tip, 1.4f, 350, new Vector2(9f, 9f), 1f, HitLevel.Unblockable, FxKind.Gold, true, true, false, 0.25f, 0.4f))
                .Lunge(14f, 0.3f, 0.48f);
            m["finalblow"].armor = true; m["finalblow"].expr = Expr.Roar; m["finalblow"].sfx = "whoosh_heavy";

            ApplyFrameArt(m);
            return m;
        }

        // ======================================================================================= frame art
        /// <summary>
        /// Points a move at frames [from..to] of a painted clip: the hit windows open on the impact frames (measured weapon / foot / tail tip),
        /// timed events fire when the window opens, and chaining opens right after it. If the clip is missing the move keeps its skeleton animation.
        /// </summary>
        static void FrameMove(MoveDef d, string clip, int from, int to, int hitFrom, int hitTo, float reach = 0f, float up = 0f, int eventFrame = -1, bool oneHit = true)
        {
            var c = HanumanFrames.Get(clip);
            if (c == null || d == null) return;
            d.frameClip = clip; d.frameFrom = from; d.frameTo = to;
            if (oneHit && d.hits.Count > 1) d.hits.RemoveRange(1, d.hits.Count - 1);     // the painted move has one impact
            float t0 = (hitFrom - from) / c.fps, t1 = (hitTo + 1 - from) / c.fps;
            foreach (var h in d.hits) { h.t0 = t0; h.t1 = t1; h.frameTip = true; h.offset = new Vector2(reach, up); }
            float te = eventFrame >= 0 ? (eventFrame - from) / c.fps : t0;
            foreach (var e in d.events) e.t = te;
            d.cancelFrom = t1;
            if (d.lungeSpeed != 0f) { d.lungeSpeed *= 0.6f; d.lungeT0 = Mathf.Max(0f, t0 - 0.1f); d.lungeT1 = t0 + 0.03f; }
        }

        /// <summary>Visual only: loops frames [from..to] of a painted clip for the whole move; timing and hit windows stay as authored.</summary>
        static void FrameLook(MoveDef d, string clip, int from, int to, bool hold = false, bool stretch = false, bool tipHits = false)
        {
            if (d == null || HanumanFrames.Get(clip) == null) return;
            d.frameClip = clip; d.frameFrom = from; d.frameTo = to; d.frameLoop = true; d.frameHold = hold; d.frameStretch = stretch;
            if (tipHits) foreach (var h in d.hits) { h.frameTip = true; h.offset = Vector2.zero; }       // hit circles follow the painted weapon / foot
        }

        static Vector3 HP(float t, float fwd, float up) => new Vector3(t, fwd, up);

        static void ApplyFrameArt(Dictionary<string, MoveDef> m)
        {
            FrameMove(m["m1"], "gada1", 0, 7, 3, 4);
            FrameMove(m["m2"], "gada2", 0, 7, 3, 4);
            FrameMove(m["m3"], "overhead", 0, 15, 9, 11, 0f, 0f, 10);
            FrameMove(m["thrust"], "thrust", 0, 7, 4, 6);
            FrameMove(m["launcher"], "launcher", 0, 7, 3, 5, 0.45f);
            FrameMove(m["sweep"], "sweep", 0, 7, 3, 4);
            FrameMove(m["k1"], "kick", 0, 7, 3, 4, 0.45f);
            m["k1"].hits[0].radius = 0.55f;
            FrameMove(m["tailtrip"], "tailtrip", 0, 7, 3, 4);
            FrameMove(m["firewhip"], "firewhip", 0, 11, 4, 6);

            // whirlwind: two impacts while the gada spins (frames 7-11 and 12-16)
            FrameMove(m["whirl"], "whirl", 0, 19, 6, 10, 0f, 0f, -1, false);
            if (m["whirl"].frameClip == "whirl" && m["whirl"].hits.Count > 1)
            {
                float wf = 1f / HanumanFrames.Get("whirl").fps;
                m["whirl"].hits[1].t0 = 11 * wf; m["whirl"].hits[1].t1 = 16 * wf; m["whirl"].cancelFrom = 16 * wf;
            }
            // leaping smashes: crouch, leap, gada comes down on frame 12
            FrameMove(m["charge1"], "smashB", 0, 19, 11, 12, 0f, 0f, 11);
            FrameMove(m["charge2"], "smashA", 0, 19, 11, 12, 0f, 0f, 11);
            FrameMove(m["air1"], "airhoriz", 0, 9, 4, 6);
            FrameMove(m["air2"], "airarc", 0, 9, 4, 6);
            FrameMove(m["air3"], "airsmash", 0, 7, 4, 6);
            FrameMove(m["stomp"], "stompsheet", 0, 11, 7, 8, 0f, 0f, 7);
            FrameMove(m["finalblow"], "finalblow", 0, 23, 12, 14);
            FrameLook(m["dive"], "dive", 0, 5, true, false, true);
            FrameLook(m["meteor"], "dive", 0, 5, true, false, true);
            FrameLook(m["flykick"], "flykick", 1, 5, true, false, true);
            FrameLook(m["vayudash"], "dash", 0, 11, false, true);
            FrameLook(m["roar"], "roar", 0, 9, false, true);

            // landing from a dive: the crouch-and-recover frames of the dive sheet
            var dl = new MoveDef { id = "diveland", name = "Landing", clip = m["dive"].clip };
            m["diveland"] = dl;
            FrameMove(dl, "diveland", 0, 5, 0, 1);

            // still borrowed (no sheet yet): roundhouse uses the kick, the rising giant uppercut uses the launcher
            FrameMove(m["k2"], "kick", 0, 7, 3, 4, 0.45f);
            m["k2"].hits[0].radius = 0.55f;
            FrameMove(m["rise"], "launcher", 0, 7, 3, 5, 0.45f);

            // tail grab: frames 1-4 reach out, frames 5-8 curl the opponent up (the slam half is still to come)
            FrameMove(m["tailgrab"], "tailgrab", 0, 3, 2, 3);
            var tt = m["tailgrab"].throwMove; var tc = HanumanFrames.Get("tailgrab");
            if (tt != null && tc != null && tc.frames.Length >= 8)
            {
                FrameMove(tt, "tailgrab", 4, 7, 4, 4);
                float len = tt.Length;
                foreach (var e in tt.events) e.t = len - 0.02f;
                tt.holdPath = new[] { HP(0f, 1.3f, 1.3f), HP(len * 0.33f, 1.2f, 1.7f), HP(len * 0.66f, 0.8f, 2.5f), HP(len, 0.5f, 3.2f) };
            }

            // lift and body slam: frames 1-4 grab, 5-12 lift overhead and slam, 13-16 recover
            FrameMove(m["bodyslam"], "liftslam", 0, 3, 2, 3);
            var ls = m["bodyslam"].throwMove; var lc = HanumanFrames.Get("liftslam");
            if (ls != null && lc != null && lc.frames.Length >= 16)
            {
                FrameMove(ls, "liftslam", 4, 15, 4, 4);
                float f = 1f / lc.fps;
                foreach (var e in ls.events) e.t = 7 * f;
                ls.holdPath = new[]
                {
                    HP(0f, 0.7f, 1.1f), HP(1 * f, 0.5f, 1.9f), HP(2 * f, 0.25f, 3.0f), HP(3 * f, 0.15f, 3.4f), HP(4 * f, 0.15f, 3.4f),
                    HP(5 * f, 0.9f, 2.5f), HP(6 * f, 1.3f, 1.2f), HP(7 * f, 1.5f, 0.45f)
                };
            }
        }

        // ======================================================================================= AHIRAVANA
        public static Puppet.Dims AhiravanaDims() => new Puppet.Dims
        {
            thigh = 0.68f, shin = 0.66f, torso = 0.92f, upperArm = 0.55f, foreArm = 0.5f, headR = 0.29f, shoulderY = 0.82f, weaponLen = 1.5f
        };

        public static Puppet.Style AhiravanaStyle() => new Puppet.Style
        {
            skin = new Color(0.33f, 0.3f, 0.62f), face = new Color(0.42f, 0.37f, 0.7f), cloth = new Color(0.2f, 0.08f, 0.28f),
            cloth2 = new Color(0.32f, 0.1f, 0.42f), gold = new Color(0.86f, 0.66f, 0.22f), hair = new Color(0.05f, 0.03f, 0.08f),
            eye = new Color(1f, 0.45f, 1f), horns = true, cape = true, glowEyes = true, weapon = 2, bulk = 1f
        };

        static Pose AStance() => P().Lean(-2, 0).Head(-8).Hip(0, -0.06f).Feet(0.32f, -0.3f).ArmF(45, 60).Wpn(28).ArmB(80, 70);

        public static Dictionary<string, MoveDef> Ahiravana(Dictionary<string, PuppetClip> clips)
        {
            D = AhiravanaDims();
            var m = new Dictionary<string, MoveDef>();
            Pose S() => AStance();
            var tip = new Vector2(0, -D.weaponLen - 0.2f);

            clips["idle"] = C(true, 1.6f, K(0, S()), K(0.8f, S().Hip(0, -0.09f).ArmB(85, 75).Head(-10)));
            Pose W(float ff, float fy, float bf, float by, float hy) => S().Hip(0.02f, hy).Feet(ff, bf, fy, by).Lean(2, 0);
            var w0 = W(0.4f, 0, -0.38f, 0, -0.1f); var w1 = W(0.02f, 0, 0f, 0.16f, -0.04f); var w2 = W(-0.38f, 0, 0.4f, 0, -0.1f); var w3 = W(0f, 0.16f, 0.02f, 0, -0.04f);
            clips["walkf"] = C(true, 0.7f, K(0, w0, Ease.Linear), K(0.175f, w1, Ease.Linear), K(0.35f, w2, Ease.Linear), K(0.525f, w3, Ease.Linear));
            clips["walkb"] = C(true, 0.76f, K(0, w0, Ease.Linear), K(0.19f, w3, Ease.Linear), K(0.38f, w2, Ease.Linear), K(0.57f, w1, Ease.Linear));
            var crouch = S().Hip(0, -0.6f).Feet(0.45f, -0.42f).Lean(18, 6).ArmF(40, 70).Wpn(40).ArmB(60, 80);
            clips["crouch"] = C(true, 1f, K(0, crouch), K(0.5f, crouch));
            var block = S().Lean(-6, 0).Hip(-0.05f, -0.12f).ArmF(75, 70).Wpn(45).ArmB(70, 100).Head(6);
            clips["block"] = C(true, 1f, K(0, block), K(0.5f, block));
            clips["crouchblock"] = C(true, 1f, K(0, crouch.Copy().ArmF(80, 60).Wpn(45).ArmB(70, 100)), K(0.5f, crouch.Copy().ArmF(80, 60).Wpn(45).ArmB(70, 100)));
            clips["jump"] = C(false, 0.3f, K(0, S().Hip(0, -0.3f)), K(0.12f, P().Lean(6).LegF(70, -100).LegB(25, -70).ArmF(100, 40).Wpn(20).ArmB(130, 30)));
            clips["fall"] = C(true, 0.8f, K(0, P().LegF(30, -40).LegB(-10, -30).ArmF(70, 40).Wpn(30).ArmB(110, 20)), K(0.4f, P().LegF(25, -35).LegB(-14, -25).ArmF(75, 35).Wpn(30).ArmB(115, 15)));
            var rec = S().Lean(-24, -8).Head(-28).Hip(-0.12f, -0.1f).ArmF(-10, 40).Wpn(60).ArmB(-30, 30);
            clips["hit"] = C(false, 0.42f, K(0, rec, Ease.Out), K(0.1f, rec.Copy().Lean(-28, -10)), K(0.42f, S()));
            var hl = S().Lean(35, 10).Head(20).Hip(0.05f, -0.3f).ArmF(20, 60).ArmB(10, 50);
            clips["hitlow"] = C(false, 0.42f, K(0, hl, Ease.Out), K(0.12f, hl), K(0.42f, S()));
            var f0 = P().Lean(-30, -20).Head(-30).LegF(50, -70).LegB(-10, -40).ArmF(150, 30).Wpn(30).ArmB(170, 20);
            clips["launched"] = C(true, 0.5f, K(0, f0), K(0.25f, f0.Copy().Lean(-35, -25).ArmF(130, 50).ArmB(190, 10).LegF(40, -90)));
            var down = P().Lean(0, -88).Head(-10).Hip(0.2f, 0.3f - D.HipBase).LegF(8, -15).LegB(14, -25).ArmF(-60, 10).Wpn(90).ArmB(-80, 0);
            clips["down"] = C(true, 1f, K(0, down), K(0.5f, down.Copy().Lean(-3, -88)));
            var sit = P().Lean(35, -50).Head(10).Hip(0, 0.45f - D.HipBase).LegF(60, -120).LegB(40, -100).ArmF(-30, 40).Wpn(80).ArmB(-50, 20);
            clips["getup"] = C(false, 0.42f, K(0, down), K(0.16f, sit), K(0.3f, crouch), K(0.42f, S()));
            var tuck = P().Lean(30, 0).Head(15).Hip(0, 0.25f).LegF(100, -130).LegB(90, -130).ArmF(60, 100).Wpn(40).ArmB(60, 100);
            clips["dodgeb"] = C(false, 0.38f, K(0, S().Hip(0, -0.3f)), K(0.06f, tuck), K(0.26f, tuck.Copy().Lean(30, -300)), K(0.38f, S().Lean(-2, -360)));
            clips["dodgef"] = C(false, 0.38f, K(0, S().Hip(0, -0.3f)), K(0.06f, tuck.Copy().Hip(0, -0.45f)), K(0.28f, tuck.Copy().Hip(0, -0.45f).Lean(30, 300)), K(0.38f, S().Lean(-2, 360)));
            var dz = S().Lean(-8, -4).Head(-15).Hip(-0.05f, -0.2f).ArmF(10, 20).Wpn(100).ArmB(5, 15);
            clips["stunned"] = C(true, 1f, K(0, dz), K(0.5f, dz.Copy().Lean(6, 4).Head(12).Hip(0.06f, -0.22f)));
            var laugh = S().Lean(-18, -4).Head(-28).ArmB(110, 90).Hip(0, -0.04f);
            clips["laugh"] = C(true, 0.36f, K(0, laugh), K(0.18f, laugh.Copy().Lean(-14, -4).Head(-22).Hip(0, -0.08f)));
            clips["victory"] = clips["laugh"];
            var trans = S().Lean(-20, -6).Head(-30).Hip(0, -0.2f).Feet(0.45f, -0.45f).ArmF(110, -30).Wpn(40).ArmB(120, -30);
            clips["transform"] = C(true, 0.4f, K(0, trans), K(0.2f, trans.Copy().Hip(0, -0.23f).Head(-33)));
            clips["roar"] = clips["transform"];
            var kneel = P().Hip(0, 0.76f - D.HipBase).Feet(0.45f, 0f).LegB(-10, -100).Lean(28, 0).Head(18).ArmF(30, 40).Wpn(100).ArmB(50, 30);
            clips["kneel"] = C(true, 1.2f, K(0, kneel), K(0.6f, kneel.Copy().Lean(31, 0).Head(22)));
            var hover = P().Lean(-5).Head(-10).LegF(20, -30).LegB(5, -20).ArmF(60, 50).Wpn(30).ArmB(100, 60);
            clips["fly"] = C(true, 1f, K(0, hover), K(0.5f, hover.Copy().LegF(25, -40)));

            // ---- trident attacks
            var jw = S().Lean(-8, 0).Hip(-0.1f, -0.08f).ArmF(-20, 120).Wpn(-18).ArmB(60, 90);
            var js = S().Lean(22, 6).Hip(0.25f, -0.2f).Feet(0.7f, -0.35f).ArmF(85, 5).Wpn(28).ArmB(20, 40);
            m["jab"] = M("jab", "Trident Jab", C(false, 0.42f, K(0, jw), K(0.1f, jw, Ease.Out), K(0.17f, js, Ease.Out), K(0.26f, js), K(0.42f, S())))
                .Hit(H(0.12f, 0.21f, J.Weapon, tip, 0.5f, 45, new Vector2(2f, 0), 0.34f, HitLevel.Mid, FxKind.Dark)).Lunge(5f, 0.1f, 0.18f);
            m["jab"].expr = Expr.Furious;

            var sl0 = S().Lean(-10, -4).ArmF(150, 10).Wpn(30).ArmB(100, 60);
            var sl1 = S().Lean(28, 8).Hip(0.2f, -0.25f).Feet(0.65f, -0.35f).ArmF(40, 0).Wpn(60).ArmB(20, 30);
            m["combo"] = M("combo", "Serpent Combo", C(false, 0.82f, K(0, jw), K(0.08f, jw, Ease.Out), K(0.15f, js, Ease.Out), K(0.24f, js), K(0.36f, sl0, Ease.In), K(0.46f, sl1, Ease.Out), K(0.58f, sl1), K(0.82f, S())))
                .Hit(H(0.1f, 0.19f, J.Weapon, tip, 0.5f, 35, new Vector2(1.5f, 0), 0.4f, HitLevel.Mid, FxKind.Dark))
                .Hit(H(0.4f, 0.5f, J.Weapon, tip, 0.65f, 50, new Vector2(3.5f, 0), 0.4f, HitLevel.Mid, FxKind.Dark, true)).Lunge(4f, 0.08f, 0.46f);
            m["combo"].expr = Expr.Furious;

            var swc = crouch.Copy().Lean(25, 8).Hip(0, -0.65f);
            m["sweep"] = M("sweep", "Low Trident Sweep", C(false, 0.6f, K(0, swc.Copy().ArmF(-20, 20).Wpn(-60)), K(0.12f, swc.Copy().ArmF(-20, 20).Wpn(-60), Ease.Out), K(0.22f, swc.Copy().ArmF(70, 10).Wpn(15), Ease.Out), K(0.36f, swc.Copy().ArmF(80, 10).Wpn(15)), K(0.6f, S())))
                .Hit(H(0.15f, 0.26f, J.Weapon, tip, 0.65f, 45, new Vector2(2f, 5f), 0.4f, HitLevel.Low, FxKind.Dark, false, true));

            var ow = S().Lean(-15, -6).Hip(-0.1f, 0f).ArmF(185, 20).Wpn(40).ArmB(180, 20).Head(-15);
            var os = S().Lean(35, 12).Hip(0.2f, -0.4f).Feet(0.75f, -0.35f).ArmF(70, 0).Wpn(20).ArmB(60, 0).Head(10);
            m["overhead"] = M("overhead", "Sky-Piercer Smash", C(false, 0.95f, K(0, S()), K(0.4f, ow, Ease.In), K(0.5f, os, Ease.Out), K(0.68f, os), K(0.95f, S())))
                .Hit(H(0.46f, 0.57f, J.Weapon, tip, 0.8f, 85, new Vector2(3f, 7f), 0.5f, HitLevel.Overhead, FxKind.Dark, true, true, false, 0.13f, 0.18f))
                .At(0.5f, f => { var p = f.WeaponPoint; FightFx.Shockwave(new Vector3(p.x, 0, 0), 1.8f * f.scale, new Color(0.7f, 0.35f, 1f)); FightSfx.Play("slam", 0.9f, 0.8f); })
                .Lunge(4f, 0.4f, 0.5f);
            m["overhead"].telegraph = true; m["overhead"].sfx = "whoosh_heavy"; m["overhead"].expr = Expr.Furious;

            var reach = S().Lean(25, 8).Hip(0.2f, -0.2f).Feet(0.7f, -0.3f).ArmB(85, 0).ArmF(30, 60).Wpn(60);
            var hold = S().Lean(-10, -4).ArmB(160, 0).ArmF(40, 60).Wpn(60).Head(-15);
            var thr = S().Lean(30, 10).Hip(0.2f, -0.3f).Feet(0.7f, -0.3f).ArmB(60, 0).ArmF(30, 60).Wpn(60);
            var dark = M("darkthrow", "Patala Throw", C(false, 1.1f, K(0, reach), K(0.3f, hold, Ease.Out), K(0.6f, hold), K(0.72f, thr, Ease.Out), K(1.1f, S())))
                .At(0.35f, f => { FightFx.Smoke(f.puppet.Point(J.HandB, Vector2.zero), 8, new Color(0.5f, 0.2f, 0.8f, 0.8f), 1f); FightSfx.Play("orb_cast"); f.SetExpr(Expr.Laugh, 0.4f); })
                .At(0.72f, f => { f.ReleaseHeld(new Vector2(7f, 6f), 90f); FightFx.Hit(f.puppet.Point(J.HandB, Vector2.zero), FxKind.Dark, true, f.facing); FightSfx.Play("orb_blast"); });
            dark.holdJoint = J.HandB; dark.holdOffset = new Vector2(0, 0.2f); dark.armor = true;
            m["grab"] = M("grab", "Shadow Grasp", C(false, 0.65f, K(0, S()), K(0.2f, S().Lean(-6).ArmB(40, 100), Ease.In), K(0.32f, reach, Ease.Out), K(0.45f, reach), K(0.65f, S())))
                .Hit(H(0.26f, 0.4f, J.HandB, Vector2.zero, 0.6f, 0, Vector2.zero, grab: true)).Lunge(5f, 0.22f, 0.34f);
            m["grab"].throwMove = dark; m["grab"].telegraph = true; m["grab"].expr = Expr.Smirk;

            var cw = S().ArmB(30, 120).Lean(-6);
            var cs = S().Lean(10, 4).ArmB(90, 0).Feet(0.5f, -0.35f);
            m["orb"] = M("orb", "Dark Orb", C(false, 0.7f, K(0, S()), K(0.2f, cw, Ease.Out), K(0.3f, cs, Ease.Out), K(0.45f, cs), K(0.7f, S())))
                .At(0.3f, f => Dir?.SpawnOrb(f, f.puppet.Point(J.HandB, new Vector2(0, -0.1f)), new Vector2(f.facing * 8.5f, 0), 45f, 0.42f));
            m["orb"].expr = Expr.Smirk;

            var barrage = M("barrage", "Orb Barrage", C(false, 1.6f, K(0, crouch), K(0.25f, hover, Ease.Out), K(0.45f, hover.Copy().ArmB(100, 0)), K(0.6f, hover), K(0.7f, hover.Copy().ArmB(100, 0)), K(0.85f, hover), K(0.95f, hover.Copy().ArmB(100, 0)), K(1.6f, hover)));
            barrage.At(0.1f, f => { f.flying = true; f.vel.y = 7f; f.grounded = false; FightSfx.Play("magic", 0.8f); });
            barrage.At(0.32f, f => { f.vel.y = 0f; });
            foreach (float t in new[] { 0.45f, 0.7f, 0.95f }) barrage.At(t, f => Dir?.SpawnOrbAimed(f, f.puppet.Point(J.HandB, new Vector2(0, -0.1f)), 9f, 35f));
            barrage.At(1.3f, f => { f.flying = false; });
            barrage.air = true; barrage.expr = Expr.Laugh;
            m["barrage"] = barrage;

            m["vanish"] = M("vanish", "Shadow Step", C(false, 0.6f, K(0, crouch), K(0.3f, crouch), K(0.6f, S())))
                .At(0.12f, f => Dir?.TeleportBehind(f));
            m["vanish"].invulnerable = true; m["vanish"].expr = Expr.Smirk;

            var raise = S().Lean(-15, -6).Head(-25).ArmF(160, 10).Wpn(20).ArmB(170, 10);
            m["summon"] = M("summon", "Maya Illusion", C(false, 1.0f, K(0, S()), K(0.3f, raise, Ease.Out), K(0.7f, raise), K(1.0f, S())))
                .At(0.5f, f => Dir?.SpawnClones(f as AhiravanaFighter));
            m["summon"].expr = Expr.Laugh; m["summon"].armor = true;

            // ---- titan form
            m["gslam"] = M("gslam", "Titan Trident Slam", C(false, 1.25f, K(0, S()), K(0.55f, ow, Ease.In), K(0.66f, os, Ease.Out), K(0.9f, os), K(1.25f, S())))
                .Hit(H(0.62f, 0.72f, J.Weapon, tip, 0.9f, 80, new Vector2(3f, 7f), 0.5f, HitLevel.Overhead, FxKind.Dark, true, true, false, 0.13f, 0.2f))
                .At(0.66f, f => { var p = f.WeaponPoint; FightFx.Shockwave(new Vector3(p.x, 0, 0), 2.6f * f.scale, new Color(0.7f, 0.35f, 1f)); FightSfx.Play("slam", 0.7f); Dir?.Shake(0.3f, 0.4f); Dir?.SpawnGroundWave(f, p.x); });
            m["gslam"].telegraph = true; m["gslam"].sfx = "whoosh_heavy"; m["gslam"].expr = Expr.Furious;
            m["gsweep"] = M("gsweep", "Titan Sweep", m["sweep"].clip)
                .Hit(H(0.15f, 0.28f, J.Weapon, tip, 0.9f, 60, new Vector2(3f, 6f), 0.45f, HitLevel.Low, FxKind.Dark, true, true, false, 0.1f, 0.15f));
            m["gsweep"].telegraph = true;

            return m;
        }
    }
}
