/// <summary>
/// Identifies which powerup was used, for <c>GameAnalyticsEvents.PowerupUsage</c>
/// (Assets/_DT1_AdsPlugin/AVNPlugin/Scripts/User Setup/GameAnalyticsEvents.cs). Deliberately in the
/// global namespace (no `namespace Meowdoku { }` wrapper) since that plugin file has none either and
/// references this type unqualified - keeping it here avoids editing the plugin's own using directives.
/// </summary>
public enum PowerupType
{
    RevealCat,
    Hint
}
