/// <summary>
/// Interface for objects that need post-deserialization fixup.
/// Called after JSON deserialization to rebuild runtime-only state
/// (e.g., Rounds array, promotion/relegation links).
/// </summary>
public interface ISaveable
{
    void OnAfterDeserialize();
}
