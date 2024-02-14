﻿namespace CharacterEngineDiscord.Models.Common
{
    public class CharacterResponse
    {
        public required string Text { get; set; }
        public string? CharacterMessageId { get; set; }
        public string? UserMessageId { get; set; }
        public string? ImageRelPath { get; set; }
        public int TokensUsed { get; set; } = 0;
        public required bool IsSuccessful { get; set; }
        public bool IsFailure { get => !IsSuccessful; }
    }
}
