#nullable disable

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diffraction.WpfPrototype.Models
{
    public sealed class CalculationParameters : INotifyPropertyChanged
    {
        private double _wavelengthMicrometers = 1.0;
        private double _incidenceAngleDegrees = 45.0;
        private double _plateStart = -1.5;
        private double _plateEnd = -0.5;
        private int _harmonicCount = 30;
        private double _skinDepthMicrometers = 0.01;
        private double _outputLeft = -2.0;
        private double _outputRight = 2.0;
        private double _outputBottom = -3.0;
        private double _outputTop = 3.0;
        private double _seriesSkinDepthStart;
        private double _seriesSkinDepthEnd = 0.1;
        private int _seriesPointCount = 21;
        private double _seriesAngleStartDegrees = 10.0;
        private double _seriesAngleEndDegrees = 90.0;
        private double _seriesAngleStepDegrees = 2.0;

        public event PropertyChangedEventHandler PropertyChanged;

        public double WavelengthMicrometers
        {
            get { return _wavelengthMicrometers; }
            set { SetProperty(ref _wavelengthMicrometers, value); }
        }

        public double IncidenceAngleDegrees
        {
            get { return _incidenceAngleDegrees; }
            set { SetProperty(ref _incidenceAngleDegrees, value); }
        }

        public double PlateStart
        {
            get { return _plateStart; }
            set { SetProperty(ref _plateStart, value); }
        }

        public double PlateEnd
        {
            get { return _plateEnd; }
            set { SetProperty(ref _plateEnd, value); }
        }

        public int HarmonicCount
        {
            get { return _harmonicCount; }
            set { SetProperty(ref _harmonicCount, value); }
        }

        public double SkinDepthMicrometers
        {
            get { return _skinDepthMicrometers; }
            set { SetProperty(ref _skinDepthMicrometers, value); }
        }

        public double OutputLeft
        {
            get { return _outputLeft; }
            set { SetProperty(ref _outputLeft, value); }
        }

        public double OutputRight
        {
            get { return _outputRight; }
            set { SetProperty(ref _outputRight, value); }
        }

        public double OutputBottom
        {
            get { return _outputBottom; }
            set { SetProperty(ref _outputBottom, value); }
        }

        public double OutputTop
        {
            get { return _outputTop; }
            set { SetProperty(ref _outputTop, value); }
        }

        public double SeriesSkinDepthStart
        {
            get { return _seriesSkinDepthStart; }
            set { SetProperty(ref _seriesSkinDepthStart, value); }
        }

        public double SeriesSkinDepthEnd
        {
            get { return _seriesSkinDepthEnd; }
            set { SetProperty(ref _seriesSkinDepthEnd, value); }
        }

        public int SeriesPointCount
        {
            get { return _seriesPointCount; }
            set { SetProperty(ref _seriesPointCount, value); }
        }

        public double SeriesAngleStartDegrees
        {
            get { return _seriesAngleStartDegrees; }
            set { SetProperty(ref _seriesAngleStartDegrees, value); }
        }

        public double SeriesAngleEndDegrees
        {
            get { return _seriesAngleEndDegrees; }
            set { SetProperty(ref _seriesAngleEndDegrees, value); }
        }

        public double SeriesAngleStepDegrees
        {
            get { return _seriesAngleStepDegrees; }
            set { SetProperty(ref _seriesAngleStepDegrees, value); }
        }

        public static CalculationParameters CreateDefault()
        {
            return new CalculationParameters();
        }

        public CalculationParameters Clone()
        {
            var clone = new CalculationParameters();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(CalculationParameters source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            WavelengthMicrometers = source.WavelengthMicrometers;
            IncidenceAngleDegrees = source.IncidenceAngleDegrees;
            PlateStart = source.PlateStart;
            PlateEnd = source.PlateEnd;
            HarmonicCount = source.HarmonicCount;
            SkinDepthMicrometers = source.SkinDepthMicrometers;
            OutputLeft = source.OutputLeft;
            OutputRight = source.OutputRight;
            OutputBottom = source.OutputBottom;
            OutputTop = source.OutputTop;
            SeriesSkinDepthStart = source.SeriesSkinDepthStart;
            SeriesSkinDepthEnd = source.SeriesSkinDepthEnd;
            SeriesPointCount = source.SeriesPointCount;
            SeriesAngleStartDegrees = source.SeriesAngleStartDegrees;
            SeriesAngleEndDegrees = source.SeriesAngleEndDegrees;
            SeriesAngleStepDegrees = source.SeriesAngleStepDegrees;
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
