using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;

namespace SkaterAnim
{
    // A single keyframe pose: per-bone world-euler deltas (applied X then Y then Z) + hips local offset.
    public class Pose
    {
        public Dictionary<string, Vector3> rot = new Dictionary<string, Vector3>();
        public Vector3 hips;
        public float lowerYaw = 60f; // how much the lower body is turned across the board this frame

        public Pose Clone()
        {
            var p = new Pose { hips = hips, lowerYaw = lowerYaw };
            foreach (var kv in rot) p.rot[kv.Key] = kv.Value;
            return p;
        }
        public Pose Set(string bone, float x, float y, float z) { rot[bone] = new Vector3(x, y, z); return this; }
        public Pose Add(string bone, float x, float y, float z)
        {
            var v = rot.ContainsKey(bone) ? rot[bone] : Vector3.zero;
            rot[bone] = v + new Vector3(x, y, z); return this;
        }
        public Pose Hips(float x, float y, float z) { hips = new Vector3(x, y, z); return this; }
    }

    public static class SkaterAnimBuilder
    {
        public const string FBX = "Assets/skate board 1/skate board/CHART/D (4).fbx";
        public const string DIR = "Assets/_Game/Animations/Skater/";

        // Skateboard stance: turn the lower body (hips+legs+feet) across the board,
        // then counter-rotate the torso back so it keeps facing forward.
        public static float LowerYaw = 60f;
        public static float TorsoCounter = -52f;

        public static Transform FindDeep(Transform r, string n)
        {
            if (r.name == n) return r;
            foreach (Transform c in r) { var x = FindDeep(c, n); if (x != null) return x; }
            return null;
        }

        // Approved polished skater stance (staggered feet, arms out, subtle forward lean + upper-body twist).
        public static Pose Base()
        {
            var p = new Pose();
            p.Hips(0, -0.11f, 0);
            p.Set("mixamorig:Spine", 7, 9, 0);
            p.Set("mixamorig:Spine1", 7, 11, 0);
            p.Set("mixamorig:Spine2", 6, 11, 0);
            p.Set("mixamorig:Neck", -12, -16, 0);
            p.Set("mixamorig:Head", -7, -12, 0);
            p.Set("mixamorig:LeftUpLeg", -30, 0, 0);
            p.Set("mixamorig:LeftLeg", 40, 0, 0);
            p.Set("mixamorig:LeftFoot", -12, 0, 0);
            p.Set("mixamorig:RightUpLeg", -10, 0, 0);
            p.Set("mixamorig:RightLeg", 40, 0, 0);
            p.Set("mixamorig:RightFoot", -22, 0, 0);
            p.Set("mixamorig:LeftArm", 12, 0, 30);
            p.Set("mixamorig:LeftForeArm", 18, 22, 0);
            p.Set("mixamorig:RightArm", 12, 0, -30);
            p.Set("mixamorig:RightForeArm", 18, -22, 0);
            return p;
        }

        public static GameObject Spawn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        public static void ApplyPose(GameObject go, Pose p)
        {
            FindDeep(go.transform, "mixamorig:Hips").localPosition += p.hips;
            foreach (var kv in p.rot)
            {
                var t = FindDeep(go.transform, kv.Key);
                if (t == null) continue;
                var e = kv.Value;
                if (e.x != 0) t.Rotate(Vector3.right, e.x, Space.World);
                if (e.y != 0) t.Rotate(Vector3.up, e.y, Space.World);
                if (e.z != 0) t.Rotate(Vector3.forward, e.z, Space.World);
            }
            // Post-step: rigidly turn the already-posed lower body across the board,
            // then counter the torso so it keeps facing forward (knees/feet stay intact).
            // Per-pose lowerYaw lets actions (e.g. pushing) turn the feet forward and back.
            if (Mathf.Abs(p.lowerYaw) > 0.01f)
            {
                var hips = FindDeep(go.transform, "mixamorig:Hips");
                var spine = FindDeep(go.transform, "mixamorig:Spine");
                if (hips != null) hips.Rotate(Vector3.up, p.lowerYaw, Space.World);
                if (spine != null) spine.Rotate(Vector3.up, -p.lowerYaw * 0.8667f, Space.World);
            }
        }

        static void SetQ(AnimationClip clip, string path, float[] t, Quaternion[] q)
        {
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
            for (int i = 0; i < t.Length; i++) { cx.AddKey(t[i], q[i].x); cy.AddKey(t[i], q[i].y); cz.AddKey(t[i], q[i].z); cw.AddKey(t[i], q[i].w); }
            clip.SetCurve(path, typeof(Transform), "localRotation.x", cx);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", cz);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
        }
        static void SetV(AnimationClip clip, string path, float[] t, Vector3[] v)
        {
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve();
            for (int i = 0; i < t.Length; i++) { cx.AddKey(t[i], v[i].x); cy.AddKey(t[i], v[i].y); cz.AddKey(t[i], v[i].z); }
            clip.SetCurve(path, typeof(Transform), "localPosition.x", cx);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", cy);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", cz);
        }

        // Build (or overwrite in place) a clip from keyframe poses. frameNumbers are in frames at 24fps.
        public static AnimationClip BuildClip(string name, bool loop, float[] frameNumbers, Pose[] poses)
        {
            Directory.CreateDirectory(Application.dataPath + "/_Game/Animations/Skater");
            float[] times = new float[frameNumbers.Length];
            for (int i = 0; i < frameNumbers.Length; i++) times[i] = frameNumbers[i] / 24f;

            var bones = new HashSet<string>();
            foreach (var p in poses) foreach (var k in p.rot.Keys) bones.Add(k);
            bones.Add("mixamorig:Hips"); // capture hips rotation too (lower-body yaw)

            var rotKeys = new Dictionary<string, Quaternion[]>();
            var paths = new Dictionary<string, string>();
            var hipsPos = new Vector3[poses.Length];
            string hipsPath = "";

            for (int k = 0; k < poses.Length; k++)
            {
                var go = Spawn();
                ApplyPose(go, poses[k]);
                var hips = FindDeep(go.transform, "mixamorig:Hips");
                hipsPos[k] = hips.localPosition;
                hipsPath = AnimationUtility.CalculateTransformPath(hips, go.transform);
                foreach (var bn in bones)
                {
                    var tr = FindDeep(go.transform, bn);
                    if (!rotKeys.ContainsKey(bn)) { rotKeys[bn] = new Quaternion[poses.Length]; paths[bn] = AnimationUtility.CalculateTransformPath(tr, go.transform); }
                    rotKeys[bn][k] = tr.localRotation;
                }
                Object.DestroyImmediate(go);
            }

            string path = DIR + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip(); else clip.ClearCurves();
            clip.frameRate = 24;
            foreach (var bn in bones) SetQ(clip, paths[bn], times, rotKeys[bn]);
            SetV(clip, hipsPath, times, hipsPos);
            clip.EnsureQuaternionContinuity();
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, st);
            if (isNew) AssetDatabase.CreateAsset(clip, path); else EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            return clip;
        }

        // Lay out sampled instances of a clip as a strip for screenshotting. yaw rotates each (90 = side profile).
        public static void Strip(string clipName, int[] frames, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DIR + clipName + ".anim");
            GameObject old; int g = 0;
            while ((old = GameObject.Find("CRUISE_PREVIEW")) != null && g++ < 10) Object.DestroyImmediate(old);
            while ((old = GameObject.Find("POSE_TEST")) != null) Object.DestroyImmediate(old);
            var root = new GameObject("CRUISE_PREVIEW");
            for (int i = 0; i < frames.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = "pf" + frames[i];
                go.transform.SetParent(root.transform);
                go.transform.position = new Vector3(i * 0.85f, 0, 0);
                go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                clip.SampleAnimation(go, frames[i] / 24f);
            }
        }

        public static void CleanTemp()
        {
            string[] names = { "POSE_TEST", "CRUISE_PREVIEW", "PROBE" };
            foreach (var n in names) { GameObject o; int g = 0; while ((o = GameObject.Find(n)) != null && g++ < 40) Object.DestroyImmediate(o); }
        }
    }
}
