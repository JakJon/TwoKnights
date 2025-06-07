using UnityEngine;

/// <summary>
/// Defines the different types that can be assigned to enemies.
/// Multiple types can be combined using the bitwise OR operator (|).
/// </summary>
[System.Flags]
public enum EnemyType
{
    None = 0,
    Ground = 1 << 0,    // Enemy that moves on the ground
    Splitting = 1 << 1  // Enemy that can split into smaller versions
}