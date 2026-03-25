namespace ChatNest.Shared.DTOs
{
    public class RedisConfiguration
    {
        public bool AllowAdmin { get; set; }
        public bool Ssl { get; set; }
        public int ConnectTimeout { get; set; }
        public int ConnectRetry { get; set; }
        public int Database { get; set; }
        public RedisHost[] Hosts { get; set; }
        public RedisHost Host => Hosts[0];
    }

}
