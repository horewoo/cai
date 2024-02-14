using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    public class OtherCommands(IDiscordClient client) : InteractionModuleBase<InteractionContext>
    {
        private readonly DiscordSocketClient _client = (DiscordSocketClient)client;

        
        [SlashCommand("help", "Información sobre la utilización de IA con el bot.")]
        public async Task BaicsHelp(bool silent = true)
        {
            var embed = new EmbedBuilder().WithTitle("Character Engine").WithColor(0xededed)
                                          .WithDescription("**Código de: `drizzle-mizzle`, traducción y adaptación por `olivervamp`** <a:fr_hug:1182414057538601011>\n" +
                                                           "# ¿Como utilizar? #\n" +
                                                           "1. Utiliza uno de los comandos `/spawn` para crear un personaje.\n" +
                                                           "2. Modifíquelo con uno de los comandos `/update` utilizando el prefix o ID de webhook dado.\n" +
                                                           "3. Llama al personaje mencionando su prefijo o con respuesta en cualquiera de sus mensajes.\n" +
                                                           "4. Si desea iniciar el chat con algún personaje desde el principio, utilice el comando `/reset-character`.\n" +
                                                           "5. Puede leer [Notas importantes y guía](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Important-Notes-and-Additional-Guides) y [wiki/Comandos](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Commands) para saber más.")
                                          .AddField("También", "Es muy recomendable ver `/help-messages-format`");
                                          
            await RespondAsync(embed: embed.Build(), ephemeral: silent);
        }

        [SlashCommand("help-messages-format", "[No traducido] Información sobre el formato de los mensajes")]
        public async Task MessagesFormatHelp(bool silent = true)
        {
            var embed = new EmbedBuilder().WithTitle("Messages format").WithColor(0xededed)
                                          .AddField("Description", "This setting allows you to change the format of messages that character will get from users.")
                                          .AddField("Commands", "`/show messages-format` - Check the current format of messages for this server or certain character\n" +
                                                                "`/update messages-format` - Change the format of messages for certain character\n" +
                                                                "`/set-server-messages-format` - Change the format of messages for all **new** characters on this server")
                                          .AddField("Placeholders", "You can use these placeholders in your formats to manipulate the data that being inserted in your messages:\n" +
                                                                    "**`{{msg}}`** - **Required** placeholder that contains the message itself.\n" +
                                                                    "**`{{user}}`** - Placeholder that contains the user's Discord name *(server nickname > display name > username)*.\n" +
                                                                    "**`{{ref_msg_begin}}`**, **`{{ref_msg_user}}`**, **`{{ref_msg_text}}`**, **`{{ref_msg_end}}`** - Combined placeholder that contains the referenced message (one that user was replying to). *Begin* and *end* parts are needed because user message can have no referenced message, and then placeholder will be removed.\n")
                                          .AddField("Example", "Format:\n*`{{ref_msg_begin}}((In response to '{{ref_msg_text}}' from '{{ref_msg_user}}')){{ref_msg_end}}\\n{{user}} says:\\n{{msg}}`*\n" +
                                                               "Inputs:\n- referenced message with text *`Hello`* from user *`Dude`*;\n- user with name *`Average AI Enjoyer`*;\n- message with text *`Do you love donuts?`*\n" +
                                                               "Result (what character will see):\n*`((In response to 'Hello' from 'Dude'))\nAverage AI Enjoyer says:\nDo you love donuts?`*\n" +
                                                               "Example above is used by default, but you are free to play with it the way you want, or you can simply disable it by setting the default message format with `{{msg}}`.");
            await RespondAsync(embed: embed.Build(), ephemeral: silent);
        }


        [SlashCommand("ping", "ping")]
        public async Task Ping()
        {
            await RespondAsync(embed: $":ping_pong: Pong! - {_client.Latency} ms".ToInlineEmbed(Color.Red));
        }
    }
}
