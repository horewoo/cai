﻿using CharacterEngineDiscord.Models.Common;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Database;
using Discord.Webhook;
using Newtonsoft.Json.Linq;
using CharacterEngineDiscord.Interfaces;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp.Helpers;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    public class ManagerCommands(IIntegrationsService integrations, IDiscordClient client) : InteractionModuleBase<InteractionContext>
    {
        private readonly DiscordSocketClient _client = (DiscordSocketClient)client;


        [SlashCommand("delete-character", "Quitar personaje-webhook del canal")]
        public async Task DeleteCharacter(string webhookIdOrPrefix, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            if (Context.Channel is not ITextChannel textChannel)
            {
                await FollowupAsync(embed: $"Ups... Algo ha pasado...".ToInlineEmbed(0xededed), ephemeral: silent);
                return;
            }

            try
            {
                var discordWebhook = await textChannel.GetWebhookAsync(characterWebhook.Id);
                if (discordWebhook is not null)
                    await discordWebhook.DeleteAsync();
            }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"Falló al eliminar: `{e.Message}`".ToInlineEmbed(Color.Red), ephemeral: silent);
            }

            db.CharacterWebhooks.Remove(characterWebhook);
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }

        
        public enum ClearCharactersChoise { [ChoiceDisplay("sólo en el canal actual")]channel, [ChoiceDisplay("en todo el servidor")]server }
        [SlashCommand("clear-characters", "Eliminar todos los personajes/webhooks de este canal/servidor")]
        public async Task ClearCharacters(ClearCharactersChoise scope, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            IReadOnlyCollection<IWebhook> discordWebhooks;
            List<CharacterWebhook> trackedWebhooks;

            await using var db = new StorageContext();
            bool all = scope is ClearCharactersChoise.server;
            if (all)
            {
                discordWebhooks = await Context.Guild.GetWebhooksAsync();
                trackedWebhooks = (from guild in (from guilds in db.Guilds where guilds.Id == Context.Guild.Id select guilds)
                                   join channel in db.Channels on guild.Id equals channel.Guild.Id
                                   join webhook in db.CharacterWebhooks on channel.Id equals webhook.Channel.Id
                                   select webhook).ToList();
            }
            else
            {
                discordWebhooks = await ((SocketTextChannel)Context.Channel).GetWebhooksAsync();
                trackedWebhooks = (from channel in (from channels in db.Channels where channels.Id == Context.Channel.Id select channels)
                                   join webhook in db.CharacterWebhooks on channel.Id equals webhook.Channel.Id
                                   select webhook).ToList();
            }

            await Parallel.ForEachAsync(trackedWebhooks, async (tw, _) =>
            {
                var discordWebhook = discordWebhooks.FirstOrDefault(dw => dw.Id == tw.Id);
                
                try {
                    if (discordWebhook is not null)
                        await discordWebhook.DeleteAsync();
                } finally {
                    db.CharacterWebhooks.Remove(tw);
                }
            });

            await TryToSaveDbChangesAsync(db);
            await FollowupAsync(embed: SuccessEmbed($"Todos los personajes {(all ? "en este servidor" : "en este canal")} han sido removidos."), ephemeral: silent);
        }


        [SlashCommand("copy-character-from-channel", "-")]
        public async Task CopyCharacter(IChannel channel, string webhookIdOrPrefix, bool silent = false)
            => await CopyCharacterAsync(channel, webhookIdOrPrefix, silent);
        

        [SlashCommand("set-channel-random-reply-chance", "Establecer la posibilidad de respuestas aleatorias del personaje para este canal")]
        public async Task SetChannelRandomReplyChance(float chance, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);
            
            await using var db = new StorageContext();
            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, db);
            string before = channel.RandomReplyChance.ToString();
            channel.RandomReplyChance = chance;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"La probabilidad de respuesta aleatoria para este canal ha cambiado de {before}% a {chance}%"), ephemeral: silent);
        }

        
        [SlashCommand("hunt-user", "Hacer que el personaje responda a los mensajes de un determinado usuario (o bot)")]
        public async Task HuntUser(string webhookIdOrPrefix, IUser? user = null, string? userIdOrCharacterPrefix = null, float chanceOfResponse = 100, bool silent = false)
            => await HuntUserAsync(webhookIdOrPrefix, user, userIdOrCharacterPrefix, chanceOfResponse, silent);


        [SlashCommand("stop-hunt-user", "Hacer que el personaje deje de responder a los mensajes de un determinado usuario (o bot)")]
        public async Task UnhuntUser(string webhookIdOrPrefix, IUser? user = null, string? userIdOrCharacterPrefix = null, bool silent = false)
            => await UnhuntUserAsync(webhookIdOrPrefix, user, userIdOrCharacterPrefix, silent);
        
        
        [SlashCommand("reset-character", "Olvida toda la historia y empieza a chatear desde el principio.")]
        public async Task ResetCharacter(string webhookIdOrPrefix, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            bool result = false;
            var type = characterWebhook.IntegrationType;

            if (type is IntegrationType.CharacterAI)
                result = await ResetCaiCharacterAsync(characterWebhook, silent, db);
            else if (type is IntegrationType.Aisekai)
                result = await ResetAisekaiCharacterAsync(characterWebhook, characterWebhook.Channel.Guild.GuildAisekaiAuthToken, silent);
            else if (type is IntegrationType.OpenAI or IntegrationType.KoboldAI or IntegrationType.HordeKoboldAI)
            {
                characterWebhook.StoredHistoryMessages.Clear();
                var firstGreetingMessage = new StoredHistoryMessage { CharacterWebhookId = characterWebhook.Id, Content = characterWebhook.Character.Greeting, Role = "assistant" };
                await db.StoredHistoryMessages.AddAsync(firstGreetingMessage);
                await TryToSaveDbChangesAsync(db);
                result = true;
            }
            else
            {
                await FollowupAsync(embed: $"<a:fr_error:1182414048793481236> No se ha establecido ningún backend de API para este personaje".ToInlineEmbed(Color.Orange), ephemeral: silent);
            }

            if (result is false) return;

            var webhookClient = integrations.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await FollowupAsync(embed: $"<a:fr_error:1182414048793481236> Webhook del canal no encontrado".ToInlineEmbed(0xededed), ephemeral: silent);
                return;
            }

            string characterMessage = $"{Context.User.Mention} {characterWebhook.Character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(Context.User as SocketGuildUser)?.GetBestName()}**")}";
            if (characterMessage.Length > 2000) characterMessage = characterMessage[..1994] + "[...]";

            try
            {
                await webhookClient.SendMessageAsync(characterMessage);
            }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"<a:fr_error:1182414048793481236> Error al enviar el mensaje de saludo del personaje: `{e.Message}`".ToInlineEmbed(0xededed), ephemeral: silent);
            }
        }

        
        [SlashCommand("set-server-messages-format", "Cambiar el formato de los mensajes utilizados para todos los nuevos personajes en este servidor por defecto")]
        public async Task SetDefaultMessagesFormat(string newFormat, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: $"<a:fr_error:1182414048793481236> No se puede establecer el formato sin un **`{{{{msg}}}}`** marcador!".ToInlineEmbed(0xededed), ephemeral: silent);
                return;
            }

            int refCount = 0;
            if (newFormat.Contains("{{ref_msg_begin}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_text}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_end}}")) refCount++;

            if (refCount != 0 && refCount != 3)
            {
                await FollowupAsync(embed: $"<a:fr_error:1182414048793481236> ¡Formato incorrecto del marcador de posición `ref_msg`!".ToInlineEmbed(0xededed), ephemeral: silent);
                return;
            }

            guild.GuildMessagesFormat = newFormat;
            await TryToSaveDbChangesAsync(db);

            string text = newFormat.Replace("{{msg}}", "¡Hola!").Replace("{{user}}", "Average AI Enjoyer");

            if (refCount == 3)
            {
                text = text.Replace("{{ref_msg_text}}", "Hola")
                           .Replace("{{ref_msg_begin}}", "")
                           .Replace("{{ref_msg_end}}", "")
                           .Replace("{{ref_msg_user}}", "Dude")
                           .Replace("\\n", "\n");
            }

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **Hecho**").WithColor(Color.Green)
                                          .AddField("Nuevo formato:", $"`{newFormat}`")
                                          .AddField("Ejemplo", $"Mensaje del usuario: *`¡Hola!`*\n" +
                                                               $"Nombre del usuario: `Average AI Enjoyer`\n" +
                                                               $"Mensaje de referencia: *`Hola`* del usuario *`Dude`*\n" +
                                                               $"Resultado (qué verá el personaje): *`{text}`*");

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }

        
        [SlashCommand("drop-server-messages-format", "Elimina el formato de mensajes por defecto para este servidor")]
        public async Task DropGuildMessagesFormat(bool silent = false)
        {
            await DeferAsync(ephemeral:silent);

            await using var db = new StorageContext();
            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

            guild.GuildMessagesFormat = null;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }

        
        [SlashCommand("set-server-jailbreak-prompt", "Cambiar el formato de los mensajes utilizados para todos los nuevos personajes en este servidor por defecto")]
        public async Task SetServerDefaultPrompt()
        {
            var modal = new ModalBuilder().WithTitle($"Actualizar jailbreak prompt para este servidor")
                                          .WithCustomId($"guild~{Context.Guild.Id}")
                                          .AddTextInput("Nuevo jailbreak prompt", "new-prompt", TextInputStyle.Paragraph)
                                          .Build();
            await RespondWithModalAsync(modal);
        }

        
        [SlashCommand("drop-server-jailbreak-prompt", "Elimina el prompt de jailbreak por defecto para este servidor")]
        public async Task DropGuildPrompt(bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

            guild.GuildJailbreakPrompt = null;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("set-server-cai-token", "Establecer el token predeterminado de Character.ai para este servidor.")]
        public async Task SetGuildCaiToken(string token, bool hasCaiPlusSubscription, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

            guild.GuildCaiUserToken = token;
            guild.GuildCaiPlusMode = hasCaiPlusSubscription;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("set-server-aisekai-auth", "Establecer cuenta Aisekai por defecto para este servidor")]
        public async Task SetGuildAisekaiAuth(string email, string password, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var response = await integrations.AisekaiClient.AuthorizeUserAsync(email, password);

            if (response.IsSuccessful)
            {
            await using var db = new StorageContext();
                var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

                guild.GuildAisekaiAuthToken = response.ExpToken;
                guild.GuildAisekaiRefreshToken = response.RefreshToken;
                await TryToSaveDbChangesAsync(db);

                await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
            }
            else
            {
                await FollowupAsync(embed: $"No se ha podido iniciar sesión en la cuenta de Aisekai: `{response.Message}`".ToInlineEmbed(Color.Red), ephemeral: silent);
            }
        }


        [SlashCommand("set-server-openai-api", "Establecer la API OpenAI por defecto para este servidor")]
        public async Task SetGuildOpenAiToken(string token, OpenAiModel gptModel, string? reverseProxyEndpoint = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

            if (reverseProxyEndpoint is not null)
                guild.GuildOpenAiApiEndpoint = reverseProxyEndpoint;

            guild.GuildOpenAiApiToken = token;
            guild.GuildOpenAiModel = gptModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : gptModel is OpenAiModel.GPT_4 ? "gpt-4" : null;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("set-server-koboldai-api", "Establecer API KoboldAI por defecto para este servidor")]
        public async Task SetGuildKoboldAiApi(string apiEndpoint, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

            guild.GuildKoboldAiApiEndpoint = apiEndpoint;

            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("set-server-horde-koboldai-api", "Establecer API de Horde KoboldAI por defecto para este servidor")]
        public async Task SetGuildHordeKoboldAiApi(string token, string model, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

            guild.GuildHordeApiToken = token;
            guild.GuildHordeModel = model;

            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("get-horde-koboldai-workers", "Obtener la lista de trabajadores disponibles de Horde KoboldAI")]
        public async Task GetHordeWorkers(bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            string url = "https://horde.koboldai.net/api/v2/workers?type=text";
            using var response = await integrations.CommonHttpClient.GetAsync(url);

            Embed embed;
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsJsonAsync();
                int count = 1;
                if (content is not null)
                {
                    var eb = new EmbedBuilder().WithDescription("");
                    foreach (dynamic worker in (JArray)content)
                    {
                        string line = $"**{count++}. `{((JArray)worker.models).FirstOrDefault()}` by {worker.name}**" +
                                      $"Capabilities: {worker.max_length}/{worker.max_context_length} ({((string)worker.performance).Replace("tokens per second", "T/s")})\n" +
                                      $"Uptime: {worker.uptime}\n";
                        if (eb.Description.Length + line.Length > 4096) break;
                        eb.Description += line;
                    }
                    embed = eb.WithColor(Color.Green).WithTitle("Found workers:").Build();
                }
                else
                {
                    embed = new EmbedBuilder().WithColor(Color.Red).WithDescription("Algo ha pasado...").Build();
                }
            }
            else
            {
                embed = new EmbedBuilder().WithColor(Color.Red).WithDescription("Algo ha pasado...").Build();
            }

            await FollowupAsync(embed: embed, ephemeral: silent);
        }


        [SlashCommand("say", "Haz que un personaje diga algo")]
        public async Task SayAsync(string webhookIdOrPrefix, string text, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            var webhookClient = integrations.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Algo ha sucedido...".ToInlineEmbed(0xededed), ephemeral: silent);
                return;
            }

            await webhookClient.SendMessageAsync(text);
            await ModifyOriginalResponseAsync(r => r.Embed = SuccessEmbed());
        }


        [SlashCommand("block-user", "Hacer que los personajes ignoren a cierto usuario en este servidor.")]
        public async Task ServerBlockUser(IUser? user = null, string? userId = null, [Summary(description: "No se ha especificado un tiempo.")]int hours = 0, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            if (user is null && userId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Usuario o ID incorrecta.".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await using var db = new StorageContext();
            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, db);

            ulong uUserId;
            if (user is null)
            {
                bool ok = ulong.TryParse(userId, out uUserId);

                if (!ok)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} ID de usuario incorrecta".ToInlineEmbed(Color.Red), ephemeral: silent);
                    return;
                }
            }
            else
            {
                uUserId = user.Id;
            }

            if (guild.BlockedUsers.Any(bu => bu.Id == uUserId))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} El usuario ya estaba bloqueado.".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await db.BlockedUsers.AddAsync(new() { Id = uUserId, From = DateTime.UtcNow, Hours = hours, GuildId = guild.Id });
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("unblock-user", "Desbloquea un usuario de este usuario.")]
        public async Task ServerUnblockUser(IUser? user = null, string? userId = null, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            if (user is null && userId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Usuario o ID incorrecto.".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            ulong uUserId;
            if (user is null)
            {
                bool ok = ulong.TryParse(userId, out uUserId);

                if (!ok)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} ID de usuario incorrecto.".ToInlineEmbed(Color.Red), ephemeral: silent);
                    return;
                }
            }
            else
            {
                uUserId = user.Id;
            }

            await using var db = new StorageContext();
            var blockedUsers = await db.BlockedUsers.Where(bu => bu.GuildId == Context.Guild.Id).ToListAsync();

            var blockedUser = blockedUsers.FirstOrDefault(bu => bu.Id == uUserId && bu.GuildId == Context.Guild.Id);
            if (blockedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Usuario no encontrado.".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            db.BlockedUsers.Remove(blockedUser);
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task<bool> ResetCaiCharacterAsync(CharacterWebhook cw, bool silent, StorageContext db)
        {
            if (integrations.CaiClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} La integración de CharacterAI (c.ai) no está disponible en estos momentos.".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return false;
            }

            string charId = cw.CharacterId ?? string.Empty; 
            string caiToken = cw.PersonalApiToken ?? cw.Channel.Guild.GuildCaiUserToken ?? string.Empty;
            bool plusMode = cw.Channel.Guild.GuildCaiPlusMode ?? false;

            while (integrations.CaiReloading)
                await Task.Delay(5000);

            var id = Guid.NewGuid();
            integrations.RunningCaiTasks.Add(id);
            try
            {
                var newHisoryId = await integrations.CaiClient.CreateNewChatAsync(charId, caiToken, plusMode).WithTimeout(60000);
                if (newHisoryId is null)
                {
                    await FollowupAsync(
                        embed: $"{WARN_SIGN_DISCORD} Error al crear un chat con este personaje.".ToInlineEmbed(
                            Color.Red), ephemeral: silent);
                    return false;
                }

                cw.ActiveHistoryID = newHisoryId;
                await TryToSaveDbChangesAsync(db);
                await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);

                return true;
            }
            finally { integrations.RunningCaiTasks.Remove(id); }
        }


        private async Task<bool> ResetAisekaiCharacterAsync(CharacterWebhook characterWebhook, string? authToken, bool silent)
        {
            var guild = characterWebhook.Channel.Guild;
            
            var response = await integrations.AisekaiClient.ResetChatHistoryAsync(authToken ?? "", characterWebhook.ActiveHistoryID ?? "");
            if (response.IsSuccessful)
            {
                await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
                characterWebhook.Character.Greeting = response.Greeting!;

                return true;
            }

            if (response.Code == 401)
            {
                string? newAuthToken = await integrations.UpdateGuildAisekaiAuthTokenAsync(guild.Id, guild.GuildAisekaiRefreshToken ?? "");
                if (newAuthToken is null)
                {
                    await FollowupAsync(embed: ($"{WARN_SIGN_DISCORD} Failed to authorize Aisekai account`").ToInlineEmbed(Color.Red), ephemeral: silent);
                    return false;
                }

                return await ResetAisekaiCharacterAsync(characterWebhook, newAuthToken, silent);
            }
            
            await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to create new chat with a character: `{response.ErrorReason}`".ToInlineEmbed(Color.Red), ephemeral: silent);
            return false;
        }


        private async Task CopyCharacterAsync(IChannel channel, string webhookIdOrPrefix, bool silent)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, channel.Id, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            if (Context.Channel is not IIntegrationChannel discordChannel)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to copy the character".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            string name = characterWebhook.Character.Name.ToLower().Contains("discord") ? characterWebhook.Character.Name.Replace('o', 'о').Replace('c', 'с') : characterWebhook.Character.Name;

            IWebhook channelWebhook;
            await using (Stream? image = await TryToDownloadImageAsync(characterWebhook.Character.AvatarUrl, integrations.ImagesHttpClient))
            {                
                try { channelWebhook = await discordChannel.CreateWebhookAsync(name, image); }
                catch (Exception e)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Falló al crear el webhook: {e.Message}".ToInlineEmbed(Color.Red), ephemeral: silent);
                    return;
                }
            }

            try
            {
                db.CharacterWebhooks.Add(new()
                {
                    Id = channelWebhook.Id,
                    WebhookToken = channelWebhook.Token,
                    ChannelId = Context.Channel.Id,
                    LastCallTime = DateTime.Now,
                    MessagesSent = 1,
                    ReplyChance = 0,
                    ResponseDelay = 1,
                    FromChub = characterWebhook.FromChub,
                    CallPrefix = characterWebhook.CallPrefix,
                    CharacterId = characterWebhook.CharacterId,
                    CrutchEnabled = characterWebhook.CrutchEnabled,
                    StopBtnEnabled = characterWebhook.StopBtnEnabled,
                    IntegrationType = characterWebhook.IntegrationType,
                    PersonalMessagesFormat = characterWebhook.PersonalMessagesFormat,
                    GenerationFreqPenaltyOrRepetitionSlope = characterWebhook.GenerationFreqPenaltyOrRepetitionSlope,
                    GenerationMaxTokens = characterWebhook.GenerationMaxTokens,
                    PersonalApiModel = characterWebhook.PersonalApiModel,
                    PersonalApiToken = characterWebhook.PersonalApiToken,
                    GenerationPresenceOrRepetitionPenalty = characterWebhook.GenerationPresenceOrRepetitionPenalty,
                    GenerationTemperature = characterWebhook.GenerationTemperature,
                    ReferencesEnabled = characterWebhook.ReferencesEnabled,
                    SwipesEnabled = characterWebhook.SwipesEnabled,
                    PersonalJailbreakPrompt = characterWebhook.PersonalJailbreakPrompt,
                    PersonalApiEndpoint = characterWebhook.PersonalApiEndpoint,
                    ActiveHistoryID = null
                });

                var type = characterWebhook.IntegrationType;
                if (type is not IntegrationType.CharacterAI && type is not IntegrationType.Aisekai)
                    db.StoredHistoryMessages.Add(new() { CharacterWebhookId = channelWebhook.Id, Content = characterWebhook.Character.Greeting, Role = "assistant" });

                await TryToSaveDbChangesAsync(db);

                var webhookClient = new DiscordWebhookClient(channelWebhook.Id, channelWebhook.Token);
                integrations.WebhookClients.TryAdd(channelWebhook.Id, webhookClient);

                string characterMessage = $"{Context.User.Mention} {characterWebhook.Character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(Context.User as SocketGuildUser)?.GetBestName()}**")}";
                if (characterMessage.Length > 2000) characterMessage = characterMessage[..1994] + "[...]";

                await FollowupAsync(embed: SuccessEmbed($"{characterWebhook.Character.Name} se ha copiado correctamente de {channel.Name}"), ephemeral: silent);
                await webhookClient.SendMessageAsync(characterMessage);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                TryToReportInLogsChannel(_client, "Exception", "Falló al inciar el personaje", e.ToString(), Color.Red, true);

                await channelWebhook.DeleteAsync();
            }
        }


        private async Task HuntUserAsync(string webhookIdOrPrefix, IUser? user, string? userIdOrCharacterPrefix, float chanceOfResponse, bool silent)
        {
            await DeferAsync(ephemeral: silent);

            if (user is null && string.IsNullOrWhiteSpace(userIdOrCharacterPrefix))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Especifica el usuario o la ID".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            string? username = user?.Mention;
            ulong? userToHuntId = user?.Id;

            if (userToHuntId is null)
            {
                bool isId = ulong.TryParse(userIdOrCharacterPrefix!.Trim(), out ulong userId);
                if (isId)
                {
                    userToHuntId = userId;
                    username = userId.ToString();
                }
                else
                {
                    var characterToHunt = await TryToFindCharacterWebhookInChannelAsync(userIdOrCharacterPrefix, Context, db);
                    userToHuntId = characterToHunt?.Id;
                    username = characterToHunt?.Character.Name;
                }
            }

            if (userToHuntId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Usuario o personaje/webhook no encontrado.".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            if (characterWebhook.HuntedUsers.Any(h => h.UserId == userToHuntId))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} El usuario está siendo escuchado.".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            await db.HuntedUsers.AddAsync(new() { UserId = (ulong)userToHuntId, Chance = chanceOfResponse, CharacterWebhookId = characterWebhook.Id });
            await TryToSaveDbChangesAsync(db);

            username ??= user?.Mention;
            await FollowupAsync(embed: $":ghost: **{characterWebhook.Character.Name}** escuchando a **{username}**".ToInlineEmbed(Color.LighterGrey, false), ephemeral: silent);
        }


        private async Task UnhuntUserAsync(string webhookIdOrPrefix, IUser? user, string? userIdOrCharacterPrefix, bool silent)
        {
            await DeferAsync(ephemeral: silent);

            if (user is null && string.IsNullOrWhiteSpace(userIdOrCharacterPrefix))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Especifica al usuario o su ID".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            string? username = user?.Mention;
            ulong? huntedUserId = user?.Id;

            if (huntedUserId is null)
            {
                bool isId = ulong.TryParse(userIdOrCharacterPrefix!.Trim(), out ulong userId);
                if (isId)
                {
                    huntedUserId = userId;
                    username = userId.ToString();
                }
                else
                {
                    var characterToUnhunt = await TryToFindCharacterWebhookInChannelAsync(userIdOrCharacterPrefix, Context, db);
                    huntedUserId = characterToUnhunt?.Id;
                    username = characterToUnhunt?.Character.Name;
                }
            }

            if (huntedUserId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Usuario o personaje no encontrado.".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            var huntedUser = characterWebhook.HuntedUsers.FirstOrDefault(h => h.UserId == huntedUserId);

            if (huntedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} El usuario no está siendo escuchado.".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            characterWebhook.HuntedUsers.Remove(huntedUser);
            await TryToSaveDbChangesAsync(db);

            username ??= user?.Mention;
            await FollowupAsync(embed: $":ghost: **{characterWebhook.Character.Name}** no está escuchando a **{username}** ahora".ToInlineEmbed(Color.LighterGrey, false), ephemeral: silent);
        }
    }
}
