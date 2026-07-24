using System;
using Mojinloop.Data;
using UnityEngine;

namespace Mojinloop.Combat
{
    [RequireComponent(typeof(Animator))]
    public sealed class MonsterController : MonoBehaviour
    {
        static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int DieHash = Animator.StringToHash("Die");

        public event Action<MonsterController, int> Damaged;
        public event Action<MonsterController> Died;
        public bool IsDead { get; private set; }
        public int Hp { get; private set; }
        public MonsterData Data { get; private set; }

        Animator animator;
        SpriteRenderer spriteRenderer;
        bool moved;
        float animateUntil;

        void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(MonsterData data)
        {
            Data = data;
            Hp = data.maxHp;
            IsDead = false;
            moved = false;
            animateUntil = 0f;
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            animator.speed = 1f;
            animator.ResetTrigger(HitHash);
            animator.ResetTrigger(DieHash);
            animator.SetBool(IsMovingHash, true);
            animator.Play("Move", 0, 0f);
            spriteRenderer.color = Color.white;
            // The revised monster sheet already faces left.
            spriteRenderer.flipX = false;
            gameObject.SetActive(true);
            transform.localScale = Vector3.one;
        }

        public void Face(Vector3 target) { }

        public void Move(float delta)
        {
            if (IsDead) return;
            transform.position += Vector3.left * Data.moveSpeed * delta;
            moved = true;
        }

        void LateUpdate()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.flipX = false;
            if (animator == null) return;

            animator.SetBool(IsMovingHash, true);
            animator.speed = moved || IsDead || Time.unscaledTime < animateUntil ? 1f : 0f;
            moved = false;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0, Hp - amount);
            Damaged?.Invoke(this, amount);
            animator.speed = 1f;
            if (Hp <= 0)
            {
                IsDead = true;
                animator.SetTrigger(DieHash);
                Died?.Invoke(this);
                return;
            }

            animateUntil = Time.unscaledTime + .24f;
            animator.SetTrigger(HitHash);
        }
    }
}
