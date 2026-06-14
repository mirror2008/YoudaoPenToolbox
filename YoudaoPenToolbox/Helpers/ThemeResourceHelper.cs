using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YoudaoPenToolbox.Helpers
{
    public static class ThemeResourceHelper
    {
        public static void RefreshElement(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            if (root is FrameworkElement element)
            {
                if (element is Control controlElement)
                {
                    controlElement.InvalidateProperty(Control.BackgroundProperty);
                    controlElement.InvalidateProperty(Control.ForegroundProperty);
                    controlElement.InvalidateProperty(Control.BorderBrushProperty);
                }

                if (element is Border border)
                {
                    border.InvalidateProperty(Border.BackgroundProperty);
                    border.InvalidateProperty(Border.BorderBrushProperty);
                }

                if (element is TextBlock textBlock)
                {
                    textBlock.InvalidateProperty(TextBlock.ForegroundProperty);
                }

                if (element is TextBox textBox)
                {
                    textBox.InvalidateProperty(TextBox.BackgroundProperty);
                    textBox.InvalidateProperty(TextBox.ForegroundProperty);
                    textBox.InvalidateProperty(TextBox.BorderBrushProperty);
                }

                if (element is ComboBox comboBox)
                {
                    comboBox.InvalidateProperty(ComboBox.BackgroundProperty);
                    comboBox.InvalidateProperty(ComboBox.ForegroundProperty);
                    comboBox.InvalidateProperty(ComboBox.BorderBrushProperty);
                }

                if (element is DataGrid dataGrid)
                {
                    dataGrid.InvalidateProperty(DataGrid.BackgroundProperty);
                    dataGrid.InvalidateProperty(DataGrid.ForegroundProperty);
                    dataGrid.InvalidateProperty(DataGrid.RowBackgroundProperty);
                    dataGrid.InvalidateProperty(DataGrid.AlternatingRowBackgroundProperty);
                    ReloadDataGridItems(dataGrid);
                }

                if (element is Window window)
                {
                    window.Background = Application.Current.FindResource("RegionBrush") as Brush;
                }

                element.InvalidateVisual();
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                RefreshElement(VisualTreeHelper.GetChild(root, i));
            }
        }

        public static void ReloadDataGridItems(DataGrid dataGrid)
        {
            if (dataGrid == null)
            {
                return;
            }

            var items = dataGrid.ItemsSource;
            if (items == null)
            {
                return;
            }

            dataGrid.ItemsSource = null;
            dataGrid.ItemsSource = items;

            if (dataGrid.SelectedItem != null)
            {
                var selected = dataGrid.SelectedItem;
                dataGrid.SelectedItem = null;
                dataGrid.SelectedItem = selected;
            }
        }

        public static void ReloadAllDataGrids(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            if (root is DataGrid dataGrid)
            {
                ReloadDataGridItems(dataGrid);
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                ReloadAllDataGrids(VisualTreeHelper.GetChild(root, i));
            }
        }
    }
}
