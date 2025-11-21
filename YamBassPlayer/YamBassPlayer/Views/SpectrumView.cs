using Terminal.Gui;

namespace YamBassPlayer.Views
{
    public sealed class SpectrumView : View
    {
        public int Bars { get; set; } = 25;
        public bool TestMode { get; set; } = true;

        private float[] _fft = new float[128];
        private readonly float[] _smoothed;
        private readonly float[] _peaks;
        private readonly float[] _peakFallSpeed;
        private readonly Random _rnd = new();

        public SpectrumView()
        {
            _smoothed = new float[Bars];
            _peaks = new float[Bars];
            _peakFallSpeed = new float[Bars];

            for (int i = 0; i < Bars; i++)
                _peakFallSpeed[i] = 0.05f + (float)_rnd.NextDouble() * 0.1f;

            CanFocus = false;
        }

        public void SetFftData(float[] fft)
        {
            if (fft.Length == 0)
                return;

            _fft = fft;
            TestMode = false;
            SetNeedsDisplay();
        }

        public override void Redraw(Rect bounds)
        {
            base.Redraw(bounds);

            var driver = Application.Driver;
            int height = bounds.Height;
            int width = Math.Min(Bars, bounds.Width);

            int fftStep = _fft.Length / width;

            for (int i = 0; i < width; i++)
            {
                float rawValue;

                if (TestMode)
                {
                    rawValue = 1f;
                }
                else
                {
                    rawValue = 0f;
                    int start = i * fftStep;
                    int end = start + fftStep;

                    for (int j = start; j < end; j++)
                        rawValue += _fft[j];

                    rawValue /= fftStep;

                    float k = ((float)Math.Log2(i + 1.3d)) * 10f;

                    rawValue *= k;
                }

                rawValue = Math.Clamp(rawValue, 0f, 1f);

                _smoothed[i] = _smoothed[i] * 0.7f + rawValue * 0.3f;

                float barHeight = _smoothed[i] * height;
                int barPixels = Math.Clamp((int)barHeight, 0, height);

                if (barHeight > _peaks[i])
                {
                    _peaks[i] = barHeight;
                }
                else
                {
                    _peaks[i] -= _peakFallSpeed[i];
                    if (_peaks[i] < 0)
                        _peaks[i] = 0;
                }

                for (int y = 0; y < barPixels; y++)
                {
                    Move(i, height - 1 - y);
                    driver.AddRune('█');
                }
                int peakY = height - 1 - (int)_peaks[i];

                if (peakY >= 0 && peakY < height)
                {
                    Move(i, peakY);
                    driver.AddRune('░');
                }
            }
        }
    }

}
