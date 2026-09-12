namespace Parlotype.Desktop.Views;

/// <summary>Time-based audio envelope and a softly tapered, continuous wave silhouette.</summary>
internal sealed class WaveformAnimation
{
    internal const int BarCount = 15;

    private const float SilenceThreshold = 0.0035f;
    private const float MinimumAmplitude = 0f;
    private const float MaximumAmplitude = 1f;
    private const double ResponseMultiplier = 45;
    private const double ResponseLogarithmBase = 10;
    private const double MaximumElapsedSeconds = 2;
    private const double IntegrationStepSeconds = 1.0 / 120;
    private const double AttackTimeConstantSeconds = 0.065;
    private const double ReleaseTimeConstantSeconds = 0.085;
    private const double MinimumPhaseSpeed = 1.6;
    private const double AdditionalPhaseSpeed = 2.4;
    private const double CenterBarIndex = (BarCount - 1) / 2.0;
    private const double EdgeTaper = 0.22;
    private const double CenterTaper = 1.0 - EdgeTaper;
    private const double TaperExponent = 1.3;
    private const double HalfTurnRadians = Math.PI / 2;
    private const double WaveBaseline = 0.54;
    private const double PrimaryWaveWeight = 0.29;
    private const double PrimaryWavePhaseSpeed = 2.1;
    private const double PrimaryWaveBarSpacing = 4.4;
    private const double SecondaryWaveWeight = 0.17;
    private const double SecondaryWavePhaseSpeed = 3.2;
    private const double SecondaryWaveBarSpacing = 6.2;
    private const double BarSmoothingTimeConstantSeconds = 0.045;
    private const double RestThreshold = 0.0005;

    private readonly double[] _heights = new double[BarCount];
    private double _envelope;
    private double _phase;

    internal double GetHeight(int index) => _heights[index];

    internal void Reset()
    {
        _envelope = 0;
        _phase = 0;
        Array.Clear(_heights);
    }

    internal void Advance(double seconds, float amplitude)
    {
        if (!double.IsFinite(seconds) || seconds <= 0)
            return;

        // A log response gives quiet speech room to move without a discontinuous
        // fallback amplitude. Silence and invalid samples always target rest.
        var level = float.IsFinite(amplitude)
            ? Math.Clamp(amplitude, MinimumAmplitude, MaximumAmplitude)
            : MinimumAmplitude;
        var target = Math.Clamp(
            Math.Log(1 + Math.Max(MinimumAmplitude, level - SilenceThreshold) * ResponseMultiplier) /
            Math.Log(ResponseLogarithmBase),
            MinimumAmplitude,
            MaximumAmplitude);
        // Integrate in small steps so dropped UI frames do not change the response.
        var remaining = Math.Min(seconds, MaximumElapsedSeconds);
        while (remaining > MinimumAmplitude)
        {
            var dt = Math.Min(remaining, IntegrationStepSeconds);
            remaining -= dt;
            var timeConstant = target > _envelope ? AttackTimeConstantSeconds : ReleaseTimeConstantSeconds;
            _envelope += (target - _envelope) * (1 - Math.Exp(-dt / timeConstant));
            _phase += dt * (MinimumPhaseSpeed + AdditionalPhaseSpeed * _envelope);

            for (var i = 0; i < BarCount; i++)
            {
                var x = (i - CenterBarIndex) / CenterBarIndex;
                var taper = EdgeTaper + CenterTaper * Math.Pow(Math.Cos(x * HalfTurnRadians), TaperExponent);
                // Positive, smooth lobes avoid the sharp cusps of Abs(sin).
                var wave = WaveBaseline
                           + PrimaryWaveWeight * Math.Sin(_phase * PrimaryWavePhaseSpeed - x * PrimaryWaveBarSpacing)
                           + SecondaryWaveWeight * Math.Sin(_phase * SecondaryWavePhaseSpeed + x * SecondaryWaveBarSpacing);
                var height = _envelope * taper * wave;
                _heights[i] += (height - _heights[i]) * (1 - Math.Exp(-dt / BarSmoothingTimeConstantSeconds));
                if (target == MinimumAmplitude && _heights[i] < RestThreshold)
                    _heights[i] = MinimumAmplitude;
            }
            if (target == MinimumAmplitude && _envelope < RestThreshold)
                _envelope = MinimumAmplitude;
        }
    }
}
