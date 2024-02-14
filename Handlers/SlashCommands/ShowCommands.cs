using System.Data.Entity;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Discord;
using CharacterEngineDiscord.Models.Common;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    [Group("show", "Show-commands")]
    public class ShowCommands() : InteractionModuleBase<InteractionContext>
    {
        //private readonly DiscordSocketClient _client = (DiscordSocketClient)client;


        [SlashCommand("all-characters", "Ve todos los personajes de este canal.")]
        public async Task ShowCharacters(int page = 1, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var channelId = Context.Channel is IThreadChannel tc ? tc.CategoryId ?? 0 : Context.Channel.Id;

            await using var db = new StorageContext();
            var channel = await FindOrStartTrackingChannelAsync(channelId, Context.Guild.Id, db);
            
            if (channel.CharacterWebhooks.Count == 0)
            {
                await FollowupAsync(embed: $"No se han encontrado personajes en este canal.".ToInlineEmbed(0xededed), ephemeral: silent);
                return;
            }

            var rCharacterWebhooks = Enumerable.Reverse(channel.CharacterWebhooks).ToList();
            var embed = new EmbedBuilder().WithColor(Color.Purple);
            int start = (page - 1) * 5;
            int end = (rCharacterWebhooks.Count - start) > 5 ? (start + 4) : start + (rCharacterWebhooks.Count - start - 1);

            for (int i = start; i <= end; i++)
            {
                var cw = rCharacterWebhooks.ElementAt(i);
                string integrationType = cw.IntegrationType is IntegrationType.CharacterAI ?
                                            $"**[CharacterAI](https://beta.character.ai/chat?char={cw.Character.Id})**"
                                       : cw.IntegrationType is IntegrationType.Aisekai ?
                                            $"**[Aisekai](https://www.aisekai.ai/chat/{cw.Character.Id})**"
                                       : cw.IntegrationType is IntegrationType.OpenAI ?
                                            $"`OpenAI {cw.PersonalApiModel}` **{(cw.FromChub ? $"[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})" : "(custom character)")}**"
                                       : cw.IntegrationType is IntegrationType.KoboldAI ?
                                            $"`KoboldAI` **{(cw.FromChub ? $"[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})" : "(custom character)")}**"
                                       : cw.IntegrationType is IntegrationType.HordeKoboldAI ?
                                            $"`Horde KoboldAI` **{(cw.FromChub ? $"[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})" : "(custom character)")}**"
                                       : "not set";

                string val = $"Prefix: *`{cw.CallPrefix}`*\n" +
                             $"Tipo de integración: {integrationType}\n" +
                             $"ID de Webhook: *`{cw.Id}`*\n" +
                             $"Mensajes enviados: *`{cw.MessagesSent}`*";

                embed.AddField($"{++start}. {cw.Character.Name}", val);
            }

            double pages = Math.Ceiling(rCharacterWebhooks.Count / 5d);
            embed.WithTitle($"Personajes en este canal: {rCharacterWebhooks.Count}");
            embed.WithFooter($"Página {page}/{pages}");

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }


        [SlashCommand("character-info", "Ve la información de un personaje")]
        public async Task ShowInfo(string webhookIdOrPrefix, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Personaje con el prefix o el ID de webhook dado no fue encontrado en el canal actual".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            var character = characterWebhook.Character;

            var _tagline = character.Title?.Replace("\n\n", "\n");
            if (string.IsNullOrWhiteSpace(_tagline)) _tagline = "Sin titulo";

            string? _characterDesc = character.Description?.Replace("\n\n", "\n");
            if (string.IsNullOrWhiteSpace(_characterDesc)) _characterDesc = "Sin descripción";

            string _statAndLink = characterWebhook.IntegrationType is IntegrationType.CharacterAI ?
                          $"Link: [Chat con {character.Name}](https://beta.character.ai/chat?char={character.Id})\nInteracciones: `{character.Interactions}`"
                                : characterWebhook.IntegrationType is IntegrationType.Aisekai ?
                          $"Link: [Chat con {character.Name}](https://www.aisekai.ai/chat/{character.Id})\nDialogos: `{character.Interactions}`\nLikes: `{character.Stars}`"
                                : characterWebhook.FromChub ?
                          $"Link: [{character.Name} en chub.ai](https://www.chub.ai/characters/{character.Id})\nStars: `{character.Stars}`"
                                : "Personaje personalizado";
            string _api = characterWebhook.IntegrationType is IntegrationType.OpenAI ?
                          $"OpenAI ({characterWebhook.PersonalApiModel})"
                        : characterWebhook.IntegrationType is IntegrationType.KoboldAI ?
                          $"KoboldAI ({characterWebhook.PersonalApiModel})"
                        : characterWebhook.IntegrationType is IntegrationType.HordeKoboldAI ?
                          $"Horde KoboldAI ({characterWebhook.PersonalApiModel})"
                        : characterWebhook.IntegrationType.ToString();

            string details = $"*{_statAndLink}\n¿Puede generar imagenes? {(character.ImageGenEnabled is true ? "¡Sí!" : "¡No!")}*";
            string info = $"Prefix: *`{characterWebhook.CallPrefix}`*\n" +
                          $"Webhook ID: *`{characterWebhook.Id}`*\n" +
                          $"API: *`{_api}`*\n" +
                          $"Mensajes enviados: *`{characterWebhook.MessagesSent}`*\n" +
                          $"Citas habilitadas: *`{characterWebhook.ReferencesEnabled}`*\n" +
                          $"Swipes habilitados: *`{characterWebhook.SwipesEnabled}`*\n" +
                          $"Botón Continuar activado: *`{characterWebhook.CrutchEnabled}`*\n" +
                          $"Delay de respuesta: *`{characterWebhook.ResponseDelay}s`*\n" +
                          $"Respuesta: *`{characterWebhook.ReplyChance}%`*\n" +
                          $"Usuarios escuchados: *`{characterWebhook.HuntedUsers.Count}`*";
            string fullDesc = $"*{_tagline}*\n\n**Descripción**\n{_characterDesc}";

            if (fullDesc.Length > 4096)
                fullDesc = fullDesc[..4090] + "[...]";

            var emb = new EmbedBuilder()
                .WithColor(Color.Gold)
                .WithTitle($"{OK_SIGN_DISCORD} **{character.Name}**")
                .WithDescription($"{fullDesc}\n")
                .AddField("Detalles", details)
                .AddField("Ajustes", info)
                .WithImageUrl(characterWebhook.Character.AvatarUrl)
                .WithFooter($"Creado por {character.AuthorName}");

            await FollowupAsync(embed: emb.Build(), ephemeral: silent);
        }


        [SlashCommand("cai-history-id", "Mostrar ID del historial de c.ai")]
        public async Task ShowCaiHistoryId(string webhookIdOrPrefix, bool silent = false)
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
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No se puede mostrar el identificador de historial para la integración que no es de CharacterAI.".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Current history ID: `{characterWebhook.ActiveHistoryID}`".ToInlineEmbed(Color.Green), ephemeral: silent);
        }


        [SlashCommand("dialog-history", "Muestra los ultimos 15 mensajes de un personaje.")]
        public async Task ShowDialogHistory(string webhookIdOrPrefix, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No se ha encontrado el personaje con el prefix o el identificador de webhook dado en el canal actual.".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            var type = characterWebhook.IntegrationType;
            if (type is not IntegrationType.OpenAI && type is not IntegrationType.KoboldAI && type is not IntegrationType.HordeKoboldAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No se puede mostrar el historial de {type}".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            if (characterWebhook.StoredHistoryMessages.Count == 0)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No se han encontrado mensajes".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            int amount = Math.Min(characterWebhook.StoredHistoryMessages.Count, 15);
            var embed = new EmbedBuilder().WithColor(Color.Green).WithTitle($"{OK_SIGN_DISCORD} Últimos {amount} mensajes con {characterWebhook.Character.Name}");

            var chunks = new List<string>();
            for (int i = characterWebhook.StoredHistoryMessages.Count - 1; i >= 0; i--)
            {
                var message = characterWebhook.StoredHistoryMessages[i];
                int l = Math.Min(message.Content.Length, 250);
                chunks.Add($"{amount--}. **{(message.Role == "user" ? "User" : characterWebhook.Character.Name)}**: *{message.Content[..l].Replace("\n", "  ").Replace("*", " ")}{(l == 250 ? "..." : "")}*\n");
                if (amount == 0) break;
            }
            chunks.Reverse();

            var result = new List<string>() { "" };
            int resultIndex = 0;
            foreach (var chunk in chunks)
            {
                // if string becomes too big, start new
                if ((result.ElementAt(resultIndex).Length + chunk.Length) > 1024)
                {
                    resultIndex++;
                    result.Add(chunk);
                    continue;
                }
                // else, append to it
                result[resultIndex] += chunk;
            }

            for (int i = 0; i < Math.Min(result.Count, 5); i++)
            {
                string newLine = result[i].Length > 1024 ? result[i][0..1018] + "[...]" : result[i];
                embed.AddField(@"\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~", newLine);
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }


        [SlashCommand("last-request-cost", "~")]
        public async Task ShowLastRequestCost(string webhookIdOrPrefix, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new StorageContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No se ha encontrado el personaje con el prefix o ID dada.".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} {characterWebhook.LastRequestTokensUsage.ToString() ?? "?"} tokens".ToInlineEmbed(Color.Green), ephemeral: silent);
        }


        [SlashCommand("messages-format", "Ve el formato de mensajes del personaje.")]
        public async Task ShowMessagesFormat(string? webhookIdOrPrefix = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            string title;
            string format;

            await using var db = new StorageContext();
            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, db);
            
            if (webhookIdOrPrefix is null)
            {
                title = "Formato de mensaje";
                format = channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
            }
            else
            {
                var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

                if (characterWebhook is null)
                {
                    await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                    return;
                }
                
                title = $"Formato de __{characterWebhook.Character.Name}__";
                format = characterWebhook.PersonalMessagesFormat ?? channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
            }

            string text = format.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer");

            if (text.Contains("{{ref_msg_text}}"))
            {
                text = text.Replace("{{ref_msg_text}}", "Hola")
                           .Replace("{{ref_msg_begin}}", "")
                           .Replace("{{ref_msg_end}}", "")
                           .Replace("{{ref_msg_user}}", "Dude")
                           .Replace("\\n", "\n");
            }

            var embed = new EmbedBuilder().WithTitle($"{title}")
                                          .WithColor(Color.Gold)
                                          .AddField("Formato:", $"`{format}`")
                                          .AddField("Ejemplo", $"Referencia: *`Hola`* from user *`Dude`*\n" +
                                                               $"Nombre del usuario: `Average AI Enjoyer`\n" +
                                                               $"Mensaje del usuario: *`Hello!`*\n" +
                                                               $"Resultado (como se ve):\n*`{text}`*");

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }


        [SlashCommand("jailbreak-prompt", "Compruebe por defecto o carácter jailbreak prompt")]
        public async Task ShowJailbreakPrompt(string? webhookIdOrPrefix = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            string title;
            string prompt;

            await using var db = new StorageContext();
            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, db);
            
            if (webhookIdOrPrefix is null)
            {
                title = "Default jailbreak prompt";
                prompt = channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!;
            }
            else
            {
                var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

                if (characterWebhook is null)
                {
                    await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                    return;
                }

                var type = characterWebhook.IntegrationType;
                if (type is IntegrationType.CharacterAI or IntegrationType.Aisekai)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No disponible para la integración con **{type}**".ToInlineEmbed(Color.Red), ephemeral: silent);
                    return;
                }


                title = $"{characterWebhook.Character.Name}'s jailbreak prompt";
                prompt = (characterWebhook.PersonalJailbreakPrompt ?? channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!).Replace("{{char}}", $"{characterWebhook.Character.Name}");
            }

            var embed = new EmbedBuilder().WithTitle($"**{title}**")
                                          .WithColor(Color.Gold);

            var promptChunked = prompt.Chunk(1016);

            foreach (var chunk in promptChunked)
                embed.AddField(@"\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~", $"```{new string(chunk)}```");

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }
    }
}
