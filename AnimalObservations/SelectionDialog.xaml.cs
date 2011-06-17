using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AnimalObservations
{
    public partial class SelectionDialog : Window
    {
        public SelectionAction Action { get; set; }

        public SelectionDialog()
        {
            InitializeComponent();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            Action = SelectionAction.Edit;
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Action = SelectionAction.Delete;
            Close();
        }

    }

    public enum SelectionAction
    {
        Nothing,
        Delete,
        Edit
    }

}
