"""One-shot patch: enemy arsenal expansion + ranged variants + fluid lengths."""
import io

def patch(path, pairs):
    with io.open(path, encoding='utf-8') as f:
        src = f.read()
    for old, new in pairs:
        assert old in src, f"missing in {path}: {old[:60]!r}"
        src = src.replace(old, new)
    with io.open(path, 'w', encoding='utf-8') as f:
        f.write(src)
    print('patched', path)

patch('Assets/Scripts/Enemies/EnemyAI.cs', [
    ("""        [SerializeField] protected int meleeVariants = 1;

        protected int PickMeleeTrigger()
        {
            if (meleeVariants <= 1) return HashAttack;
            int pick = Random.Range(0, meleeVariants);
            return pick == 0 ? HashAttack : Animator.StringToHash("Attack" + (pick + 1));
        }""",
     """        [SerializeField] protected int meleeVariants = 1;
        [SerializeField] protected int shootVariants = 1;

        protected int PickMeleeTrigger()
        {
            if (meleeVariants <= 1) return HashAttack;
            int pick = Random.Range(0, meleeVariants);
            return pick == 0 ? HashAttack : Animator.StringToHash("Attack" + (pick + 1));
        }

        protected int PickShootTrigger()
        {
            if (shootVariants <= 1) return HashShoot;
            int pick = Random.Range(0, shootVariants);
            return pick == 0 ? HashShoot : Animator.StringToHash("Shoot" + (pick + 1));
        }"""),
    ("""            if (animator != null) animator.SetTrigger(HashShoot);""",
     """            if (animator != null) animator.SetTrigger(PickShootTrigger());"""),
])

patch('Assets/Scripts/Enemies/SamuraiAI.cs', [
    ("StartMeleeAttack(meleeAttack, HashAttack);", "StartMeleeAttack(meleeAttack, PickMeleeTrigger());"),
    ("StartMeleeAttack(slash2, HashAttack);", "StartMeleeAttack(slash2, PickMeleeTrigger());"),
])

ATTACK_BAKE_OLD = """                var attackClips = attackSets
                    .Select((frames, i) => BakeAttackClip($"{p}_Attack{(i == 0 ? "" : (i + 1).ToString())}",
                        frames, idle, 0.5f, projectile: false))
                    .ToArray();
                var specialClip = BakeAttackClip($"{p}_Special", special, idle, 0.65f, projectile: false);
                var shootClip = BakeAttackClip($"{p}_Shoot", shoot, idle, 0.5f, projectile: true);"""
ATTACK_BAKE_NEW = """                // clip length grows with the strip so rich animations play out
                var attackClips = attackSets
                    .Select((frames, i) => BakeAttackClip($"{p}_Attack{(i == 0 ? "" : (i + 1).ToString())}",
                        frames, idle, Mathf.Max(0.5f, frames.Length / 24f + 0.14f), projectile: false))
                    .ToArray();
                var specialClip = BakeAttackClip($"{p}_Special", special, idle,
                    Mathf.Max(0.65f, special.Length / 24f + 0.16f), projectile: false);
                var shootClips = shootSets
                    .Select((frames, i) => BakeAttackClip($"{p}_Shoot{(i == 0 ? "" : (i + 1).ToString())}",
                        frames, idle, Mathf.Max(0.5f, frames.Length / 24f + 0.12f), projectile: true))
                    .ToArray();"""

patch('Assets/Editor/SpriteBaker.cs', [
    ("""            public string special = "attack2";
            public string shoot = "shoot";""",
     """            public string special = "attack2";
            public string[] shoots = { "shoot" };"""),
    ("""                attacks = new[] { "claw_slash", "double_claw", "bite" }, special = "shred_burst",
                shoot = "spine_blade", hurt = "light_hit" },""",
     """                attacks = new[] { "claw_slash", "double_claw", "bite", "pounce" }, special = "shred_burst",
                shoots = new[] { "spine_blade" }, hurt = "light_hit" },"""),
    ("""                attacks = new[] { "bite", "hyper_claw", "predator_pounce", "tail_whip" }, special = "tail_drill",
                shoot = "plasma_beam", hurt = "light_hit" },""",
     """                attacks = new[] { "bite", "hyper_claw", "predator_pounce", "tail_whip", "blood_rend", "spinning_claw", "tail_spike_combo", "frenzied_rush" },
                special = "tail_drill",
                shoots = new[] { "plasma_beam", "acid_spray", "homing_orbs", "missile_barrage" }, hurt = "light_hit" },"""),
    ("""                attacks = new[] { "melee_slash", "dash_strike" }, special = "melee_slash",
                hurt = "take_damage" },""",
     """                attacks = new[] { "melee_slash", "dash_strike" }, special = "melee_slash",
                shoots = new[] { "shoot" }, hurt = "take_damage" },"""),
    ("""                attacks = new[] { "punch_combo", "kick_combo", "slash_attack" }, special = "spin_slash",
                shoot = "rapid_fire", hurt = "light_hit" },""",
     """                attacks = new[] { "punch_combo", "kick_combo", "slash_attack", "uppercut", "energy_blade" }, special = "spin_slash",
                shoots = new[] { "rapid_fire", "charged_shot", "spread_shot" }, hurt = "light_hit" },"""),
    ("""                attacks = new[] { "slash1", "slash2", "thrust", "combo1", "combo2", "combo3" },
                special = "dragon_cut", shoot = "energy_wave", hurt = "light_hit",
                knockdown = "knocked_down", getup = "get_up", death = "death1" },""",
     """                attacks = new[] { "slash1", "slash2", "thrust", "combo1", "combo2", "combo3", "rising_slash", "pulse_slash" },
                special = "dragon_cut", shoots = new[] { "energy_wave" }, hurt = "light_hit",
                knockdown = "knocked_down", getup = "get_up", death = "death1" },"""),
    ("""                attacks = new[] { "slash1", "slash2", "thrust", "combo1", "combo2", "combo3" },
                special = "void_slash", shoot = "energy_wave", hurt = "light_hit",
                knockdown = "knocked_down", getup = "get_up", death = "death2" },""",
     """                attacks = new[] { "slash1", "slash2", "thrust", "combo1", "combo2", "combo3", "rising_slash", "blade_storm" },
                special = "void_slash", shoots = new[] { "energy_wave" }, hurt = "light_hit",
                knockdown = "knocked_down", getup = "get_up", death = "death2" },"""),
    ("""                attacks = new[] { "melee" }, special = "special", hurt = "hit_big", hurtHeavy = "hurt_heavy", death = "death_big" },
            new EnemyAnims { folder = "Bruiser", prefab = "Assets/Prefabs/Enemy_Bruiser.prefab",""",
     """                attacks = new[] { "melee" }, special = "special", shoots = new[] { "shoot" }, hurt = "hit_big", hurtHeavy = "hurt_heavy", death = "death_big" },
            new EnemyAnims { folder = "Bruiser", prefab = "Assets/Prefabs/Enemy_Bruiser.prefab","""),
    ("""                var special = LoadFrames($"{dir}/{enemy.special}");
                if (special.Length == 0) special = attackSets[attackSets.Count - 1];
                var shoot = LoadFrames($"{dir}/{enemy.shoot}");
                if (shoot.Length == 0) shoot = attackSets[0];""",
     """                var special = LoadFrames($"{dir}/{enemy.special}");
                if (special.Length == 0) special = attackSets[attackSets.Count - 1];
                var shootSets = enemy.shoots
                    .Select(folder => LoadFrames($"{dir}/{folder}"))
                    .Where(frames => frames.Length > 0)
                    .ToList();
                if (shootSets.Count == 0) shootSets.Add(attackSets[0]);"""),
    (ATTACK_BAKE_OLD, ATTACK_BAKE_NEW),
    ("""                var controller = BuildEnemyController(p, idleClip, walkClip, runClip, attackClips,
                    specialClip, shootClip, hurtClip, hurtHeavyClip, knockdownClip, getUpClip, deadClip);

                ApplyToPrefab(enemy.prefab, controller, idle[0], attackClips.Length);""",
     """                var controller = BuildEnemyController(p, idleClip, walkClip, runClip, attackClips,
                    specialClip, shootClips, hurtClip, hurtHeavyClip, knockdownClip, getUpClip, deadClip);

                ApplyToPrefab(enemy.prefab, controller, idle[0], attackClips.Length, shootClips.Count);"""),
    ("""            AnimationClip run, AnimationClip[] attacks, AnimationClip special, AnimationClip shoot,
            AnimationClip hurt, AnimationClip hurtHeavy, AnimationClip knockdown, AnimationClip getUp, AnimationClip dead)""",
     """            AnimationClip run, AnimationClip[] attacks, AnimationClip special, AnimationClip[] shoots,
            AnimationClip hurt, AnimationClip hurtHeavy, AnimationClip knockdown, AnimationClip getUp, AnimationClip dead)"""),
    ("""            AddTriggerState("Special", special);
            AddTriggerState("Shoot", shoot);
            AddTriggerState("Hurt", hurt);""",
     """            AddTriggerState("Special", special);
            for (int i = 0; i < shoots.Length; i++)
                AddTriggerState(i == 0 ? "Shoot" : "Shoot" + (i + 1), shoots[i]);
            AddTriggerState("Hurt", hurt);"""),
    ("""        private static void ApplyToPrefab(string prefabPath, AnimatorController controller, Sprite idleSprite,
            int meleeVariants = 1)
        {""",
     """        private static void ApplyToPrefab(string prefabPath, AnimatorController controller, Sprite idleSprite,
            int meleeVariants = 1, int shootVariants = 1)
        {"""),
    ("""                    var prop = so.FindProperty("meleeVariants");
                    if (prop != null)
                    {
                        prop.intValue = meleeVariants;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }""",
     """                    var prop = so.FindProperty("meleeVariants");
                    if (prop != null) prop.intValue = meleeVariants;
                    var sprop = so.FindProperty("shootVariants");
                    if (sprop != null) sprop.intValue = shootVariants;
                    so.ApplyModifiedPropertiesWithoutUndo();"""),
])
print('arsenal patch complete')
