using System.Windows.Controls;
using System.Windows.Media;
using ScottPlot.Plottables;

namespace CsvPlotter.Models
{
    public class ParameterInfo
    {
        public int ColumnIndex { get; set; }

        public string Name { get; set; } = string.Empty;

        public CheckBox? CheckBox { get; set; }

        public TextBox? ValueTextBox { get; set; }

        public Brush? UiColor { get; set; }

        public Scatter? Plot { get; set; }
    }
}