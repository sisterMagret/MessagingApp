namespace Core.Enums;

[Flags]
public enum FeatureType
{
    None = 0,
    VoiceMessages = 1,
    FileSharing = 2,
    GroupChats = 4
}
