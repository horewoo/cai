﻿using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Services;

namespace CharacterEngineDiscord.Handlers
{
    public class SlashCommandsHandler(IServiceProvider services, IDiscordClient client)
    {
        public Task HandleCommand(SocketSlashCommand command)
        {
            Task.Run(async () =>
            {
                try { await HandleCommandAsync(command); }
                catch (Exception e) { HandleSlashCommandException(command, e); }
            });

            return Task.CompletedTask;
        }


        private async Task HandleCommandAsync(SocketInteraction command)
        {
            if (await UserIsBannedCheckOnly(command.User.Id)) return;

            var context = new InteractionContext(client, command, command.Channel);
            await services.GetRequiredService<InteractionService>().ExecuteCommandAsync(context, services);
        }


        private void HandleSlashCommandException(SocketSlashCommand command, Exception e)
        {
            LogException(new[] { e });
            var channel = command.Channel as SocketGuildChannel;
            var guild = channel?.Guild;

            List<string> commandParams = new();
            foreach (var option in command.Data.Options)
            {
                var val = option?.Value?.ToString() ?? "";
                int l = Math.Min(val.Length, 20);
                commandParams.Add($"{option?.Name}:{val[..l] + (val.Length > 20 ? "..." : "")}");
            }
            
            TryToReportInLogsChannel(client, title: "Excepción de Slash Command",
                                              desc: $"En servidor `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Propietario: `{guild?.Owner.GetBestName()} ({guild?.Owner.Username})`\n" +
                                                    $"Canal: `{channel?.Name} ({channel?.Id})`\n" +
                                                    $"Usuario: `{command.User?.Username}`\n" +
                                                    $"Slash command: `/{command.CommandName}` `[{string.Join(" | ", commandParams)}]`",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }
    }
}
