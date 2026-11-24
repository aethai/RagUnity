namespace Ragnarok.Content {
    public enum RagnarokContentInitializationState {
        NotStarted,
        LoadingCachedIndex,
        RebuildingIndex,
        Ready,
        Failed
    }
}
