using HarmonyLib;

//original work done by KK
//modifications for patching weaponType and headshots added from Mustached_Maniac

namespace _7DTDWebsockets.patchs
{
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