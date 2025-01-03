namespace _7DTDWebsockets
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
}