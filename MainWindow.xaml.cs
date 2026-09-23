using CsvPlotter.Models;
using CsvPlotter.Services;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottables;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CsvPlotter
{
    public partial class MainWindow : Window
    {
        private CsvPlotData? csvData;

        private readonly List<ParameterInfo> _parameters = new List<ParameterInfo>();

        private readonly List<int> _selectedColumnIndexes = new List<int> { 1, 44, 45, 69 };

        private VerticalLine? verticalCursorLine;


        private readonly Stopwatch _mouseTimer = Stopwatch.StartNew();

        private const int MouseUpdateMilliseconds = 30;
        public MainWindow()
        {
            InitializeComponent();

            SetupPlot();

            PlotControl.MouseMove += PlotControl_MouseMove;

            PlotControl.MouseLeave += PlotControl_MouseLeave;
        }


        private void SetupPlot()
        {
            PlotControl.Plot.Title("Parameter Plot");

            PlotControl.Plot.XLabel("Time");

            PlotControl.Plot.YLabel("Data");


            verticalCursorLine = PlotControl.Plot.Add.VerticalLine(1);

            verticalCursorLine.IsVisible = true;
            verticalCursorLine.Color = ScottPlot.Colors.DarkRed;

            verticalCursorLine.LineWidth = 1;


            PlotControl.Refresh();
        }


        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Filter =
                        "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
                };


            if (dialog.ShowDialog() == true)
            {
                FilePathTextBox.Text =
                    dialog.FileName;
            }
            var allLines = File.ReadLines(FilePathTextBox.Text);
            int lineCount = allLines.Count();
            MaxRowsTextBox.Text = lineCount.ToString();
            MinRowsTextBox.Text = 0.ToString();
            int coloumnsCount = allLines.ToArray()[0].Split(',').Length;
            _selectedColumnIndexes.Clear();
            for (int i = 1; i < coloumnsCount; i++)
            {
                _selectedColumnIndexes.Add(i);
            }
        }


        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FilePathTextBox.Text))
            {
                MessageBox.Show("Please select a CSV file.");

                return;
            }


            if (_selectedColumnIndexes.Count == 0)
            {
                MessageBox.Show("Please select at least one column.");

                return;
            }


            int maxRows = 2000;
            if (!int.TryParse(MaxRowsTextBox.Text, out maxRows))
            {
                maxRows = 2000;
            }

            int minRows = 0;
            if (!int.TryParse(MinRowsTextBox.Text, out minRows))
            {
                minRows = 0;
            }

            int Resolution = 1000;
            if (!int.TryParse(ResolutionTextBox.Text, out Resolution))
            {
                Resolution = 1000;
            }


            StatusText.Text = "Loading CSV...";


            LoadButton.IsEnabled = false;
            BrowseButton.IsEnabled = false;


            try
            {
                string filePath = FilePathTextBox.Text;


                int[] selectedColumns = _selectedColumnIndexes.ToArray();


                Stopwatch stopwatch = Stopwatch.StartNew();


                CsvPlotData data = await Task.Run(() => CsvDataReader.Read(filePath, selectedColumns, maxRows, minRows, Resolution));


                stopwatch.Stop();
                csvData = data;

                CreatePlots();

                CreateParameterPanel();


                StatusText.Text = $"Loaded {csvData.RowCount:N0} rows in " + $"{stopwatch.ElapsedMilliseconds:N0} ms";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading CSV:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);


                StatusText.Text = "Loading failed.";
            }
            finally
            {
                LoadButton.IsEnabled = true;
                BrowseButton.IsEnabled = true;
            }
        }

        private void CreatePlots()
        {
            PlotControl.Plot.Clear();

            _parameters.Clear();


            if (csvData == null || csvData.RowCount == 0)
            {
                PlotControl.Refresh();
                return;
            }


            ScottPlot.Color[] colors =
            {
                ScottPlot.Colors.Blue,
                ScottPlot.Colors.Red,
                ScottPlot.Colors.Green,
                ScottPlot.Colors.Orange,
                ScottPlot.Colors.Purple,
                ScottPlot.Colors.Cyan,
                ScottPlot.Colors.Magenta,
                ScottPlot.Colors.Brown,
                ScottPlot.Colors.DarkBlue,
                ScottPlot.Colors.DarkGreen
            };


            int colorIndex = 0;


            foreach (int columnIndex in _selectedColumnIndexes)
            {
                if (columnIndex == 0)
                    continue;


                if (!csvData.ParameterValues.ContainsKey(columnIndex))
                {
                    continue;
                }


                string parameterName = csvData.ColumnNames.ContainsKey(columnIndex) ? csvData.ColumnNames[columnIndex] : $"Column {columnIndex}";


                ScottPlot.Color plotColor = colors[colorIndex % colors.Length];


                Brush uiColor = ConvertScottPlotColorToBrush(plotColor);


                double[] values = csvData.ParameterValues[columnIndex];


                Scatter scatter = PlotControl.Plot.Add.Scatter(csvData.TimeMilliseconds, values);

                scatter.Color = plotColor;


                scatter.LineWidth = 1;


                var parameter = new ParameterInfo
                {
                    ColumnIndex = columnIndex,

                    Name = parameterName,

                    UiColor = uiColor,

                    Plot = scatter
                };

                _parameters.Add(parameter);

                //parameter.Plot.IsVisible = false;
                colorIndex++;
            }


            PlotControl.Plot.Axes.AutoScale();

            PlotControl.Refresh();
        }


        private void CreateParameterPanel()
        {
            ParameterPanel.Children.Clear();


            foreach (ParameterInfo parameter in _parameters)
            {
                Grid row = new Grid();


                row.Margin = new Thickness(0, 0, 0, 6);


                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(30)
                });


                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });


                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(28)
                });


                row.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width =
                            new GridLength(100)
                });


                CheckBox checkBox = new CheckBox();


                checkBox.IsChecked = true;


                checkBox.VerticalAlignment = System.Windows.VerticalAlignment.Center;


                checkBox.Tag = parameter;


                checkBox.Checked += ParameterCheckBox_Changed;


                checkBox.Unchecked += ParameterCheckBox_Changed;


                TextBlock name = new TextBlock();


                name.Text = parameter.Name;


                name.VerticalAlignment = System.Windows.VerticalAlignment.Center;


                name.Margin = new Thickness(4, 0, 4, 0);


                name.TextWrapping = TextWrapping.Wrap;


                Border colorBox = new Border();


                colorBox.Width = 20;

                colorBox.Height = 20;


                colorBox.Background = parameter.UiColor;


                colorBox.BorderBrush = Brushes.Black;


                colorBox.BorderThickness = new Thickness(1);


                colorBox.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;


                colorBox.VerticalAlignment = System.Windows.VerticalAlignment.Center;


                TextBox valueBox = new TextBox();


                valueBox.Text = "--";


                valueBox.IsReadOnly = true;


                valueBox.Height = 25;


                valueBox.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;


                valueBox.Margin = new Thickness(3, 0, 0, 0);


                parameter.CheckBox = checkBox;


                parameter.ValueTextBox = valueBox;


                Grid.SetColumn(checkBox, 0);


                Grid.SetColumn(name, 1);


                Grid.SetColumn(colorBox, 2);


                Grid.SetColumn(valueBox, 3);
                checkBox.IsChecked = false;

                row.Children.Add(checkBox);

                row.Children.Add(name);

                row.Children.Add(colorBox);

                row.Children.Add(valueBox);


                ParameterPanel.Children.Add(row);
            }
        }


        private void ParameterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is ParameterInfo parameter && parameter.Plot != null)
            {
                parameter.Plot.IsVisible = checkBox.IsChecked == true;

                PlotControl.Refresh();
            }
        }


        private void PlotControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (csvData == null || csvData.RowCount == 0)
            {
                return;
            }


            if (_mouseTimer.ElapsedMilliseconds < MouseUpdateMilliseconds)
            {
                return;
            }


            _mouseTimer.Restart();


            Point mousePosition = e.GetPosition(PlotControl);


            Coordinates coordinates = PlotControl.Plot.GetCoordinates(
                    new Pixel(
                        (float)mousePosition.X,
                        (float)mousePosition.Y));


            double mouseTime = coordinates.X;


            int nearestIndex = FindNearestTimeIndex(
                    csvData.TimeMilliseconds,
                    mouseTime);


            if (nearestIndex < 0)
                return;


            double nearestTime = csvData.TimeMilliseconds[nearestIndex];


            if (verticalCursorLine != null)
            {
                verticalCursorLine.X = nearestTime;

                verticalCursorLine.IsVisible = true;
            }


            if (nearestIndex < csvData.OriginalTimeStrings.Length)
            {
                CursorTimeTextBox.Text = csvData.OriginalTimeStrings[nearestIndex];
                CursorRecordTextBox.Text = csvData.CurrentRow[nearestIndex];
            }


            foreach (ParameterInfo parameter in _parameters)
            {
                if (parameter.ValueTextBox == null)
                    continue;


                double[] values = csvData.ParameterValues[
                        parameter.ColumnIndex];


                if (nearestIndex >= values.Length)
                {
                    parameter.ValueTextBox.Text = "--";

                    continue;
                }


                double value = values[nearestIndex];


                if (double.IsNaN(value) ||
                    double.IsInfinity(value))
                {
                    parameter.ValueTextBox.Text = "--";
                }
                else
                {
                    parameter.ValueTextBox.Text =
                        value.ToString("G10");
                }
            }

            PlotControl.Refresh();
        }


        private void PlotControl_MouseLeave(object sender, MouseEventArgs e)
        {
            if (verticalCursorLine != null)
            {
                verticalCursorLine.IsVisible = true;

                PlotControl.Refresh();
            }
        }


        private static int FindNearestTimeIndex(double[] times, double target)
        {
            if (times.Length == 0)
                return -1;


            if (target <= times[0])
                return 0;


            if (target >= times[times.Length - 1])
            {
                return times.Length - 1;
            }


            int left = 0;

            int right = times.Length - 1;


            while (left <= right)
            {
                int middle = left + ((right - left) / 2);


                if (times[middle] == target)
                {
                    return middle;
                }


                if (times[middle] < target)
                {
                    left = middle + 1;
                }
                else
                {
                    right = middle - 1;
                }
            }


            int index1 = Math.Max(0, right);


            int index2 = Math.Min(times.Length - 1, left);


            double difference1 = Math.Abs(times[index1] - target);


            double difference2 = Math.Abs(times[index2] - target);


            return difference1 <= difference2
                ? index1
                : index2;
        }


        private static Brush ConvertScottPlotColorToBrush(ScottPlot.Color color)
        {
            return new SolidColorBrush(
                System.Windows.Media.Color.FromArgb(
                    color.Alpha,
                    color.Red,
                    color.Green,
                    color.Blue));
        }
    }
}