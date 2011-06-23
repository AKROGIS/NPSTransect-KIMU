using System.Windows;

namespace AnimalObservations
{
    public partial class SelectionDialog
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
