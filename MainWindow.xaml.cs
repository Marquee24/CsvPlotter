using CsvPlotter.Models;
using CsvPlotter.Services;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
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


        private readonly List<int> _loadedColumnIndexes =
            new List<int>();

        private readonly HashSet<int> _visibleColumnIndexes =
            new HashSet<int>();

        private readonly List<ParameterInfo> _parameters =
            new List<ParameterInfo>();

        private readonly Dictionary<int, string> _csvColumnNames =
            new Dictionary<int, string>();

        private enum ChartType
        {
            Superimposed,
            Stacked
        }

        private ChartType _currentChartType =
            ChartType.Superimposed;

        private readonly List<WpfPlot> _stackedPlots =
            new List<WpfPlot>();

        private VerticalLine? verticalCursorLine;

        private readonly List<VerticalLine> _stackedCursorLines =
            new List<VerticalLine>();

        public static bool NormTime = true;

        private DateTime _lastMouseMoveTime =
            DateTime.MinValue;

        private const int MouseMoveIntervalMs = 30;

        private readonly ScottPlot.Color[] _plotColors =
        {
        ScottPlot.Colors.Blue,
        ScottPlot.Colors.Red,
        ScottPlot.Colors.Green,
        ScottPlot.Colors.Orange,
        ScottPlot.Colors.Purple,
        ScottPlot.Colors.Brown,
        ScottPlot.Colors.Magenta,
        ScottPlot.Colors.Cyan,
        ScottPlot.Colors.DarkBlue,
        ScottPlot.Colors.DarkRed
    };

        public MainWindow()
        {
            InitializeComponent();

            PlotControl.MouseMove +=
                PlotControl_MouseMove;

            PlotControl.MouseLeave +=
                PlotControl_MouseLeave;
        }

        // =========================================================
        // BROWSE CSV
        // =========================================================

        private void BrowseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Filter =
                        "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
                };

            if (dialog.ShowDialog() != true)
                return;

            FilePathTextBox.Text = dialog.FileName;

            try
            {
                var allLines =
                    File.ReadLines(dialog.FileName);

                int lineCount =
                    allLines.Count();

                MaxRowsTextBox.Text =
                    lineCount.ToString();

                MinRowsTextBox.Text = "0";

                string firstLine =
                    allLines.FirstOrDefault();

                if (string.IsNullOrWhiteSpace(firstLine))
                {
                    MessageBox.Show(
                        "CSV file is empty.",
                        "CSV",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                string[] headers =
                    firstLine.Split(',');

                int columnCount =
                    headers.Length;

                if (columnCount <= 1)
                {
                    MessageBox.Show(
                        "No parameter columns found.",
                        "CSV",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                _csvColumnNames.Clear();

                for (int i = 0; i < headers.Length; i++)
                {
                    _csvColumnNames[i] =
                        headers[i].Trim().Trim('"');
                }

                _loadedColumnIndexes.Clear();

                for (int i = 0; i < columnCount; i++)
                {
                    _loadedColumnIndexes.Add(i);
                }

                _visibleColumnIndexes.Clear();
                _parameters.Clear();

                CreateParameterPanel();

                csvData = null;

                ClearAllPlots();

                CursorTimeTextBox.Text = "--";
                CursorRecordTextBox.Text = "--";

                StatusText.Text =
                    $"CSV selected: {lineCount:N0} rows, " +
                    $"{columnCount - 1} parameters";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Browse Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        private async Task LoadCsvData()
        {

            if (string.IsNullOrWhiteSpace(
                FilePathTextBox.Text))
            {
                MessageBox.Show(
                    "Please select a CSV file.");

                return;
            }

            if (_loadedColumnIndexes.Count == 0)
            {
                MessageBox.Show(
                    "Please select a CSV file first.");

                return;
            }

            int maxRows = 2000;

            if (!int.TryParse(
                MaxRowsTextBox.Text,
                out maxRows))
            {
                maxRows = 2000;
            }

            int minRows = 0;

            if (!int.TryParse(
                MinRowsTextBox.Text,
                out minRows))
            {
                minRows = 0;
            }

            int resolution = 10000;

            if (!int.TryParse(
                ResolutionTextBox.Text,
                out resolution))
            {
                resolution = 10000;
            }

            if (resolution <= 0)
                resolution = 1000;

            if (minRows < 0)
                minRows = 0;

            if (maxRows < minRows)
            {
                MessageBox.Show(
                    "Max row must be greater than or equal to Min row.",
                    "Invalid Range",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            StatusText.Text =
                "Loading CSV...";

            LoadButton.IsEnabled = false;
            BrowseButton.IsEnabled = false;

            try
            {
                string filePath =
                    FilePathTextBox.Text;

                int[] selectedColumns =
                    _loadedColumnIndexes.ToArray();

                Stopwatch stopwatch =
                    Stopwatch.StartNew();

                CsvPlotData data =
                    await Task.Run(() =>
                        CsvDataReader.Read(
                            filePath,
                            selectedColumns,
                            maxRows,
                            minRows,
                            resolution));

                stopwatch.Stop();

                csvData = data;



                ClearAllPlots();

                CursorTimeTextBox.Text = "--";
                CursorRecordTextBox.Text = "--";

                CreatePlots();

                StatusText.Text =
                    $"Loaded {csvData.RowCount:N0} plot points " +
                    $"in {stopwatch.ElapsedMilliseconds:N0} ms";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error loading CSV:\n\n" +
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text =
                    "Loading failed.";
            }
            finally
            {
                LoadButton.IsEnabled = true;
                BrowseButton.IsEnabled = true;
            }
        }

        private async void LoadButton_Click(
    object sender,
    RoutedEventArgs e)
        {
            await LoadCsvData();
        }



        private async void ResetRangeButton_Click(
            object sender,
            RoutedEventArgs e)
        {

            MinRowsTextBox.Text = "0";

            if (_csvColumnNames.Count > 0 &&
                !string.IsNullOrWhiteSpace(FilePathTextBox.Text))
            {
                try
                {
                    int totalRows =
                        File.ReadLines(FilePathTextBox.Text).Count() - 1;

                    if (totalRows > 0)
                    {
                        MaxRowsTextBox.Text =
                            totalRows.ToString();
                    }
                    else
                    {
                        MaxRowsTextBox.Text = "2000";
                    }
                }
                catch
                {
                    MaxRowsTextBox.Text = "2000";
                }
            }
            else
            {
                MaxRowsTextBox.Text = "2000";
            }


            await LoadCsvData();

            StatusText.Text =
                "Range reset and full CSV range loaded.";
        }




        private void CreateParameterPanel()
        {
            ParameterPanel.Children.Clear();
            FilterStack.Children.Clear();
            _parameters.Clear();

            foreach (int columnIndex
                     in _loadedColumnIndexes)
            {
                if (columnIndex == 0)
                    continue;

                string parameterName =
                    GetColumnName(columnIndex);

                Grid row = new Grid();

                row.Margin =
                    new Thickness(0, 2, 0, 2);

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = GridLength.Auto
                    });

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(22)
                    });

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                1,
                                GridUnitType.Star)
                    });

                row.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(100)
                    });

                CheckBox parameterCheckBox =
                    new CheckBox();

                parameterCheckBox.VerticalAlignment =
                    System.Windows.VerticalAlignment.Center;

                parameterCheckBox.IsChecked = false;

                Grid.SetColumn(
                    parameterCheckBox,
                    0);

                Border colorBorder =
                    new Border();

                colorBorder.Width = 14;
                colorBorder.Height = 14;

                colorBorder.Margin =
                    new Thickness(3, 0, 5, 0);

                colorBorder.CornerRadius =
                    new CornerRadius(2);

                colorBorder.Background =
                    new SolidColorBrush(
                        ConvertColor(
                            GetPlotColor(
                                _parameters.Count)));

                Grid.SetColumn(
                    colorBorder,
                    1);

                TextBlock nameText =
                    new TextBlock();

                nameText.Text =
                    parameterName;

                nameText.VerticalAlignment =
                    System.Windows.VerticalAlignment.Center;

                nameText.TextTrimming =
                    TextTrimming.CharacterEllipsis;

                nameText.ToolTip =
                    parameterName;

                Grid.SetColumn(
                    nameText,
                    2);

                TextBox valueTextBox =
                    new TextBox();

                valueTextBox.Text = "--";
                valueTextBox.Height = 26;
                valueTextBox.IsReadOnly = true;

                valueTextBox.VerticalContentAlignment =
                    System.Windows.VerticalAlignment.Center;

                valueTextBox.Margin =
                    new Thickness(5, 0, 0, 0);

                Grid.SetColumn(
                    valueTextBox,
                    3);

                row.Children.Add(parameterCheckBox);
                row.Children.Add(colorBorder);
                row.Children.Add(nameText);
                row.Children.Add(valueTextBox);

                row.Visibility =
                    Visibility.Collapsed;

                ParameterInfo parameter =
                    new ParameterInfo
                    {
                        ColumnIndex = columnIndex,
                        Name = parameterName,
                        CheckBox = parameterCheckBox,
                        ValueTextBox = valueTextBox,

                        UiColor =
                            new SolidColorBrush(
                                ConvertColor(
                                    GetPlotColor(
                                        _parameters.Count))),

                        ParameterRow = row,
                        Plot = null
                    };

                parameterCheckBox.Checked +=
                    ParameterCheckBox_Changed;

                parameterCheckBox.Unchecked +=
                    ParameterCheckBox_Changed;

                _parameters.Add(parameter);

                ParameterPanel.Children.Add(row);

                CheckBox filterCheckBox =
                    new CheckBox();

                filterCheckBox.Content =
                    parameterName;

                filterCheckBox.Margin =
                    new Thickness(2, 3, 2, 3);

                filterCheckBox.IsChecked = false;

                parameter.FilterCheckBox =
                    filterCheckBox;

                filterCheckBox.Checked +=
                    FilterParameterCheckBox_Changed;

                filterCheckBox.Unchecked +=
                    FilterParameterCheckBox_Changed;

                FilterStack.Children.Add(
                    filterCheckBox);
            }
        }

        private string GetColumnName(
            int columnIndex)
        {
            if (_csvColumnNames.ContainsKey(
                columnIndex))
            {
                return _csvColumnNames[
                    columnIndex];
            }

            if (csvData != null &&
                csvData.ColumnNames.ContainsKey(
                    columnIndex))
            {
                return csvData.ColumnNames[
                    columnIndex];
            }

            return $"Parameter {columnIndex}";
        }

        // =========================================================
        // FILTER
        // =========================================================

        private void Filter_Click(
            object sender,
            RoutedEventArgs e)
        {
            FilterPopUp.IsOpen =
                !FilterPopUp.IsOpen;

            if (FilterPopUp.IsOpen)
            {
                FilterSearchTextBox.Focus();
            }
        }

        private void FilterSearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            UpdateFilterList();
        }

        private void UpdateFilterList()
        {
            if (FilterStack == null)
                return;

            string searchText =
                FilterSearchTextBox.Text
                .Trim()
                .ToLower();

            FilterStack.Children.Clear();

            foreach (ParameterInfo parameter
                     in _parameters)
            {
                if (parameter.FilterCheckBox == null)
                    continue;

                bool matches =
                    string.IsNullOrEmpty(searchText) ||
                    parameter.Name
                    .ToLower()
                    .Contains(searchText);

                if (matches)
                {
                    FilterStack.Children.Add(
                        parameter.FilterCheckBox);
                }
            }
        }

        private void FilterParameterCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not CheckBox filterCheckBox)
                return;

            ParameterInfo? parameter =
                _parameters.FirstOrDefault(
                    p =>
                        p.FilterCheckBox ==
                        filterCheckBox);

            if (parameter == null)
                return;

            if (filterCheckBox.IsChecked == true)
            {
                if (parameter.ParameterRow != null)
                {
                    parameter.ParameterRow.Visibility =
                        Visibility.Visible;
                }

                int idx = 0;
                for (int i = 0; i < _parameters.Count; i++)
                {
                    if (_parameters[i].ParameterRow.Visibility == Visibility.Visible)
                    {
                        _parameters[i].UiColor = new SolidColorBrush(ConvertColor(GetPlotColor(idx)));
                        foreach (var element in _parameters[i].ParameterRow.Children)
                        {
                            if (element is Border border)
                            {
                                border.Background = new SolidColorBrush(ConvertColor(GetPlotColor(idx)));
                            }
                        }
                        idx++;
                    }
                }
                if (parameter.CheckBox != null)
                {
                    parameter.CheckBox.IsChecked = true;
                }
            }
            else
            {
                if (parameter.ParameterRow != null)
                {
                    parameter.ParameterRow.Visibility =
                        Visibility.Collapsed;
                }

                if (parameter.CheckBox != null)
                {
                    parameter.CheckBox.IsChecked = false;
                }
            }
        }

        private void ParameterCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            ParameterInfo? parameter =
                _parameters.FirstOrDefault(
                    p =>
                        p.CheckBox ==
                        checkBox);

            if (parameter == null)
                return;

            if (checkBox.IsChecked == true)
            {
                _visibleColumnIndexes.Add(
                    parameter.ColumnIndex);
            }
            else
            {
                _visibleColumnIndexes.Remove(
                    parameter.ColumnIndex);
            }

            CreatePlots();
        }

        // =========================================================
        // CHART TYPE
        // =========================================================

        private void ChartTypeComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ChartTypeComboBox == null)
                return;

            if (ChartTypeComboBox.SelectedIndex == 0)
            {
                _currentChartType =
                    ChartType.Superimposed;
            }
            else
            {
                _currentChartType =
                    ChartType.Stacked;
            }

            CreatePlots();
        }

        // =========================================================
        // CREATE PLOTS
        // =========================================================

        private void CreatePlots()
        {
            if (csvData == null)
            {
                ClearAllPlots();
                return;
            }

            ClearAllPlots();

            if (_currentChartType ==
                ChartType.Superimposed)
            {
                PlotControl.Visibility =
                    Visibility.Visible;

                StackedPlotScrollViewer.Visibility =
                    Visibility.Collapsed;

                CreateSuperimposedPlot();
            }
            else
            {
                PlotControl.Visibility =
                    Visibility.Collapsed;

                StackedPlotScrollViewer.Visibility =
                    Visibility.Visible;

                CreateStackedPlots();
            }
        }

        private void ClearAllPlots()
        {
            if (PlotControl != null)
            {
                PlotControl.Plot.Clear();

                verticalCursorLine = null;

                PlotControl.Refresh();
            }

            if (StackedPlotPanel != null)
            {
                StackedPlotPanel.Children.Clear();
            }

            _stackedPlots.Clear();
            _stackedCursorLines.Clear();

            foreach (ParameterInfo parameter
                     in _parameters)
            {
                parameter.Plot = null;
            }
        }

        // =========================================================
        // SUPERIMPOSED PLOT
        // =========================================================

        private void CreateSuperimposedPlot()
        {
            if (csvData == null)
                return;

            PlotControl.Plot.Clear();

            verticalCursorLine =
                PlotControl.Plot.Add.VerticalLine(0);

            verticalCursorLine.LineWidth = 1;

            verticalCursorLine.Color =
                ScottPlot.Colors.DarkRed;

            foreach (ParameterInfo parameter
                     in _parameters)
            {
                parameter.Plot = null;

                if (!_visibleColumnIndexes.Contains(
                    parameter.ColumnIndex))
                {
                    continue;
                }

                double[]? values =
                    GetParameterValues(
                        parameter.ColumnIndex);

                if (values == null ||
                    values.Length == 0)
                {
                    continue;
                }

                var scatter =
                    PlotControl.Plot.Add.Scatter(
                        csvData.TimeMilliseconds,
                        values);

                int parameterIndex =
                    _parameters.IndexOf(parameter);

                if (parameter.UiColor is SolidColorBrush)
                {
                    // Convert to ScottPlot Color (ARGB)
                    var colour = ((SolidColorBrush)(parameter.UiColor));
                    scatter.Color = new ScottPlot.Color(colour.Color.R, colour.Color.G, colour.Color.B, colour.Color.A);
                }
                else
                {
                    //scatter.Color = GetPlotColor(parameterIndex);
                }
                scatter.LineWidth = 1;
                scatter.MarkerSize = 1;
                parameter.Plot =
                    scatter;
            }

            PlotControl.Plot.Axes.AutoScale();

            PlotControl.Refresh();
        }

        // =========================================================
        // STACKED PLOTS
        // =========================================================

        private void CreateStackedPlots()
        {
            if (csvData == null)
                return;

            StackedPlotPanel.Children.Clear();

            _stackedPlots.Clear();
            _stackedCursorLines.Clear();

            foreach (ParameterInfo parameter
                     in _parameters)
            {
                parameter.Plot = null;

                if (!_visibleColumnIndexes.Contains(
                    parameter.ColumnIndex))
                {
                    continue;
                }

                double[]? values = null;
                if (csvData.ParameterValuesDisplay.ContainsKey(
                    parameter.ColumnIndex))
                {
                    values = csvData.ParameterValuesDisplay[
                        parameter.ColumnIndex];
                }

                if (values == null ||
                    values.Length == 0)
                {
                    continue;
                }

                Border border =
                    new Border();

                border.BorderBrush =
                    Brushes.LightGray;

                border.BorderThickness =
                    new Thickness(1);

                border.Margin =
                    new Thickness(0, 0, 0, 5);

                Grid grid =
                    new Grid();

                grid.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height =
                            GridLength.Auto
                    });

                grid.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height =
                            new GridLength(220)
                    });

                TextBlock title =
                    new TextBlock();

                title.Text =
                    parameter.Name;

                title.FontWeight =
                    FontWeights.Bold;

                title.Margin =
                    new Thickness(5, 3, 5, 3);

                Grid.SetRow(title, 0);

                grid.Children.Add(title);

                WpfPlot plot = new WpfPlot();

                Grid.SetRow(plot, 1);

                grid.Children.Add(plot);

                border.Child =
                    grid;

                StackedPlotPanel.Children.Add(
                    border);
                plot.MouseWheel += (s, e) =>
                {
                    e.Handled = true;
                };
                _stackedPlots.Add(plot);

                var scatter =
                    plot.Plot.Add.Scatter(
                        csvData.TimeMilliseconds,
                        values);

                int parameterIndex =
                    _parameters.IndexOf(parameter);

                if (parameter.UiColor is System.Windows.Media.SolidColorBrush solidBrush)
                {
                    // Convert to ScottPlot Color (ARGB)
                    scatter.Color = new ScottPlot.Color(solidBrush.Color.R, solidBrush.Color.G, solidBrush.Color.B, solidBrush.Color.A);
                }
                else
                {
                    //scatter.Color = GetPlotColor(parameterIndex);
                }
                scatter.LineWidth = 1;
                scatter.MarkerSize = 1;

                VerticalLine cursorLine =
                    plot.Plot.Add.VerticalLine(0);

                cursorLine.LineWidth = 1;

                cursorLine.Color =
                    ScottPlot.Colors.DarkRed;

                _stackedCursorLines.Add(
                    cursorLine);

                plot.MouseMove +=
                    StackedPlot_MouseMove;

                plot.MouseLeave +=
                    StackedPlot_MouseLeave;

                plot.MouseDown +=
                    StackedPlot_MouseDown;

                plot.Plot.Axes.AutoScale();

                plot.Refresh();

                parameter.Plot =
                    scatter;
            }
        }


        private void StackedPlotScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;

            while (source != null)
            {
                if (source is WpfPlot)
                {
                    // Allow ScottPlot to process the mouse wheel for zooming/panning
                    // BUT mark handled so the parent ScrollViewer does not scroll
                    e.Handled = false; // let WpfPlot receive it
                    return;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            // Block the mouse wheel from scrolling the outer ScrollViewer on all non-plot areas
            //e.Handled = true;
        }



        private ScottPlot.Color GetPlotColor(
            int index)
        {
            return _plotColors[
                index % _plotColors.Length];
        }

        private System.Windows.Media.Color ConvertColor(
            ScottPlot.Color color)
        {
            return System.Windows.Media.Color.FromArgb(
                color.A,
                color.R,
                color.G,
                color.B);
        }

        private void PlotControl_MouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (_currentChartType !=
                ChartType.Superimposed)
                return;

            if (csvData == null)
                return;

            if (csvData.TimeMilliseconds.Length == 0)
                return;

            DateTime now =
                DateTime.Now;

            if ((now - _lastMouseMoveTime)
                .TotalMilliseconds <
                MouseMoveIntervalMs)
            {
                return;
            }

            _lastMouseMoveTime = now;

            Point position =
                e.GetPosition(PlotControl);

            try
            {
                double scaledX =
                    position.X *
                    PlotControl.DisplayScale;

                double scaledY =
                    position.Y *
                    PlotControl.DisplayScale;

                var coordinates =
                    PlotControl.Plot.GetCoordinates(
                        new Pixel(
                            scaledX,
                            scaledY));

                double mouseTime =
                    coordinates.X;

                int index =
                    FindNearestTimeIndex(
                        csvData.TimeMilliseconds,
                        mouseTime);

                if (index < 0 ||
                    index >=
                    csvData.TimeMilliseconds.Length)
                {
                    return;
                }

                if (verticalCursorLine != null)
                {
                    verticalCursorLine.X =
                        mouseTime;
                }

                UpdateCursorInformation(index);

                PlotControl.Refresh();
            }
            catch
            {
            }
        }


        private void StackedPlot_MouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (_currentChartType !=
                ChartType.Stacked)
                return;

            if (csvData == null)
                return;

            if (csvData.TimeMilliseconds.Length == 0)
                return;

            if (sender is not WpfPlot currentPlot)
                return;

            DateTime now =
                DateTime.Now;

            if ((now - _lastMouseMoveTime)
                .TotalMilliseconds <
                MouseMoveIntervalMs)
            {
                return;
            }

            _lastMouseMoveTime = now;

            Point position =
                e.GetPosition(currentPlot);

            try
            {
                double scaledX =
                    position.X *
                    currentPlot.DisplayScale;

                double scaledY =
                    position.Y *
                    currentPlot.DisplayScale;

                var coordinates =
                    currentPlot.Plot.GetCoordinates(
                        new Pixel(
                            scaledX,
                            scaledY));

                double mouseTime =
                    coordinates.X;

                int index =
                    FindNearestTimeIndex(
                        csvData.TimeMilliseconds,
                        mouseTime);

                if (index < 0 ||
                    index >=
                    csvData.TimeMilliseconds.Length)
                {
                    return;
                }

                foreach (VerticalLine cursorLine
                         in _stackedCursorLines)
                {
                    cursorLine.X =
                        mouseTime;
                }

                UpdateCursorInformation(index);

                foreach (WpfPlot plot
                         in _stackedPlots)
                {
                    plot.Refresh();
                }
            }
            catch
            {
            }
        }

        private void StackedPlot_MouseLeave(
            object sender,
            MouseEventArgs e)
        {
        }

        // =========================================================
        // LEFT / RIGHT CLICK RANGE SELECTION
        // =========================================================

        private void PlotControl_MouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (_currentChartType !=
                ChartType.Superimposed)
                return;

            SelectRangeFromMouse(
                PlotControl,
                e);
        }

        private void StackedPlot_MouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (_currentChartType !=
                ChartType.Stacked)
                return;

            if (sender is not WpfPlot plot)
                return;

            SelectRangeFromMouse(
                plot,
                e);
        }

        private void SelectRangeFromMouse(
            WpfPlot plot,
            MouseButtonEventArgs e)
        {
            if (csvData == null)
                return;

            if (csvData.TimeMilliseconds.Length == 0)
                return;

            if (e.ChangedButton != MouseButton.Left &&
                e.ChangedButton != MouseButton.Right)
            {
                return;
            }

            Point position =
                e.GetPosition(plot);

            try
            {
                double scaledX =
                    position.X *
                    plot.DisplayScale;

                double scaledY =
                    position.Y *
                    plot.DisplayScale;

                var coordinates =
                    plot.Plot.GetCoordinates(
                        new Pixel(
                            scaledX,
                            scaledY));

                int index =
                    FindNearestTimeIndex(
                        csvData.TimeMilliseconds,
                        coordinates.X);

                if (index < 0 ||
                    index >=
                    csvData.TimeMilliseconds.Length)
                {
                    return;
                }

                if (index >=
                    csvData.CurrentRow.Length)
                {
                    return;
                }

                string selectedRow =
                    csvData.CurrentRow[index];

                // LEFT CLICK = MIN ROW
                if (e.ChangedButton ==
                    MouseButton.Left)
                {
                    MinRowsTextBox.Text =
                        selectedRow;

                    StatusText.Text =
                        $"Min selected: row {selectedRow}";
                }

                // RIGHT CLICK = MAX ROW
                else if (e.ChangedButton ==
                         MouseButton.Right)
                {
                    MaxRowsTextBox.Text =
                        selectedRow;

                    StatusText.Text =
                        $"Max selected: row {selectedRow}";
                }

                // IMPORTANT:
                // We do NOT reload or replot here.
                // The user must click LOAD.
                e.Handled = true;
            }
            catch
            {
            }
        }

        // =========================================================
        // CURSOR INFORMATION
        // =========================================================

        private void UpdateCursorInformation(
            int index)
        {
            if (csvData == null)
                return;

            if (index < 0 ||
                index >=
                csvData.TimeMilliseconds.Length)
            {
                return;
            }

            double actualTime =
                csvData.TimeMilliseconds[index];

            if (index <
                csvData.OriginalTimeStrings.Length)
            {
                CursorTimeTextBox.Text =
                    csvData.OriginalTimeStrings[index];
            }
            else
            {
                CursorTimeTextBox.Text =
                    actualTime.ToString("0.###");
            }

            // Show the exact CurrentRow value.
            // This is the same value used by
            // left/right mouse click.
            if (index <
                csvData.CurrentRow.Length)
            {
                CursorRecordTextBox.Text =
                    csvData.CurrentRow[index];
            }
            else
            {
                CursorRecordTextBox.Text =
                    index.ToString();
            }

            foreach (ParameterInfo parameter
                     in _parameters)
            {
                if (parameter.ValueTextBox == null)
                    continue;

                double[]? values =
                    GetParameterValues(
                        parameter.ColumnIndex);

                if (values == null)
                {
                    parameter.ValueTextBox.Text =
                        "--";

                    continue;
                }

                if (index < values.Length)
                {
                    double value =
                        values[index];

                    if (double.IsNaN(value))
                    {
                        parameter.ValueTextBox.Text =
                            "NaN";
                    }
                    else
                    {
                        parameter.ValueTextBox.Text =
                            value.ToString("0.#####");
                    }
                }
                else
                {
                    parameter.ValueTextBox.Text =
                        "--";
                }
            }
        }

        private void PlotControl_MouseLeave(
            object sender,
            MouseEventArgs e)
        {
            if (verticalCursorLine != null)
            {
                verticalCursorLine.IsVisible = true;

                PlotControl.Refresh();
            }
        }

        // =========================================================
        // FIND NEAREST TIME INDEX
        // =========================================================

        private int FindNearestTimeIndex(
            double[] values,
            double target)
        {
            if (values == null ||
                values.Length == 0)
            {
                return -1;
            }

            if (target <= values[0])
                return 0;

            if (target >=
                values[values.Length - 1])
            {
                return values.Length - 1;
            }

            int low = 0;

            int high =
                values.Length - 1;

            while (low <= high)
            {
                int mid =
                    low +
                    (high - low) / 2;

                double value =
                    values[mid];

                if (value == target)
                    return mid;

                if (value < target)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            if (low >= values.Length)
                return values.Length - 1;

            if (high < 0)
                return 0;

            double differenceLow =
                Math.Abs(
                    values[low] -
                    target);

            double differenceHigh =
                Math.Abs(
                    values[high] -
                    target);

            if (differenceLow <
                differenceHigh)
            {
                return low;
            }

            return high;
        }

        // =========================================================
        // STANDARD VALUES
        // =========================================================

        private void StandardTime_Checked(
            object sender,
            RoutedEventArgs e)
        {
            NormTime = false;

            if (csvData != null)
                CreatePlots();
        }

        // =========================================================
        // NORMALISED VALUES
        // =========================================================

        private void NormalisedTime_Checked(
            object sender,
            RoutedEventArgs e)
        {
            NormTime = true;

            if (csvData != null)
                CreatePlots();
        }

        private double[]? GetParameterValues(
            int columnIndex)
        {
            if (csvData == null)
                return null;

            if (NormTime)
            {
                if (csvData.ParameterValues.ContainsKey(
                    columnIndex))
                {
                    return csvData.ParameterValues[
                        columnIndex];
                }
            }
            else
            {
                if (csvData.ParameterValuesDisplay.ContainsKey(
                    columnIndex))
                {
                    return csvData.ParameterValuesDisplay[
                        columnIndex];
                }
            }

            return null;
        }
    }


}
