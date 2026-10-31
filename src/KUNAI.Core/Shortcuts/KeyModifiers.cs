namespace KUNAI.Core.Shortcuts;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    /// <summary>Windows キー / Super キー。</summary>
    Win = 8
}
