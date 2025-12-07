using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ISCSI_Util.Converters;


    // Converter para elegir la geometría (círculo)
    public class EstadoToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Siempre devolvemos el círculo definido en App.axaml
            return Application.Current?.Resources["CircleGeometry"] as Geometry;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Converter para el color de relleno
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool conectado = value is bool b && b;
            return conectado ? Brushes.Green : Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Converter para el grosor del borde
    public class BoolToStrokeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool conectado = value is bool b && b;
            return conectado ? 0 : 2; // sin borde si está conectado, borde visible si no
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
