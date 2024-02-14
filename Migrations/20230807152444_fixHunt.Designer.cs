﻿// <auto-generated />
using System;
using CharacterEngineDiscord.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace CharacterEngineDiscord.Migrations
{
    [DbContext(typeof(StorageContext))]
    [Migration("20230807152444_fixHunt")]
    partial class fixHunt
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.9")
                .HasAnnotation("Proxies:ChangeTracking", false)
                .HasAnnotation("Proxies:CheckEquality", false)
                .HasAnnotation("Proxies:LazyLoading", true);

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.BlockedGuild", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("BlockedGuilds");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.BlockedUser", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("From")
                        .HasColumnType("TEXT");

                    b.Property<ulong?>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Hours")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("BlockedUsers");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.Channel", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<float>("RandomReplyChance")
                        .HasColumnType("REAL");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.Character", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("AuthorName")
                        .HasColumnType("TEXT");

                    b.Property<string>("AvatarUrl")
                        .HasColumnType("TEXT");

                    b.Property<string>("Definition")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<string>("Greeting")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("ImageGenEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("Interactions")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<ulong?>("Stars")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Tgt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Characters");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.CharacterWebhook", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("CaiActiveHistoryId")
                        .HasColumnType("TEXT");

                    b.Property<string>("CallPrefix")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("CharacterId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("CurrentSwipeIndex")
                        .HasColumnType("INTEGER");

                    b.Property<int>("IntegrationType")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("LastCallTime")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("LastCharacterDiscordMsgId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LastCharacterMsgUuId")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("LastDiscordUserCallerId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("LastRequestTokensUsage")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LastUserMsgUuId")
                        .HasColumnType("TEXT");

                    b.Property<string>("MessagesFormat")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<float?>("OpenAiFreqPenalty")
                        .HasColumnType("REAL");

                    b.Property<int?>("OpenAiMaxTokens")
                        .HasColumnType("INTEGER");

                    b.Property<string>("OpenAiModel")
                        .HasColumnType("TEXT");

                    b.Property<float?>("OpenAiPresencePenalty")
                        .HasColumnType("REAL");

                    b.Property<float?>("OpenAiTemperature")
                        .HasColumnType("REAL");

                    b.Property<string>("PersonalCaiUserAuthToken")
                        .HasColumnType("TEXT");

                    b.Property<string>("PersonalOpenAiApiEndpoint")
                        .HasColumnType("TEXT");

                    b.Property<string>("PersonalOpenAiApiToken")
                        .HasColumnType("TEXT");

                    b.Property<bool>("ReferencesEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<float>("ReplyChance")
                        .HasColumnType("REAL");

                    b.Property<int>("ResponseDelay")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("SkipNextBotMessage")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("SwipesEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UniversalJailbreakPrompt")
                        .HasColumnType("TEXT");

                    b.Property<string>("WebhookToken")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ChannelId");

                    b.HasIndex("CharacterId");

                    b.ToTable("CharacterWebhooks");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.Guild", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("BtnsRemoveDelay")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("GuildCaiPlusMode")
                        .HasColumnType("INTEGER");

                    b.Property<string>("GuildCaiUserToken")
                        .HasColumnType("TEXT");

                    b.Property<string>("GuildJailbreakPrompt")
                        .HasColumnType("TEXT");

                    b.Property<string>("GuildMessagesFormat")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("GuildOpenAiApiEndpoint")
                        .HasColumnType("TEXT");

                    b.Property<string>("GuildOpenAiApiToken")
                        .HasColumnType("TEXT");

                    b.Property<string>("GuildOpenAiModel")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Guilds");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.HuntedUser", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<float>("Chance")
                        .HasColumnType("REAL");

                    b.Property<ulong>("CharacterWebhookId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("CharacterWebhookId");

                    b.ToTable("HuntedUsers");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.OpenAiHistoryMessage", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("CharacterWebhookId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("CharacterWebhookId");

                    b.ToTable("OpenAiHistoryMessages");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.BlockedUser", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Models.Database.Guild", "Guild")
                        .WithMany("BlockedUsers")
                        .HasForeignKey("GuildId");

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.Channel", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Models.Database.Guild", "Guild")
                        .WithMany("Channels")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.CharacterWebhook", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Models.Database.Channel", "Channel")
                        .WithMany("CharacterWebhooks")
                        .HasForeignKey("ChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("CharacterEngineDiscord.Models.Database.Character", "Character")
                        .WithMany("CharacterWebhooks")
                        .HasForeignKey("CharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Channel");

                    b.Navigation("Character");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.HuntedUser", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Models.Database.CharacterWebhook", "CharacterWebhook")
                        .WithMany("HuntedUsers")
                        .HasForeignKey("CharacterWebhookId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CharacterWebhook");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.OpenAiHistoryMessage", b =>
                {
                    b.HasOne("CharacterEngineDiscord.Models.Database.CharacterWebhook", "CharacterWebhook")
                        .WithMany("OpenAiHistoryMessages")
                        .HasForeignKey("CharacterWebhookId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CharacterWebhook");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.Channel", b =>
                {
                    b.Navigation("CharacterWebhooks");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.Character", b =>
                {
                    b.Navigation("CharacterWebhooks");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.CharacterWebhook", b =>
                {
                    b.Navigation("HuntedUsers");

                    b.Navigation("OpenAiHistoryMessages");
                });

            modelBuilder.Entity("CharacterEngineDiscord.Models.Database.Guild", b =>
                {
                    b.Navigation("BlockedUsers");

                    b.Navigation("Channels");
                });
#pragma warning restore 612, 618
        }
    }
}
