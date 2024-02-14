using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.CharacterHub;
using Discord.Webhook;
using Discord.WebSocket;
using CharacterEngineDiscord.Services.AisekaiIntegration.SearchEnums;
using CharacterEngineDiscord.Interfaces;
using PuppeteerSharp.Helpers;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    [Group("spawn", "Genera un nuevo personaje.")]
    public class SpawnCharacterCommands(IIntegrationsService integrations) : InteractionModuleBase<InteractionContext>
    {
        private const string sqDesc = "Para buscar con ID, establece 'set-with-id' a 'True'";
        private const string tagsDesc = "Separa los tags con ','";


        [SlashCommand("cai-character", "Agrega un nuevo personaje de Character.ai en este canal.")]
        public async Task SpawnCaiCharacter([Summary(description: sqDesc)] string searchQueryOrCharacterId, bool setWithId = false, bool silent = false)
            => await SpawnCaiCharacterAsync(searchQueryOrCharacterId, setWithId, silent);


        [SlashCommand("aisekai-character", "Agrega un nuevo personaje de Aisekai en este canal.")]
        public async Task SpawnAisekaiCharacter([Summary(description: sqDesc)] string? searchQueryOrCharacterId = null, bool setWithId = false, [Summary(description: tagsDesc)] string? tags = null, bool allowNsfw = true, SearchSort sort = SearchSort.desc, SearchTime time = SearchTime.all, SearchType type = SearchType.best, bool silent = false)
            => await SpawnAisekaiCharacterAsync(searchQueryOrCharacterId, setWithId, tags, allowNsfw, sort, time, type, silent);

        [SlashCommand("chub-character", "Agrega un nuevo personaje de CharacterHub a este canal.")]
        public async Task SpawnChubCharacter(ApiTypeForChub apiType, [Summary(description: sqDesc)] string? searchQueryOrCharacterId = null, bool setWithId = false, [Summary(description: tagsDesc)] string? tags = null, bool allowNSFW = true, SortField sortBy = SortField.MostPopular, bool silent = false)
            => await SpawnChubCharacterAsync(apiType, searchQueryOrCharacterId, tags, allowNSFW, sortBy, setWithId, silent);

        [SlashCommand("custom-character", "Agrega un personaje a este canal para personalizarlo <3")]
        public async Task SpawnCustomTavernCharacter()
            => await RespondWithCustomCharModalasync();


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task SpawnChubCharacterAsync(ApiTypeForChub apiType, string? searchQueryOrCharacterId, string? tags, bool allowNSFW, SortField sortBy, bool setWithId, bool silent)
        {
            await DeferAsync(ephemeral: silent);
            EnsureCanSendMessages();

            if (Context.Channel is ITextChannel { IsNsfw: false })
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Advertencia. Los personajes proporcionados por chub.ai pueden contener imágenes y descripciones de avatares NSFW.".ToInlineEmbed(Color.Purple), ephemeral: silent);

            IntegrationType integrationType = apiType is ApiTypeForChub.OpenAI ? IntegrationType.OpenAI
                                            : apiType is ApiTypeForChub.KoboldAI ? IntegrationType.KoboldAI
                                            : apiType is ApiTypeForChub.HordeKoboldAI ? IntegrationType.HordeKoboldAI
                                            : IntegrationType.Empty;

            if (setWithId)
            {
                await FollowupAsync(embed: WAIT_MESSAGE, ephemeral: silent);

                var chubCharacter = await GetChubCharacterInfoAsync(searchQueryOrCharacterId ?? string.Empty, integrations.ChubAiHttpClient);
                var character = CharacterFromChubCharacterInfo(chubCharacter);
                await FinishSpawningAsync(integrationType, character);
            }
            else // set with search
            {
                await FollowupAsync(embed: WAIT_MESSAGE, ephemeral: silent);

                var response = await SearchChubCharactersAsync(new()
                {
                    Text = searchQueryOrCharacterId ?? "",
                    Amount = 300,
                    Tags = tags ?? "",
                    ExcludeTags = "",
                    Page = 1,
                    SortBy = sortBy,
                    AllowNSFW = allowNSFW
                }, integrations.ChubAiHttpClient);

                var searchQueryData = SearchQueryDataFromChubResponse(integrationType, response);
                await FinishSearchAsync(searchQueryData);
            }
        }

        private async Task SpawnCaiCharacterAsync(string searchQueryOrCharacterId, bool setWithId = false, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);
            EnsureCanSendMessages();

            if (integrations.CaiClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} La integración de CharacterAI está desactivada".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await using var db = new StorageContext();
            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, db);
            
            var caiToken = channel.Guild.GuildCaiUserToken ?? string.Empty;

            if (string.IsNullOrWhiteSpace(caiToken))
            {
                await FollowupAsync(embed: ($"{WARN_SIGN_DISCORD} Necesitas establecer primero un token de acceso de CharacterAI en este servidor.\n" +
                                            $"¿Cómo obtener?: [wiki/Important-Notes-and-Additional-Guides](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Important-Notes-and-Additional-Guides#get-characterai-user-auth-token)\n" +
                                            $"Cmado: `/set-server-cai-token`").ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            var plusMode = channel.Guild.GuildCaiPlusMode ?? false;

            await FollowupAsync(embed: WAIT_MESSAGE, ephemeral: silent);
            
            while (integrations.CaiReloading)
                await Task.Delay(5000);

            var id = Guid.NewGuid();
            integrations.RunningCaiTasks.Add(id);
            try
            {
                if (setWithId)
                {
                    var caiCharacter = await integrations.CaiClient.GetInfoAsync(searchQueryOrCharacterId,
                        authToken: caiToken, plusMode: plusMode).WithTimeout(60000);
                    var character = CharacterFromCaiCharacterInfo(caiCharacter);

                    await FinishSpawningAsync(IntegrationType.CharacterAI, character);
                }
                else // set with search
                {
                    var response = await integrations.CaiClient.SearchAsync(searchQueryOrCharacterId,
                        authToken: caiToken, plusMode: plusMode).WithTimeout(60000);
                    var searchQueryData = SearchQueryDataFromCaiResponse(response);

                    await FinishSearchAsync(searchQueryData);
                }
            }
            finally { integrations.RunningCaiTasks.Remove(id); }
        }

        

        private async Task SpawnAisekaiCharacterAsync(string? searchQueryOrCharacterId, bool setWithId, string? tags, bool nsfw, SearchSort sort, SearchTime time, SearchType type, bool silent)
        {
            await DeferAsync(ephemeral: silent);
            EnsureCanSendMessages();

            await using var db = new StorageContext();
            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, db);

            string? authToken = channel.Guild.GuildAisekaiAuthToken;

            if (string.IsNullOrWhiteSpace(authToken))
            {
                await FollowupAsync(embed: ($"{WARN_SIGN_DISCORD} Primero tienes que especificar una cuenta de usuario Aisekai para tu servidor.\n" +                                            
                                            $"Comando: `/set-server-aisekai-auth email:... password:...`").ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await FollowupAsync(embed: WAIT_MESSAGE, ephemeral: silent);

            if (setWithId)
            {
                await SpawnAisekaiCharacterWithIdAsync(channel.Guild, searchQueryOrCharacterId ?? string.Empty, authToken);
            }
            else // set with search
            {
                var response = await integrations.AisekaiClient.GetSearchAsync(authToken, searchQueryOrCharacterId, time, type, sort, nsfw, 1, 100, tags);
                var searchQueryData = SearchQueryDataFromAisekaiResponse(response);

                await FinishSearchAsync(searchQueryData);
            }
        }

        private async Task SpawnAisekaiCharacterWithIdAsync(Models.Database.Guild guild, string characterId, string authToken)
        {
            var response = await integrations.AisekaiClient.GetCharacterInfoAsync(authToken, characterId);

            if (response.IsSuccessful)
            {
                var character = CharacterFromAisekaiCharacterInfo(response.Character!.Value);
                await FinishSpawningAsync(IntegrationType.Aisekai, character);
            }
            else if (response.Code == 401)
            {   // Re-login
                var newAuthToken = await integrations.UpdateGuildAisekaiAuthTokenAsync(guild.Id, guild.GuildAisekaiRefreshToken!);
                if (newAuthToken is null)
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Falló al iniciar sesión con Aisekai`".ToInlineEmbed(Color.Red));
                else
                    await SpawnAisekaiCharacterWithIdAsync(guild, characterId, newAuthToken);
            }
            else
            {
                await ModifyOriginalResponseAsync(r => r.Embed = $"{WARN_SIGN_DISCORD} Falló al obtener la información del personaje: `{response.ErrorReason}`".ToInlineEmbed(Color.Red));
            }
        }

        private async Task FinishSpawningAsync(IntegrationType type, Models.Database.Character? character)
        {
            if (character is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} ¡Algo mal ha pasao'!".ToInlineEmbed(Color.Red));
                return;
            }

            var fromChub = type is not IntegrationType.CharacterAI && type is not IntegrationType.Aisekai;
            var characterWebhook = await CreateCharacterWebhookAsync(type, Context, character, integrations, fromChub);

            if (characterWebhook is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} ¡Algo mal ha pasao'!".ToInlineEmbed(Color.Red));
                return;
            }

            await using var db = new StorageContext();
            characterWebhook = db.Entry(characterWebhook).Entity;

            var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            integrations.WebhookClients.TryAdd(characterWebhook.Id, webhookClient);

            var originalMessage = await ModifyOriginalResponseAsync(msg => msg.Embed = SpawnCharacterEmbed(characterWebhook));
            if (type is IntegrationType.Aisekai)
                await Context.Channel.SendMessageAsync(embed: ":zap: Por favor, presta atención al hecho de que los personajes Aisekai no soportan historiales de chat separados. Por lo tanto, si creas el mismo personaje en dos canales diferentes, ambos canales seguirán compartiendo el mismo contexto de chat; lo mismo ocurre con el comando `/reset-character`: una vez ejecutado, el historial de chat se borrará en cada canal en el que esté presente el personaje especificado.".ToInlineEmbed(Color.Gold, false));

            string characterMessage = $"{Context.User.Mention} {character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(Context.User as SocketGuildUser)?.GetBestName()}**")}";
            if (characterMessage.Length > 2000) characterMessage = characterMessage[..1994] + "[...]";

            // Try to set avatar
            Stream? image = null;
            if (!string.IsNullOrWhiteSpace(characterWebhook.Character.AvatarUrl))
            {
                var imageUrl = originalMessage.Embeds?.Single()?.Image?.ProxyUrl;
                image = await TryToDownloadImageAsync(imageUrl, integrations.ImagesHttpClient);
            }
            image ??= new MemoryStream(await File.ReadAllBytesAsync($"{EXE_DIR}{SC}storage{SC}default_avatar.png"));
            
            try { await webhookClient.ModifyWebhookAsync(w => w.Image = new Image(image)); }
            finally { await image.DisposeAsync(); }

            await webhookClient.SendMessageAsync(characterMessage);
        }

        private async Task FinishSearchAsync(SearchQueryData searchQueryData)
        {
            var newSQ = await BuildAndSendSelectionMenuAsync(Context, searchQueryData);
            if (newSQ is null) return;

            var lastSQ = integrations.SearchQueries.Find(sq => sq.ChannelId == newSQ.ChannelId);

            await integrations.SearchQueriesLock.WaitAsync();
            try
            {
                if (lastSQ is not null) // stop tracking the last query in this channel
                    integrations.SearchQueries.Remove(lastSQ);

                integrations.SearchQueries.Add(newSQ); // and start tracking this one
            }
            finally
            {
                integrations.SearchQueriesLock.Release();
            }
        }

        private async Task RespondWithCustomCharModalasync()
        {
            var modal = new ModalBuilder().WithTitle($"Crear un personaje (AÚN NO TRADUCIDO)")
                                            .WithCustomId("spawn")
                                            .AddTextInput($"Name", "name", TextInputStyle.Short, required: true)
                                            .AddTextInput($"First message", "first-message", TextInputStyle.Paragraph, "*{{char}} joins server*\nHello everyone!", required: true)
                                            .AddTextInput($"Definition-1", "definition-1", TextInputStyle.Paragraph, required: true, value:
                                                        "((DELETE THIS SECTION))\n" +
                                                        "  Discord doesn't allow to set\n" +
                                                        "  more than 5 rows in one modal, so\n" +
                                                        "  you'll have to write the whole\n" +
                                                        "  character definition in these two.\n" +
                                                        "  It's highly recommended to follow\n" +
                                                        "  this exact pattern below and fill\n" +
                                                        "  each line one by one.\n" +
                                                        "  Remove or rename lines that are not\n" +
                                                        "  needed. Custom Jailbreak prompt can\n" +
                                                        "  be set with `/update` command later.\n" +
                                                        "  Default one can be seen be seen with\n" +
                                                        "  `show jailbreak-prompt` command.\n" +
                                                        "((DELETE THIS SECTION))\n\n" +
                                                        "{{char}}'s personality: ALL BASIC INFO ABOUT CHARACTER, CONTINUE IN THE NEXT FIELD IF OUT OF SPACE.")
                                            .AddTextInput($"Definition-2", "definition-2", TextInputStyle.Paragraph, required: false, value:
                                                        "((DELETE THIS SECTION))\n" +
                                                        "  This section will simply continue\n" +
                                                        "  the previous one, as if these two were\n" +
                                                        "  just one big text field.\n" +
                                                        "((DELETE THIS SECTION))\n\n" +
                                                        "Scenario of roleplay: {{char}} has joined Discord!\n\n" +
                                                        "Example conversations between {{char}} and {{user}}:\n<START>\n{{user}}: Nullpo;\n{{char}}: Gah!\n<END>")
                                            .AddTextInput($"Avatar url", "avatar-url", TextInputStyle.Short, "https://some.site/.../avatar.jpg", required: false)
                                            .Build();

            await RespondWithModalAsync(modal);
        }

        private void EnsureCanSendMessages()
        {
            try
            {
                if (Context.Channel is not ITextChannel tc)
                    throw new();
                else if (tc.Name is null)
                    throw new();
            }
            catch
            {
                throw new($"{WARN_SIGN_DISCORD} ¡Tienes que invitar al bot a este canal para ejecutar comandos aquí!");
            }
        }
    }
}
