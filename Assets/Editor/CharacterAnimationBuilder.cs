using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Mojinloop.Editor
{
    internal static class CharacterAnimationBuilder
    {
        private const string TexturePath = "Assets/Reference/character.png";
        private const string ControllerPath = "Assets/Data/HeroAnimator.controller";

        internal static void Prepare()
        {
            SliceCharacter();
            var idle = Sprites("hero_idle");
            var run = Sprites("hero_run");
            var attack = Sprites("hero_attack");
            var idleClip = Clip("Assets/Data/HeroIdle.anim", idle, 8, true);
            var runClip = Clip("Assets/Data/HeroRun.anim", run, 12, true);
            var attackClip = Clip("Assets/Data/HeroAttack.anim", attack, 12, true);
            CreateController(idleClip, runClip, attackClip);
        }

        internal static Sprite IdleSprite()
        {
            return Sprites("hero_idle").First();
        }

        internal static void ConfigureHeroPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SliceCharacter()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("character.png TextureImporter was not found.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = new SpriteRect[18];
            for (var i = 0; i < 6; i++) rects[i] = Rect($"hero_idle_{i}", 42 + 135 * i, 598, 100, 138);
            for (var i = 0; i < 8; i++) rects[6 + i] = Rect($"hero_run_{i}", 42 + 135 * i, 425, 115, 142);
            for (var i = 0; i < 4; i++) rects[14 + i] = Rect($"hero_attack_{i}", 42 + 135 * i, 232, 120, 150);
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static SpriteRect Rect(string name, float x, float y, float width, float height)
        {
            return new SpriteRect { name = name, rect = new Rect(x, y, width, height), alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, 0), spriteID = GUID.Generate() };
        }

        private static Sprite[] Sprites(string prefix)
        {
            return AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().Where(s => s.name.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(s => s.name).ToArray();
        }

        private static AnimationClip Clip(string path, Sprite[] sprites, float frameRate, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.frameRate = frameRate;
            var frames = new ObjectReferenceKeyframe[sprites.Length];
            for (var i = 0; i < sprites.Length; i++) frames[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void CreateController(AnimationClip idle, AnimationClip run, AnimationClip attack)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle"); idleState.motion = idle; machine.defaultState = idleState;
            var runState = machine.AddState("Run"); runState.motion = run;
            var attackState = machine.AddState("Attack"); attackState.motion = attack;
            Transition(idleState, runState, "IsRunning", true);
            Transition(runState, idleState, "IsRunning", false);
            Transition(idleState, attackState, "IsAttacking", true);
            Transition(runState, attackState, "IsAttacking", true);
            Transition(attackState, idleState, "IsAttacking", false);
            EditorUtility.SetDirty(controller);
        }

        private static void Transition(AnimatorState from, AnimatorState to, string parameter, bool value)
        {
            var transition = from.AddTransition(to); transition.hasExitTime = false; transition.duration = .05f;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
        }
    }
}
