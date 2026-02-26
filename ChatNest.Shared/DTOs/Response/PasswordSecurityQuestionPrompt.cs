namespace ChatNest.Shared.DTOs.Response
{
    public sealed record PasswordSecurityQuestionPrompt
    {
        public required string QuestionKey { get; init; }
        public required string QuestionText { get; init; }
        public required bool HasAnswerConfigured { get; init; }
    }
}
