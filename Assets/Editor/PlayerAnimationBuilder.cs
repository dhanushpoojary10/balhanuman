using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BalHanuman.EditorTools
{
    /// <summary>Creates the Bal Hanuman AnimationClips and Animator Controller from the HQ character frames.</summary>
    public static class PlayerAnimationBuilder
    {
        const string Dir = "Assets/Animations/BalHanuman";

        static Sprite[] Frames(string folder, string prefix, int count)
        {
            var l = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                string p = "Assets/ArtHQ/Character/" + folder + "/" + prefix + "_" + (i + 1).ToString("00") + ".png";
                l[i] = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (l[i] == null) Debug.LogError("Missing frame " + p);
            }
            return l;
        }

        static AnimationClip Clip(string name, Sprite[] frames, float fps, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = fps };
            var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
            int n = frames.Length;
            var keys = new ObjectReferenceKeyframe[n + (n == 1 ? 1 : 0)];
            for (int i = 0; i < n; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            if (n == 1) keys[1] = new ObjectReferenceKeyframe { time = 1f / fps, value = frames[0] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            string path = Dir + "/" + name + ".anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AnimatorStateTransition T(AnimatorState from, AnimatorState to, bool exit = false)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = exit; t.exitTime = 1f; t.duration = 0f; t.hasFixedDuration = true;
            return t;
        }

        public static RuntimeAnimatorController Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Animations", "BalHanuman");

            var idle = Clip("Hanuman_Idle", Frames("Idle24", "idle24", 24), 12f, true);
            var run = Clip("Hanuman_Run", Frames("Run", "run", 8), 14f, true);
            // the 12-frame jump sheet is split by phase: 1-3 crouch, 4-6 take-off and rise, 7 top, 8-9 falling, 10-12 landing
            var j12 = Frames("Jump12", "jump12", 12);
            var jump = Clip("Hanuman_Jump", j12.GetRange(3, 3), 14f, false);
            var apex = Clip("Hanuman_Apex", j12.GetRange(6, 1), 1f, false);
            var fall = Clip("Hanuman_Fall", j12.GetRange(7, 2), 9f, false);
            var land = Clip("Hanuman_Land", j12.GetRange(9, 3), 15f, false);
            var climb = Clip("Hanuman_Climb", Frames("Climb10", "climb10", 10), 12f, true);
            var interact = Clip("Hanuman_Interact", Frames("Special", "power", 3), 7f, false);
            var roll = Clip("Hanuman_Roll", Frames("Roll12", "roll12", 12), 24f, false);
            var crouch = Clip("Hanuman_Crouch", j12.GetRange(1, 1), 1f, true);

            string path = Dir + "/BalHanuman.controller";
            AssetDatabase.DeleteAsset(path);
            var c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("VelY", AnimatorControllerParameterType.Float);
            c.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            c.AddParameter("Climbing", AnimatorControllerParameterType.Bool);
            c.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Rolling", AnimatorControllerParameterType.Bool);
            c.AddParameter("Crouching", AnimatorControllerParameterType.Bool);

            var sm = c.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sRun = sm.AddState("Run"); sRun.motion = run;
            var sJump = sm.AddState("Jump"); sJump.motion = jump;
            var sFall = sm.AddState("Fall"); sFall.motion = fall;
            var sApex = sm.AddState("Apex"); sApex.motion = apex;
            var sLand = sm.AddState("Land"); sLand.motion = land;
            var sClimb = sm.AddState("Climb"); sClimb.motion = climb;
            var sInteract = sm.AddState("Interact"); sInteract.motion = interact;
            var sRoll = sm.AddState("Roll"); sRoll.motion = roll;
            var sCrouch = sm.AddState("Crouch"); sCrouch.motion = crouch;
            sm.defaultState = sIdle;

            var t = T(sIdle, sRun); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            t = T(sRun, sIdle); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");

            t = sm.AddAnyStateTransition(sJump); t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); t.AddCondition(AnimatorConditionMode.Greater, 1.2f, "VelY"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Climbing");
            t = sm.AddAnyStateTransition(sApex); t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); t.AddCondition(AnimatorConditionMode.Less, 1.2f, "VelY"); t.AddCondition(AnimatorConditionMode.Greater, -1.2f, "VelY"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Climbing");
            t = sm.AddAnyStateTransition(sFall); t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); t.AddCondition(AnimatorConditionMode.Less, -1.2f, "VelY"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Climbing");

            // touching down plays the landing crouch (frames 10-12); running or turning straight on cuts it short
            t = T(sJump, sLand); t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            t = T(sApex, sLand); t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            t = T(sFall, sLand); t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            t = T(sLand, sRun); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            T(sLand, sIdle, true);

            t = sm.AddAnyStateTransition(sClimb); t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Climbing");
            t = T(sClimb, sIdle); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Climbing");

            t = sm.AddAnyStateTransition(sInteract); t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Interact");
            T(sInteract, sIdle, true);

            t = sm.AddAnyStateTransition(sRoll); t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Rolling");
            t = T(sRoll, sIdle); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Rolling");
            t = sm.AddAnyStateTransition(sCrouch); t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Crouching"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Rolling");
            t = T(sCrouch, sIdle); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Crouching");

            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            return c;
        }

        static Sprite[] GetRange(this Sprite[] a, int start, int count)
        {
            var r = new Sprite[count];
            System.Array.Copy(a, start, r, 0, count);
            return r;
        }
    }
}
