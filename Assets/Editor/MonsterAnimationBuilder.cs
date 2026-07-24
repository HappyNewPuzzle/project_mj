using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Mojinloop.Editor
{
    internal static class MonsterAnimationBuilder
    {
        private const string TexturePath = "Assets/Reference/monster.png";
        private const string ControllerPath = "Assets/Data/MonsterAnimator.controller";
        private const string MaterialPath = "Assets/Data/MonsterBlackKey.mat";

        internal static void Prepare()
        {
            Slice();
            var idle = Clip("Assets/Data/MonsterIdle.anim", Sprites("monster_idle"), 6, true);
            var move = Clip("Assets/Data/MonsterMove.anim", Sprites("monster_move"), 10, true);
            var hit = Clip("Assets/Data/MonsterHit.anim", Sprites("monster_hit"), 10, false);
            var die = Clip("Assets/Data/MonsterDie.anim", Sprites("monster_die"), 8, false);
            Controller(idle, move, hit, die);
            PrepareMaterial();
        }

        internal static Sprite IdleSprite()
        {
            var sprites = Sprites("monster_idle");
            if (sprites.Length == 0) throw new InvalidOperationException("monster.png에서 Idle Sprite를 생성하지 못했습니다. Rebuild를 다시 실행하세요.");
            return sprites[0];
        }

        internal static void ConfigurePrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            root.GetComponent<SpriteRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void Slice()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("monster.png TextureImporter was not found.");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var rects = new SpriteRect[16];
            // 현재 2400x1309 시트의 좌측 상단 초록 드래곤 영역입니다.
            // 1704x923 sheet: green dragon in the upper-left group.
            // Coordinates use Unity's bottom-left texture origin.
            for (var i = 0; i < 3; i++)
                rects[i] = SpriteRect($"monster_idle_{i}", 24 + 85 * i, 788, 76, 85);
            for (var i = 0; i < 6; i++)
                rects[3 + i] = SpriteRect(
                    $"monster_move_{i}",
                    i < 3 ? 365 + 88 * i : 368 + 93 * (i - 3),
                    i < 3 ? 788 : 693,
                    82,
                    85);
            for (var i = 0; i < 2; i++)
                rects[9 + i] = SpriteRect($"monster_hit_{i}", 638 + 93 * i, 788, 84, 85);
            for (var i = 0; i < 5; i++)
                rects[11 + i] = SpriteRect($"monster_die_{i}", 370 + 91 * i, 603, 84, 55);
            provider.SetSpriteRects(rects); provider.Apply(); importer.SaveAndReimport();
        }

        private static SpriteRect SpriteRect(string name, float x, float y, float width, float height) => new() { name = name, rect = new Rect(x, y, width, height), alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, 0), spriteID = GUID.Generate() };
        private static Sprite[] Sprites(string prefix) => AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().Where(s => s.name.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(s => s.name).ToArray();

        private static void PrepareMaterial()
        {
            var shader = Shader.Find("Mojinloop/Monster Black Key");
            if (shader == null)
                throw new InvalidOperationException("Monster Black Key shader was not imported.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MonsterBlackKey" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
        }

        private static AnimationClip Clip(string path, Sprite[] sprites, float rate, bool loop)
        {
            if (sprites.Length == 0) throw new InvalidOperationException($"{path}에 사용할 Sprite가 없습니다.");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.frameRate = rate;
            var frames = new ObjectReferenceKeyframe[sprites.Length];
            for (var i = 0; i < sprites.Length; i++) frames[i] = new ObjectReferenceKeyframe { time = i / rate, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip); return clip;
        }

        private static void Controller(AnimationClip idle, AnimationClip move, AnimationClip hit, AnimationClip die)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle"); idleState.motion = idle; machine.defaultState = idleState;
            var moveState = machine.AddState("Move"); moveState.motion = move;
            var hitState = machine.AddState("Hit"); hitState.motion = hit;
            var dieState = machine.AddState("Die"); dieState.motion = die;
            BoolTransition(idleState, moveState, true); BoolTransition(moveState, idleState, false);
            var hitTransition = machine.AddAnyStateTransition(hitState); hitTransition.hasExitTime = false; hitTransition.duration = .03f; hitTransition.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            var hitReturn = hitState.AddTransition(moveState); hitReturn.hasExitTime = true; hitReturn.exitTime = 1; hitReturn.duration = 0f;
            var dieTransition = machine.AddAnyStateTransition(dieState); dieTransition.hasExitTime = false; dieTransition.duration = .03f; dieTransition.AddCondition(AnimatorConditionMode.If, 0, "Die");
            EditorUtility.SetDirty(controller);
        }

        private static void BoolTransition(AnimatorState from, AnimatorState to, bool moving)
        {
            var transition = from.AddTransition(to); transition.hasExitTime = false; transition.duration = .05f;
            transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "IsMoving");
        }
    }
}
