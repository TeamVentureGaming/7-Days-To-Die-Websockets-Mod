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

    [HarmonyPatch]
    public static class DamagePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetPackageDamageEntity), "ProcessPackage")]
        public static void DamageEntityPacketProccessPrefix(NetPackage __instance, World _world, GameManager _callbacks)
        {
            // TODO: seems like maybe this should be pattern matching on the type?
            var packageName = NetPackageManager.GetPackageName(__instance.PackageId);
            DebugLog.Out(() => $"[DamagePatches] DamageEntityPacketProccessPrefix => package id: {__instance.PackageId}, package name: {packageName}");
            if (packageName != nameof(NetPackageDamageEntity))
            {
                return;
            }

            var damage = (NetPackageDamageEntity)__instance;
            var entity = _world.GetEntity(damage.entityId);
            if (entity == null || entity is not EntityPlayer entityPlayer)
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
            if (__instance is not EntityPlayer player)
            {
                return;
            }

            DebugLog.Out("[DamagePatches] EntityAliveDamagePrefix sending PlayerDamage event.");
            API.Send("PlayerDamage", new PlayerDmgEvent(new Player(player), _damageSource.damageType.ToString(), _strength));
            return;
        }
    }
}