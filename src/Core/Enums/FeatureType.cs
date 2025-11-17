namespace Core.Enums;

[Flags]
public enum FeatureType
{
    None = 0,
    VoiceMessage = 1,
    FileSharing = 2,
    GroupChat = 4,
    EmailAlerts = 5
}
