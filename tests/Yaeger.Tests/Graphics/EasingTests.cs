using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class EasingTests
{
    private static readonly EasingFunction[] AllFunctions = Enum.GetValues<EasingFunction>();

    public static IEnumerable<object[]> AllFunctionsData =>
        AllFunctions.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(AllFunctionsData))]
    public void Apply_AtZero_ShouldReturnZero(EasingFunction function)
    {
        Assert.Equal(0f, Easing.Apply(function, 0f), 0.0001f);
    }

    [Theory]
    [MemberData(nameof(AllFunctionsData))]
    public void Apply_AtOne_ShouldReturnOne(EasingFunction function)
    {
        Assert.Equal(1f, Easing.Apply(function, 1f), 0.0001f);
    }

    [Fact]
    public void Linear_ShouldReturnInputUnchanged()
    {
        Assert.Equal(0.3f, Easing.Linear(0.3f), 0.0001f);
        Assert.Equal(0.7f, Easing.Linear(0.7f), 0.0001f);
    }

    [Fact]
    public void QuadIn_AtHalf_ShouldBeQuarter()
    {
        Assert.Equal(0.25f, Easing.QuadIn(0.5f), 0.0001f);
    }

    [Fact]
    public void QuadOut_AtHalf_ShouldBeThreeQuarters()
    {
        Assert.Equal(0.75f, Easing.QuadOut(0.5f), 0.0001f);
    }

    [Fact]
    public void CubicIn_AtHalf_ShouldBeOneEighth()
    {
        Assert.Equal(0.125f, Easing.CubicIn(0.5f), 0.0001f);
    }

    [Fact]
    public void BackIn_BeforeThreshold_ShouldOvershootBelowZero()
    {
        // BackIn pulls back below the start value before accelerating — a signature trait
        // distinguishing it from a plain ease-in curve.
        Assert.True(Easing.BackIn(0.2f) < 0f);
    }

    [Fact]
    public void BackOut_NearEnd_ShouldOvershootAboveOne()
    {
        Assert.True(Easing.BackOut(0.8f) > 1f);
    }

    [Fact]
    public void ElasticOut_EarlyCurve_ShouldOvershootAboveOne()
    {
        Assert.True(Easing.ElasticOut(0.1f) > 1f);
    }

    [Fact]
    public void Apply_QuadInOut_MatchesDirectCall()
    {
        Assert.Equal(Easing.QuadInOut(0.42f), Easing.Apply(EasingFunction.QuadInOut, 0.42f));
    }

    [Fact]
    public void Apply_UnknownEnumValue_FallsBackToLinear()
    {
        var unknown = (EasingFunction)999;

        Assert.Equal(Easing.Linear(0.42f), Easing.Apply(unknown, 0.42f));
    }

    [Theory]
    [MemberData(nameof(AllFunctionsData))]
    public void Apply_IsMonotonicAtEndpoints_DoesNotThrow(EasingFunction function)
    {
        // Smoke test across the full domain in fine steps — every curve must stay finite.
        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20f;
            Assert.True(float.IsFinite(Easing.Apply(function, t)));
        }
    }
}
