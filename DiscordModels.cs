using System;

namespace maimai_rpc
{
    public class DiscordActivity
    {
        public string Details { get; set; }
        public string State { get; set; }
        public long? StartTimestamp { get; set; }
        public long? EndTimestamp { get; set; }
        public string LargeImageKey { get; set; }
        public string LargeImageText { get; set; }
        public string SmallImageKey { get; set; }
        public string SmallImageText { get; set; }
        public int Type { get; set; } // 0 = Playing

        public DiscordActivity()
        {
            Type = 0;
        }
    }

    public class DiscordUser
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string Avatar { get; set; }

        public string GetAvatarUrl()
        {
            return GetAvatarUrl("png", 128);
        }

        public string GetAvatarUrl(string format, int size)
        {
            if (string.IsNullOrEmpty(Id) || string.IsNullOrEmpty(Avatar))
                return null;

            return string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.{2}?size={3}", Id, Avatar, format, size);
        }
    }
}
