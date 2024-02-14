using System.Data.Entity;
using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Discord.WebSocket;
using System.Diagnostics;
using CharacterEngineDiscord.Interfaces;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireHosterAccess]
    [Group("admin", "Admin commands")]
    public class AdminCommands(IIntegrationsService integrations, IDiscordClient client) : InteractionModuleBase<InteractionContext>
    {
        private readonly DiscordSocketClient _client = (DiscordSocketClient)client;

        [SlashCommand("status", "[OWNER BOT] Ve mis estadisticas")]
        public async Task AdminStatus(bool silent = true)
        {
            await DeferAsync(ephemeral: silent);
            var time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

            var tasks = integrations.RunningCaiTasks;
            await using var db = new StorageContext();
            string text = $"# Langue originale par @__olivercastillon__ #\n" +
                          $"<:fr_separador1:1182802278030266380> **En ligne depuis**: `{time.Days}d/{time.Hours}h/{time.Minutes}m`\n" +
                          $"<:fr_separador2:1182802280769138718> **Messages**: `{integrations.MessagesSent}`\n" +
                          $"<:fr_separador3:1182802282664960090> **Bloqué**: `{db.BlockedUsers.Count(bu => bu.GuildId == null)} user(s)` | `{db.BlockedGuilds.Count()} serveur(s)`\n" +
                          $"<:fr_separador5:1182802286179799050> **Statistiques**: `{integrations.WebhookClients.Count}wh / {integrations.Conversations.Count}cv / {tasks.Count}ct`";

            await FollowupAsync(embed: text.ToInlineEmbed(0xf5f5f5, false), ephemeral: silent);
        }


        [SlashCommand("list-servers", "[OWNER BOT] Ve la lista de los servidores en donde me encuentro.")]
        public async Task AdminListServers(int page = 1, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var embed = new EmbedBuilder().WithColor(Color.Green);

            int start = (page - 1) * 10;
            int end = (_client.Guilds.Count - start) > 10 ? (start + 9) : start + (_client.Guilds.Count - start - 1);

            var guilds = _client.Guilds.OrderBy(g => g.MemberCount).Reverse();

            for (int i = start; i <= end; i++)
            {
                var guild = guilds.ElementAt(i);
                var guildOwner = await _client.GetUserAsync(guild.OwnerId);
                string val = $"<:fr_dash1:1182802212448112731> **ID**: {guild.Id}\n" +
                             $"{(guild.Description is string desc ? $"<:fr_dash3:1182802215744835655> **Descripción**: \"{desc[0..Math.Min(200, desc.Length - 1)]}\"\n" : "")}" +
                             $"<:fr_dash4:1182802219431628911> *Propietario**: {guildOwner?.Username}{(guildOwner?.GlobalName is string gn ? $" ({gn})" : "")}\n" +
                             $"<:fr_dash5:1182802220832538624> **Members**: {guild.MemberCount}";
                embed.AddField(guild.Name, val);
            }
            double pages = Math.Ceiling(_client.Guilds.Count / 10d);
            embed.WithTitle($"Servidores: {_client.Guilds.Count}");
            embed.WithFooter($"Pagina {page}/{pages}");

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }

        [SlashCommand("block-server", "[OWNER BOT] Bloquea un servidor.")]
        public async Task AdminBlockGuild(string serverId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            ulong guildId = ulong.Parse(serverId.Trim());
            var guild = await db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                await FollowupAsync(embed: $"<a:fr_error:1182414048793481236> Servidor no encontrado.".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            if ((await db.BlockedGuilds.FindAsync(guild.Id)) is not null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} ¡Servidor bloqueado!".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            await db.BlockedGuilds.AddAsync(new() { Id = guildId });
            db.Guilds.Remove(guild); // Remove from db
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Server was removed from the database".ToInlineEmbed(Color.Red), ephemeral: silent);

            // Leave
            var discordGuild = _client.GetGuild(guildId);

            if (discordGuild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to leave the server".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await discordGuild.LeaveAsync();
            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Server \"{discordGuild.Name}\" is leaved".ToInlineEmbed(Color.Red), ephemeral: silent);
        }


        [SlashCommand("unblock-server", "[OWNER BOT] Desbloquea un servidor.")]
        public async Task AdminUnblockGuild(string serverId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var blockedGuild = await db.BlockedGuilds.FindAsync(ulong.Parse(serverId.Trim()));

            if (blockedGuild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server not found".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            db.BlockedGuilds.Remove(blockedGuild);
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("block-user-global", "[OWNER BOT] Bloquea a un usuario.")]
        public async Task AdminBlockUser(string userId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            bool ok = ulong.TryParse(userId, out var uUserId);

            if (!ok)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await using var db = new StorageContext();
            if ((await db.BlockedUsers.FindAsync(uUserId)) is not null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already blocked".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await db.BlockedUsers.AddAsync(new() { Id = uUserId, From = DateTime.UtcNow, Hours = 0 });
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("unblock-user-global", "[OWNER BOT] Desbloquea a un usuario.")]
        public async Task AdminUnblockUser(string userId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            bool ok = ulong.TryParse(userId, out var uUserId);

            if (!ok)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await using var db = new StorageContext();
            var blockedUser = await db.BlockedUsers.FindAsync(uUserId);
            if (blockedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User not found".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            db.BlockedUsers.Remove(blockedUser);
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("broadcast", "[OWNER BOT] Envía un mensaje en cada canal en el que se haya llamado alguna vez al bot.")]
        public async Task AdminShoutOut(string? title, string? desc = null, string? imageUrl = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var embedB = new EmbedBuilder().WithColor(Color.Orange);
            if (title is not null) embedB.WithTitle(title);
            if (desc is not null) embedB.WithDescription(desc);
            if (imageUrl is not null) embedB.WithImageUrl(imageUrl);
            var embed = embedB.Build();

            await using var db = new StorageContext();
            var channelIds = db.Channels.Select(c => c.Id).ToList();
            var channels = new List<IMessageChannel>();

            await Parallel.ForEachAsync(channelIds, async (channelId, ct) =>
            {
                IMessageChannel? mc;
                try { mc = (await _client.GetChannelAsync(channelId)) as IMessageChannel; }
                catch { return; }
                if (mc is not null) channels.Add(mc);
            });

            int count = 0;
            await Parallel.ForEachAsync(channels, async (channel, ct) =>
            {
                try {
                    await channel.SendMessageAsync(embed: embed);
                    count++;
                }
                catch { return; }
            });
                
            await FollowupAsync(embed: SuccessEmbed($"Message was sent in {count} channels"), ephemeral: silent);
        }


        [SlashCommand("server-stats", "[OWNER BOT] Ve las estadisticas de un servidor.")]
        public async Task AdminGuildStats(string? guildId = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            ulong uGuildId;
            if (guildId is null)
            {
                uGuildId = Context.Guild.Id;
            }
            else
            {
                if (!ulong.TryParse(guildId, out uGuildId))
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong ID".ToInlineEmbed(Color.Red), ephemeral: silent);
                    return;
                }
            }
            
            var guild = _client.GetGuild(uGuildId);
            if (guild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Guild not found".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await using var db = new StorageContext();
            var dbGuild = await FindOrStartTrackingGuildAsync(guild.Id, db);
            var allCharacters = await db.CharacterWebhooks.Where(cw => cw.Channel.GuildId == guild.Id).ToListAsync();

            if (allCharacters.Count == 0)
            {
                await FollowupAsync(embed: $"No records ({dbGuild.MessagesSent})".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            int charactersCount = allCharacters.Count;
            var lastUsed = allCharacters.OrderByDescending(c => c.LastCallTime).First().LastCallTime;
            string callDate = $"{lastUsed.Day}/{lastUsed.Month}/{lastUsed.Year}";
            
            string desc = $"**Owner:** `{guild.Owner?.Username}`\n" +
                          $"**Personajes:** `{charactersCount}`\n" +
                          $"**Ultimo personaje llamado:** `{callDate}`\n" +
                          $"**Mensajes enviados:** `{dbGuild.MessagesSent}`";

            var embed = new EmbedBuilder().WithTitle(guild.Name)
                                          .WithColor(Color.Magenta)
                                          .WithDescription(desc);
            try
            {
                string iconUrl = guild.IconUrl;
                if (!string.IsNullOrWhiteSpace(iconUrl))
                    embed.WithImageUrl(iconUrl);
            }
            finally
            {
                await FollowupAsync(embed: embed.Build(), ephemeral: silent);
            }
        }


        [SlashCommand("shutdown", "[OWNER BOT] Apaga el bot.")]
        public async Task AdminShutdownAsync(bool silent = true)
        {
            await RespondAsync(embed: $"{WARN_SIGN_DISCORD} Shutting down...".ToInlineEmbed(Color.Orange), ephemeral: silent);

            integrations.CaiClient?.Dispose();
            Environment.Exit(0);
        }


        [SlashCommand("set-game", "[OWNER BOT] Dale un status (de juego) al bot.")]
        public async Task AdminUpdateGame(string? activity = null, string? streamUrl = null, ActivityType type = ActivityType.Playing, bool silent = true)
        {
            await _client.SetGameAsync(activity, streamUrl, type);
            await RespondAsync(embed: SuccessEmbed(), ephemeral: silent);

            string gamePath = $"{EXE_DIR}{SC}storage{SC}lastgame.txt";
            File.WriteAllText(gamePath, activity ?? "");
        }


        [SlashCommand("set-status", "[OWNER BOT] Dale un status al bot.")]
        public async Task AdminUpdateStatus(UserStatus status, bool silent = true)
        {
            await _client.SetStatusAsync(status);
            await RespondAsync(embed: SuccessEmbed(), ephemeral: silent);
        }
    }
}
