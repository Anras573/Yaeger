namespace Yaeger.Rendering;

/// <summary>Tone-mapping curve used by <see cref="ToneMapEffect"/> to compress HDR colour into [0, 1].</summary>
public enum ToneMapOperator
{
    /// <summary>Simple <c>c / (c + 1)</c> curve. Desaturates highlights more than <see cref="AcesFilmic"/>.</summary>
    Reinhard,

    /// <summary>Narkowicz's ACES filmic fit — punchier contrast and rolloff, closer to a film response curve.</summary>
    AcesFilmic,
}
