using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace _7DTDWebsockets
{
    [HarmonyPatch(typeof(EntityAlive), "SetDead")]
    public static partial class PatchEntityDeath
    {
        // if the [GeneratedRegex] attribute can't be used, still prefer static, compiled Regex fields
        //private static readonly Regex animalEntityNameRegex = new (@"(animal)", RegexOptions.Compiled);
        //private static readonly Regex zombieEntityNameRegex = new (@"(zombie)", RegexOptions.Compiled);

        // using the [GeneratedRegex] attribute, the regex is compiled at build time!
        [GeneratedRegex(@"(animal)")]
        private static partial Regex AnimalEntityNameRegex();

        [GeneratedRegex(@"(zombie)")]
        private static partial Regex ZombieEntityNameRegex();

        public static bool IsHeadshot; // TODO: this seems very, very wrong

        public static bool Prefix(EntityAlive __instance)
        {
            if (__instance is EntityPlayer)
            {
                return true;
            }

            var obj = Traverse.Create(__instance).Field("entityThatKilledMe").GetValue();
            if (obj == null)
            {
                return true;
            }

            if (obj is not EntityPlayer player)
            {
                return true;
            }

            string entityNameLower = __instance.GetDebugName().ToLower();

            bool isAnimal = false;
            bool isZombie = false;

            if (entityNameLower.Contains("animal"))
            {
                isAnimal = true;
                AnimalEntityNameRegex().Replace(entityNameLower, "");
            }

            if (entityNameLower.Contains("zombie"))
            {
                isZombie = true;
                ZombieEntityNameRegex().Replace(entityNameLower, "");
            }

            API.Send("PlayerKillEntity", JsonSerializer.Serialize(new PlayerKillEntityEvent(new Player(player), entityNameLower, isAnimal, isZombie, player.inventory.holdingItem.Name, IsHeadshot)));

            if (isZombie)
            {
                API.Send("PlayerKillZombie", JsonSerializer.Serialize(new PlayerEntityEvent(new Player(player), entityNameLower)));
            }
            else if (isAnimal)
            {
                API.Send("PlayerKillAnimal", JsonSerializer.Serialize(new PlayerEntityEvent(new Player(player), entityNameLower)));
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(EntityAlive), "damageEntityLocal")]
    public static class PatchDamageEntityLocal
    {
        public static void Postfix(DamageResponse __result)
        {
            PatchEntityDeath.IsHeadshot = __result.HitBodyPart == EnumBodyPartHit.Head;
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
            if (NetPackageManager.GetPackageName(__instance.PackageId) != nameof(NetPackageDamageEntity))
            {
                return;
            }

            NetPackageDamageEntity damage = (NetPackageDamageEntity)__instance;
            Entity entity = _world.GetEntity(damage.entityId);
            if (entity == null || entity is not EntityPlayer entityPlayer)
            {
                return;
            }

            API.Send("PlayerDamage", JsonSerializer.Serialize(new PlayerDmgEvent(new Player(entityPlayer), damage.damageTyp.ToString(), damage.strength)));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityAlive), "DamageEntity")]
        public static void EntityAliveDamagePrefix(EntityAlive __instance, DamageSource _damageSource, int _strength, bool _criticalHit, float _impulseScale = 1f)
        {
            if (__instance is not EntityPlayer player)
            {
                return;
            }

            API.Send("PlayerDamage", JsonSerializer.Serialize(new PlayerDmgEvent(new Player(player), _damageSource.damageType.ToString(), _strength)));
            return;
        }
    }
}