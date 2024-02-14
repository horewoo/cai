﻿using System.Text.RegularExpressions;
using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Discord.WebSocket;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Interfaces;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    [Group("update", "Cambia los ajustes del personaje")]
    public class UpdateCharacterCommands(IIntegrationsService integrations) : InteractionModuleBase<InteractionContext>
    {
        //private readonly DiscordSocketClient _client = (DiscordSocketClient)client;



        [SlashCommand("call-prefix", "Cambia el prefix del personaje")]
        public async Task SetCallPrefix(string webhookIdOrPrefix, string newCallPrefix, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("messages-format", "Cambia el formato de los mensajes del personaje")]
        public async Task SetMessagesFormat(string webhookIdOrPrefix, string newFormat, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't set format without a **`{{{{msg}}}}`** placeholder!".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            int refCount = 0;
            if (newFormat.Contains("{{ref_msg_begin}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_text}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_end}}")) refCount++;

            if (refCount != 0 && refCount != 3)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong `ref_msg` placeholder format!".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            characterWebhook.PersonalMessagesFormat = newFormat;
            await TryToSaveDbChangesAsync(db);

            string text = newFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer");

            if (refCount == 3)
            {
                text = text.Replace("{{ref_msg_text}}", "Hola")
                           .Replace("{{ref_msg_begin}}", "")
                           .Replace("{{ref_msg_end}}", "")
                           .Replace("{{ref_msg_user}}", "Dude")
                           .Replace("\\n", "\n");
            }

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **{characterWebhook.Character.Name}**")
                                          .AddField("Formato:", $"`{newFormat}`")
                                          .AddField("[Example]", $"Mensaje del usuario: *`Hello!`*\n" +
                                                                 $"Nombre del personaje: `Average AI Enjoyer`\n" +
                                                                 $"Mensaje de Referencia: *`Hola`* from user *`Dude`*\n" +
                                                                 $"Resultado (lo que el personaje ve): *`{text}`*")
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed, ephemeral: silent);
        }


        [SlashCommand("jailbreak-prompt", "Cambiar el jailbreak prompt del personaje")]
        public async Task SetJailbreakPrompt(string webhookIdOrPrefix)
        {
            var modal = new ModalBuilder().WithTitle($"Update jailbreak prompt for the character")
                                          .WithCustomId($"upd~{webhookIdOrPrefix}")
                                          .AddTextInput("New jailbreak prompt", "new-prompt", TextInputStyle.Paragraph, "(Will not work for CharacterAI)")
                                          .Build();
            await RespondWithModalAsync(modal);
        }


        [SlashCommand("avatar", "Cambia el avatar del personaje")]
        public async Task SetAvatar(string webhookIdOrPrefix, string avatarUrl, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            var channel = (SocketTextChannel)Context.Channel;
            var channelWebhook = await channel.GetWebhookAsync(characterWebhook.Id);

            await using (Stream? image = await TryToDownloadImageAsync(avatarUrl, integrations.ImagesHttpClient))
            {
                await channelWebhook.ModifyAsync(cw
                    => cw.Image = new Image(image ?? new MemoryStream(File.ReadAllBytes($"{EXE_DIR}{SC}storage{SC}default_avatar.png"))));
            };

            characterWebhook.Character.AvatarUrl = avatarUrl;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"El avatar de {characterWebhook.Character.Name} se ha cambiado :)", imageUrl: avatarUrl), ephemeral: silent);
        }

        
        [SlashCommand("name", "Cambia el nombre del personaje")]
        public async Task SetCharacterName(string webhookIdOrPrefix, string name, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            name = name.ToLower().Contains("discord") ? name.Replace('o', 'о').Replace('c', 'с') : name;

            var channel = (SocketTextChannel)Context.Channel;
            var channelWebhook = await channel.GetWebhookAsync(characterWebhook.Id);

            await channelWebhook.ModifyAsync(cw => cw.Name = name);

            string before = characterWebhook.Character.Name;
            characterWebhook.Character.Name = name;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"El nombre se ha cambiado de {before} a {name}"), ephemeral: silent);
        }

        
        [SlashCommand("set-delay", "Cambia el retardo de respuesta")]
        public async Task SetDelay(string webhookIdOrPrefix, int seconds, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            string before = characterWebhook.ResponseDelay.ToString();
            characterWebhook.ResponseDelay = seconds;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"Se ha cambiado el tiempo de respuesta de {before}s a {seconds}s"), ephemeral: silent);
        }


        [SlashCommand("toggle-quotes", "Activar/desactivar las comillas")]
        public async Task ToggleQuotes(string webhookIdOrPrefix, bool enable, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            characterWebhook.ReferencesEnabled = enable;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"Comillas {(enable ? "habilitadas" : "deshabilitadas")}"), ephemeral: silent);
        }


        [SlashCommand("toggle-swipes", "Activar/desactivar botones deslizantes")]
        public async Task ToggleSwipes(string webhookIdOrPrefix, bool enable, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            characterWebhook.SwipesEnabled = enable;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"Botones {(enable ? "habilitados" : "deshabilitados")}"), ephemeral: silent);
        }


        [SlashCommand("toggle-crutch", "Activar/desactivar el botón de proceder a la genetación")]
        public async Task ToggleCrutch(string webhookIdOrPrefix, bool enable, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            var type = characterWebhook.IntegrationType;
            if (type is IntegrationType.CharacterAI || type is IntegrationType.Aisekai)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Not available for {type} integrations".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            characterWebhook.CrutchEnabled = enable;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"Crutch {(enable ? "enabled" : "disabled")}"), ephemeral: silent);
        }


        [SlashCommand("toggle-stop-btn", "Habilitar o deshabilitar el botón de Detener (STOP)")]
        public async Task ToggleStop(string webhookIdOrPrefix, bool enable, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            characterWebhook.StopBtnEnabled = enable;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"Botón de detener (STOP) {(enable ? "habilitado" : "deshabilitado")}"), ephemeral: silent);
        }


        [SlashCommand("set-random-reply-chance", "Cambiar el ajuste random de respuestas")]
        public async Task SetRandomReplyChance(string webhookIdOrPrefix, float chance, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            string before = characterWebhook.ReplyChance.ToString();
            characterWebhook.ReplyChance = chance;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed($"Ajuste random de respuestas de {characterWebhook.Character.Name} se ha cambiado de {before} a {chance}"), ephemeral: silent);
        }


        [SlashCommand("set-cai-history-id", "Cambiar historial de c.ai")]
        public async Task SetCaiHistory(string webhookIdOrPrefix, string newHistoryId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't set history ID for non-CharacterAI integration".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            string message = $"{OK_SIGN_DISCORD} **History ID** for this channel was changed from `{characterWebhook.ActiveHistoryID}` to `{newHistoryId}`";

            if (Regex.IsMatch(newHistoryId, @"[\da-z]{4,12}-[\da-z]{4,12}-[\da-z]{4,12}-[\da-z]{4,12}-[\da-z]{4,12}"))
                message += $"\n{WARN_SIGN_DISCORD} Entered ID belongs to \"chat2\" history and is not compatible with the bot in current moment.";
            else if (newHistoryId.Length != 43)
                message += $".\nEntered history ID has length that is different from expected ({newHistoryId.Length}/43). Make sure it's correct.";

            characterWebhook.ActiveHistoryID = newHistoryId;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green), ephemeral: silent);
        }

        [SlashCommand("set-api-type", "Cambiar la API backend para los personajes")]
        public async Task SetApi(string webhookIdOrPrefix, ApiTypeForChub api, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            var type = characterWebhook.IntegrationType;
            if (type is IntegrationType.CharacterAI or IntegrationType.Aisekai)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't change API type for {type} character".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            var integrationType = api is ApiTypeForChub.OpenAI ? IntegrationType.OpenAI
                                : api is ApiTypeForChub.KoboldAI ? IntegrationType.KoboldAI
                                : api is ApiTypeForChub.HordeKoboldAI ? IntegrationType.HordeKoboldAI
                                : IntegrationType.Empty;

            characterWebhook.IntegrationType = integrationType;
            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }

        [SlashCommand("open-ai-settings", "Change OpenAI integration settings")]
        public async Task SetOpenAiSettings(string webhookIdOrPrefix, int? maxTokens = null, float? temperature = null, float? frequencyPenalty = null, float? presencePenalty = null, OpenAiModel? openAiModel = null, string? personalApiToken = null, string? personalApiEndpoint = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.OpenAI && characterWebhook.IntegrationType is not IntegrationType.Empty)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Not OpenAI intergration or Custom character".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} Settings updated")
                                          .WithColor(Color.Green)
                                          .WithDescription("**Changes:**\n");

            // MaxTokens
            if (maxTokens is not null && (maxTokens < 0 || maxTokens > 1000))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} [max-tokens] Availabe values: `0..1000`".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }
            else if (maxTokens is not null)
            {
                embed.Description += $"- Max tokens value was changed from {characterWebhook.GenerationMaxTokens ?? 200} to {maxTokens}\n";
                characterWebhook.GenerationMaxTokens = maxTokens;
            }

            // Temperature
            if (temperature is not null && (temperature < 0 || temperature > 2))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} [temperature] Availabe values: `0.0 ... 2.0`".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }
            else if (temperature is not null)
            {
                embed.Description += $"- Temperature value was changed from {characterWebhook.GenerationTemperature ?? 1.05} to {temperature}\n";
                characterWebhook.GenerationTemperature = temperature;
            }

            // FreqPenalty
            if (frequencyPenalty is not null && (frequencyPenalty < -2.0 || frequencyPenalty > 2.0))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Availabe values: `-2.0 ... 2.0`".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }
            else if (frequencyPenalty is not null)
            {
                embed.Description += $"- Frequency penalty value was changed from {characterWebhook.GenerationFreqPenaltyOrRepetitionSlope ?? 0.9} to {frequencyPenalty}\n";
                characterWebhook.GenerationFreqPenaltyOrRepetitionSlope = frequencyPenalty;
            }

            // PresPenalty
            if (presencePenalty is not null && (presencePenalty < -2.0 || presencePenalty > 2.0))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} [presence-penalty] Availabe values: `-2.0 ... 2.0`".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }
            else if (presencePenalty is not null)
            {
                embed.Description += $"- Presence penalty value was changed from {characterWebhook.GenerationPresenceOrRepetitionPenalty ?? 0.9} to {presencePenalty}\n";
                characterWebhook.GenerationPresenceOrRepetitionPenalty = presencePenalty;
            }

            // Model
            if (openAiModel is not null)
            {
                var model = openAiModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : openAiModel is OpenAiModel.GPT_4 ? "gpt-4" : null;
                embed.Description += $"- Model was changed from {characterWebhook.PersonalApiModel ?? characterWebhook.Channel.Guild.GuildOpenAiModel ?? "`not set`"} to {model}\n";
                characterWebhook.PersonalApiModel = model;
            }

            // Token
            if (personalApiToken is not null)
            {
                embed.Description += $"- Api token was changed from {characterWebhook.PersonalApiToken ?? characterWebhook.Channel.Guild.GuildOpenAiApiToken ?? "`not set`"} to {personalApiToken}\n";
                characterWebhook.PersonalApiToken = personalApiToken;
            }

            // Endpoint
            if (personalApiEndpoint is not null)
            {
                embed.Description += $"- Api endpoint was changed from {characterWebhook.PersonalApiEndpoint ?? characterWebhook.Channel.Guild.GuildOpenAiApiEndpoint?? "`not set`"} to {personalApiEndpoint}\n";
                characterWebhook.PersonalApiEndpoint = personalApiEndpoint;
            }

            await TryToSaveDbChangesAsync(db);

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }
    }
}
