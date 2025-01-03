namespace _7DTDWebsockets
{
    public class Player
    {
        public string name;

        public Player(ClientInfo clientInfo)
        {
            this.name = clientInfo.playerName ?? "Unknown";
        }

        public Player(EntityPlayer player)
        {
            this.name = player.EntityName;
        }
    }
}