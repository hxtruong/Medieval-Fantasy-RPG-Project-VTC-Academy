﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class WeaponSystem : MonoBehaviour
{
    [Range(.1f, 1.0f)] [SerializeField] float criticalHitChance = 0.1f;
    [SerializeField] float criticalHitMultiplier = 1.25f;

    [SerializeField] WeaponConfig currentWeaponConfig;
    [SerializeField] ProjectileConfig currentProjectileConfig;
    

    const string TEMP_OBJECTS = "TempObjects";
    const string ATTACK_TRIGGER = "Attack";
    const string ATTACK_SPEED = "AttackSpeed";
    const string DEFAULT_ATTACK = "DEFAULT ATTACK";

    GameObject target;
    GameObject weaponObject;
    Animator animator;
    AnimationClip attackClip;
    Character character;
    InventorySystem inventorySystem;
    float lastHitTime; 

    public WeaponConfig GetCurrentWeapon()
    {
        return currentWeaponConfig;
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        character = GetComponent<Character>();
        inventorySystem = GetComponent<InventorySystem>();

        PutWeaponInHand(currentWeaponConfig);
        SetAttackAnimation();      
    }

    private GameObject RequestRighHand()
    {
        var dominantHands = GetComponentsInChildren<RightHand>();
        int numberOfDominantHands = dominantHands.Length;
        Assert.IsFalse(numberOfDominantHands <= 0, "Khong tim thay RightHand script nao tren " + gameObject.name);
        Assert.IsFalse(numberOfDominantHands > 1, "Co qua nhieu RightHand script tren " + gameObject.name + " xoa bot di.");
        return dominantHands[0].gameObject;
    }

    private GameObject RequestLeftHand()
    {
        var dominantHands = GetComponentsInChildren<LeftHand>();
        int numberOfDominantHands = dominantHands.Length;
        Assert.IsFalse(numberOfDominantHands <= 0, "Khong tim thay LeftHand script nao tren " + gameObject.name);
        Assert.IsFalse(numberOfDominantHands > 1, "Co qua nhieu LeftHand script tren " + gameObject.name + " xoa bot di.");
        return dominantHands[0].gameObject;
    }

    public void PutWeaponInHand(WeaponConfig weaponToUse)
    {
        currentWeaponConfig = weaponToUse;
        var weaponPrefab = weaponToUse.GetWeaponPrefab();
        GameObject dominantHand;
        if (currentWeaponConfig.IsRightHandWeapon())
        {
            dominantHand = RequestRighHand();
        }
        else
        {
            dominantHand = RequestLeftHand();
        }
        Destroy(weaponObject);
        weaponObject = Instantiate(weaponPrefab, dominantHand.transform);
        weaponObject.transform.localPosition = currentWeaponConfig.gripTransform.localPosition;
        weaponObject.transform.localRotation = currentWeaponConfig.gripTransform.localRotation;
    }

    private GameObject SpawnProjectile()
    {
        var projectilePrefab = currentProjectileConfig.GetProjectilePrefab();
        var firingPos = GetComponentInChildren<ArrowShootingPosition>();
        var projectileObject = Instantiate(projectilePrefab, firingPos.transform);

        var projectile = projectileObject.GetComponentInChildren<Projectile>();
        projectile.SetProjectileConfig(currentProjectileConfig);
        projectile.SetShooter(gameObject);

        projectileObject.transform.parent = GameObject.FindGameObjectWithTag(TEMP_OBJECTS).transform;
        return projectileObject;
    }

    IEnumerator MoveProjectile(GameObject projectile, Vector3 from, Vector3 target, float speed, float vanishAfterSec)
    {
        float startTime = Time.time;
        var normalizeDirection = (target - from).normalized;
        var vanishTime = Time.time + vanishAfterSec;
        projectile.transform.LookAt(target);

        while (Time.time < vanishTime && projectile != null)
        {
            projectile.transform.position += normalizeDirection * (Time.deltaTime * speed);
            yield return null;
        }
        Destroy(projectile);
    }

    public void SetProjectileDirection(ProjectileConfig projectileToUse)
    {
        currentProjectileConfig = projectileToUse;
        var projectileObject = SpawnProjectile();

        var targetToShoot = target.GetComponentInChildren<MainBody>();
        var targetCenter = targetToShoot.GetComponentInChildren<Renderer>().bounds.center;

        StartCoroutine(MoveProjectile(projectileObject,
                                      projectileObject.transform.position,
                                      targetCenter,
                                      currentProjectileConfig.GetProjectileSpeed(),
                                      currentProjectileConfig.GetVanishTime()));
    }

    private void ShootArrow()
    {
        SetProjectileDirection(currentProjectileConfig);
    }

    private void SetAttackAnimation()
    {
        if (!character.GetOverrideController())
        {
            Debug.Break();
            Debug.LogAssertion("Them animation override controller vao " + gameObject);
        }
        else
        {
            var animatorOverrideController = character.GetOverrideController();
            animator.runtimeAnimatorController = animatorOverrideController;
            attackClip = currentWeaponConfig.GetAttackAnimClip();
            animatorOverrideController[DEFAULT_ATTACK] = attackClip;

            animator.SetFloat(ATTACK_SPEED, currentWeaponConfig.GetAttackAnimSpeedMultiplier());
        }
    }

    public void CancleAction()
    {
        character.SetDestination(character.transform.position);
        animator.Play("Grounded");
    }

    private void Hit()
    {
        target.GetComponent<HealthSystem>().TakeDamage(NormalAttackDamage());
    }

    public void StopAttacking()
    {
        //TODO impliment
    }

    public void AttackTarget(GameObject targetToAttack)
    {
        if (weaponObject == null)
            PutWeaponInHand(currentWeaponConfig);

        target = targetToAttack;

        bool attackerStillAlive = GetComponent<HealthSystem>().healthAsPercentage > 0;
        bool targetStillAlive = target.GetComponent<HealthSystem>().healthAsPercentage > 0;

        if (attackerStillAlive && targetStillAlive)
        {
            float weaponHitPeriod = attackClip.length;
            float timeToWait = weaponHitPeriod + currentWeaponConfig.GetMinTimeBetweenHits();

            if (Time.time - lastHitTime >= timeToWait)
            {
                lastHitTime = Time.time;
                RunAnimationAttackOnce();
            }
        }
        if (character.GetComponent<PlayerControl>())
        {
            character.CurrentState = CharacterState.attacking;
        }
    }

    private void RunAnimationAttackOnce()
    {
        transform.LookAt(target.transform);
        SetAttackAnimation();
        animator.SetTrigger(ATTACK_TRIGGER);
    }

    public int GetWeaponDamage()
    {
        return Random.Range(currentWeaponConfig.GetMinDamage(), currentWeaponConfig.GetMaxDamage());
    }

    public float NormalAttackDamage()
    {
        bool isCriticalHit = Random.Range(0f, 1f) <= criticalHitChance;
        float damageBeforeCritical = character.GetBaseDamage() + GetWeaponDamage();
        if (isCriticalHit)
        {
            PlayCriticalHitParticle();
            return damageBeforeCritical * criticalHitMultiplier;
        }
        else
        {
            return damageBeforeCritical;
        }
    }

    protected void PlayCriticalHitParticle()
    {
        var particlePrefab = currentWeaponConfig.GetCriticalHitPrefab();
        var particleObject = Instantiate
        (
            particlePrefab,
            target.transform.position,
            particlePrefab.transform.rotation
        );
        particleObject.transform.parent = target.transform;
        particleObject.transform.parent = GameObject.FindGameObjectWithTag(TEMP_OBJECTS).transform;
        particleObject.GetComponent<ParticleSystem>().Play();
        particleObject.transform.parent = GameObject.FindGameObjectWithTag(TEMP_OBJECTS).transform;
        StartCoroutine(DestroyParticleAfterFinishedSec(particleObject));
    }

    IEnumerator DestroyParticleAfterFinishedSec(GameObject particlePrefab)
    {
        while (particlePrefab.GetComponent<ParticleSystem>().isPlaying)
        {
            yield return new WaitForSeconds(currentWeaponConfig.GetDestroyParticleTime());
        }
        Destroy(particlePrefab);
    }

    public void DestroyWeaponObject()
    {
        Destroy(weaponObject);
    }
}