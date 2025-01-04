using System.Text.RegularExpressions;
using HarmonyLib;

//original work done by KK
//modifications for patching weaponType and headshots added from Mustached_Maniac

namespace _7DTDWebsockets.patchs
{
    public sealed class PlayerDmgEvent
    {
        public readonly Player player;
        public readonly string cause;
        public readonly int damage;

        public PlayerDmgEvent(Player player, string cause, int damage)
        {
            this.player = player;
            this.cause = cause;
            this.damage = damage;
        }
    }

    public sealed class PlayerKillEntityEvent
    {
        public readonly Player player;
        public readonly string entity;
        public readonly bool animal;
        public readonly bool zombie;
        public readonly string weaponType;
        public readonly bool headshot;

        public PlayerKillEntityEvent(Player player, string entity, bool animal, bool zombie, string weaponType, bool headshot)
        {
            this.player = player;
            this.entity = entity;
            this.animal = animal;
            this.zombie = zombie;
            this.weaponType = weaponType;
            this.headshot = headshot;
        }
    }

    public sealed class PlayerEntityEvent
    {
        public readonly Player player;
        public readonly string entity;

        public PlayerEntityEvent(Player player, string entity)
        {
            this.player = player;
            this.entity = entity;
        }
    }

    public sealed class PlayerOnlyEvent
    {
        public readonly Player player;

        public PlayerOnlyEvent(Player player)
        {
            this.player = player;
        }
    }

    public sealed class PlayerDeathEvent
    {
        public readonly Player player;

        public PlayerDeathEvent(Player player)
        {
            this.player = player;
        }
    }

    [HarmonyPatch(typeof(EntityAlive), "SetDead")]
    public static /*partial*/ class PatchEntityDeath
    {
        // if the [GeneratedRegex] attribute can't be used, still prefer static, compiled Regex fields
        private static readonly Regex animalEntityNameRegex = new Regex(@"(animal)", RegexOptions.Compiled);
        private static readonly Regex zombieEntityNameRegex = new Regex(@"(zombie)", RegexOptions.Compiled);

        // using the [GeneratedRegex] attribute, the regex is compiled at build time!
        //[GeneratedRegex(@"(animal)")]
        //private static partial Regex AnimalEntityNameRegex();

        //[GeneratedRegex(@"(zombie)")]
        //private static partial Regex ZombieEntityNameRegex();

        public static bool IsHeadshot; // TODO: this seems very, very wrong

        public static bool Prefix(EntityAlive __instance)
        {
            DebugLog.Out("[PatchEntityDeath] Prefix start");
            if (__instance is EntityPlayer)
            {
                DebugLog.Out("[PatchEntityDeath] instance is EntityPlayer, returning true");
                // TODO: send player died event
                return true;
            }

            DebugLog.Out("[PatchEntityDeath] traversing for entityThatKilledMe");
            var obj = Traverse.Create(__instance).Field("entityThatKilledMe").GetValue();
            if (obj == null)
            {
                DebugLog.Out("[PatchEntityDeath] traverse found no match, returning true");
                return true;
            }

            if (!(obj is EntityPlayer player))
            {
                DebugLog.Out("[PatchEntityDeath] found entityThatKilledMe but it was not a EntityPlayer, returning true");
                return true;
            }

            string entityNameLower = __instance.GetDebugName().ToLower();
// avoid formatting the message if not in debug mode
#if DEBUG
            Log.Out($"[PatchEntityDeath] entityNameLower: {entityNameLower}");
#endif

            bool isAnimal = false;
            bool isZombie = false;

            if (entityNameLower.Contains("animal"))
            {
                DebugLog.Out("[PatchEntityDeath] entityNameLower contains animal");
                isAnimal = true;
                entityNameLower = animalEntityNameRegex.Replace(entityNameLower, "");
// avoid formatting the message if not in debug mode
#if DEBUG
                Log.Out($"[PatchEntityDeath] entityNameLower after animalEntityNameRegex: {entityNameLower}");
#endif
            }

            if (entityNameLower.Contains("zombie"))
            {
                DebugLog.Out("[PatchEntityDeath] entityNameLower contains zombie");
                isZombie = true;
                entityNameLower = zombieEntityNameRegex.Replace(entityNameLower, "");
// avoid formatting the message if not in debug mode
#if DEBUG
                Log.Out($"[PatchEntityDeath] entityNameLower after zombieEntityNameRegex: {entityNameLower}");
#endif
            }

            DebugLog.Out("[PatchEntityDeath] sending PlayerKillEntity event");
            API.Send("PlayerKillEntity", new PlayerKillEntityEvent(new Player(player), entityNameLower, isAnimal, isZombie, player.inventory.holdingItem.Name, IsHeadshot));

            if (isZombie)
            {
                DebugLog.Out("[PatchEntityDeath] sending PlayerKillZombie event");
                API.Send("PlayerKillZombie", new PlayerEntityEvent(new Player(player), entityNameLower));
            }
            else if (isAnimal)
            {
                DebugLog.Out("[PatchEntityDeath] sending PlayerKillAnimal event");
                API.Send("PlayerKillAnimal", new PlayerEntityEvent(new Player(player), entityNameLower));
            }

            DebugLog.Out("[PatchEntityDeath] Prefix end, returning true");
            return true;
        }
    }

    [HarmonyPatch(typeof(EntityAlive), "damageEntityLocal")]
    public static class PatchDamageEntityLocal
    {
        public static void Postfix(DamageResponse __result)
        {
            var isHeadshot = __result.HitBodyPart == EnumBodyPartHit.Head;
            PatchEntityDeath.IsHeadshot = isHeadshot; // TODO: this seems very, very wrong
// avoid formatting the message if not in debug mode
#if DEBUG
            Log.Out($"[PatchDamageEntityLocal] Postfix, isHeadshot: {isHeadshot}");
#endif
        }
    }

    [HarmonyPatch]
    public static class DamagePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetPackageDamageEntity), "ProcessPackage")]
        public static void DamageEntityPacketProccessPrefix(NetPackage __instance, World _world, GameManager _callbacks)
        {
            // TODO: seems like maybe this should be pattern matching on the type?
            var packageName = NetPackageManager.GetPackageName(__instance.PackageId);
// avoid formatting the message if not in debug mode
#if DEBUG
            Log.Out($"[DamagePatches] DamageEntityPacketProccessPrefix => package id: {__instance.PackageId}, package name: {packageName}");
#endif
            if (packageName != nameof(NetPackageDamageEntity))
            {
                return;
            }

            var damage = (NetPackageDamageEntity)__instance;
            var entity = _world.GetEntity(damage.entityId);
            if (entity == null || !(entity is EntityPlayer entityPlayer))
            {
                return;
            }

            DebugLog.Out("[DamagePatches] DamageEntityPacketProccessPrefix sending PlayerDamage event.");
            API.Send("PlayerDamage", new PlayerDmgEvent(new Player(entityPlayer), damage.damageTyp.ToString(), damage.strength));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityAlive), "DamageEntity")]
        public static void EntityAliveDamagePrefix(EntityAlive __instance, DamageSource _damageSource, int _strength, bool _criticalHit, float _impulseScale = 1f)
        {
            if (!(__instance is EntityPlayer player))
            {
                return;
            }

            DebugLog.Out("[DamagePatches] EntityAliveDamagePrefix sending PlayerDamage event.");
            API.Send("PlayerDamage", new PlayerDmgEvent(new Player(player), _damageSource.damageType.ToString(), _strength));
            return;
        }
    }
}
