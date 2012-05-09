using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ESRI.ArcGIS.Mobile.Client;
using Microsoft.Windows.Controls;

namespace AnimalObservations
{

    public partial class EditObservationAttributesPage
    {

        //Public properties so that they are visible in XAML
        public CollectTrackLogTask Task { get; private set; }

        #region Constructor

        public EditObservationAttributesPage()
        {

            InitializeData();

            InitializeComponent();

            Title = "Observation at " + Task.ActiveObservation.GpsPoint.LocalTime.ToLongTimeString();
            Note = "Edit the observation values";
            // page icon
            var uri = new Uri("pack://application:,,,/AnimalObservations;Component/duck-icon.png");
            ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);

            var newObservationCommand = new PageNavigationCommand(
                PageNavigationCommandType.Highlighted,
                "New Observation",
                param => NewObservationCommandExecute(),
                param => true  //We can always execute this command
            );

            OkCommand.CommandType = PageNavigationCommandType.Positive;
            OkCommand.Text = "Save";

            // back Buttons
            BackCommands.Clear();
            CancelCommand.Text = "Cancel";
            BackCommands.Add(CancelCommand);

            // forward Buttons
            ForwardCommands.Clear();
            ForwardCommands.Add(newObservationCommand);
            ForwardCommands.Add(OkCommand);

            //Setup desired keyboard behavior
            Focusable = true;
            Loaded += (s, e) => Keyboard.Focus(angleTextBox);

            Task.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "ActiveObservation" && Task.ActiveObservation != null)
                    Title = "Observation at " + Task.ActiveObservation.GpsPoint.LocalTime.ToLongTimeString();
            };

            angleTextBox.GotKeyboardFocus += (s, e) => angleTextBox.SelectAll();
            distanceTextBox.GotKeyboardFocus += (s, e) => distanceTextBox.SelectAll();

        }

        private void InitializeData()
        {
            Task = MobileApplication.Current.FindTask(typeof(CollectTrackLogTask)) as CollectTrackLogTask;
        }

        #endregion


        #region Page navigation overrides

        /// <summary>
        /// Discard this observation (if existing, abandon changes; if new, delete new (unsaved) feature)
        /// Transition to next in list, or if empty, previous page
        /// </summary>
        protected override void OnCancelCommandExecute()
        {
            dataGrid.CancelEdit();
            Task.RemoveObservation(Task.ActiveObservation);
            if (Task.ActiveObservation == null)
                MobileApplication.Current.Transition(PreviousPage);
        }

        /// <summary>
        /// Log observation point and add observation attribute page to the queue
        /// </summary>
        void NewObservationCommandExecute()
        {
            CommitEdit(dataGrid);

            //FromGpsPoint() may throw exceptions, but that would be catastrophic, so let the app handle it.
            Task.AddObservationAsActive(Observation.FromGpsPoint(Task.CurrentGpsPoint));
            Keyboard.Focus(angleTextBox);
        }

        private void CommitEdit(DataGrid dataGrid)
        {
            dataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            dataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        }

        protected override bool CanExecuteOkCommand()
        {
            //nullity check is required because this override is called after CancelCommand and OkCommand
            if (Task.ActiveObservation == null)
                return false;

            string errorMessage = "";
            // the Observation.Angle and Distance may be valid, but the UI value is not.
            // there will only be a validation errors if Observation.Angle and Distance are valid
            if (Validation.GetHasError(angleTextBox))
                errorMessage += "Angle must be an integer between 0 and 360, inclusive.\n";
            if (Validation.GetHasError(distanceTextBox))
                errorMessage += "Distance must be a positive integer less than 500.\n";
            Task.ActiveObservation.ValidateBeforeSave(); //Has a side effect of updating the ErrorMessage property
            errorMessage += Task.ActiveObservation.Error;
            errorLabel.Content = errorMessage; 
            return string.IsNullOrEmpty(errorMessage);
        }

        /// <summary>
        /// Save and close the current observation attribute page
        /// Transition to next in list, or if empty, previous page
        /// </summary>
        protected override void OnOkCommandExecute()
        {
            CommitEdit(dataGrid);
            //If saving throws an exception, it is catastrophic, so let the app handle it
            bool saved = Task.ActiveObservation.Save();
            if (saved)
            {
                Task.RemoveObservation(Task.ActiveObservation);
                if (Task.ActiveObservation == null)
                    MobileApplication.Current.Transition(PreviousPage);
                else
                    Keyboard.Focus(angleTextBox);
            }
            else
            {
                var msg = new StringBuilder();
                msg.Append(Task.ActiveObservation.Error + "\n");
                foreach (var birdGroup in Task.ActiveObservation.BirdGroups)
                    msg.Append(birdGroup.Error + "\n");
                string msg2 = msg.ToString();
                if (msg.Length == 0)
                    msg2 = "Problem is undefined.  Geometry may be out of bounds.";
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(msg2, "Save Failed",MessageBoxButton.OK,MessageBoxImage.Error);
            }
        }

        #endregion


        #region Keyboard event overrides

        protected override void OnKeyDown(KeyEventArgs e)
        {
            //Command keys - Save
            if (e.Key == Key.Enter ||
                (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.Control))
            {
                e.Handled = true;
                CommitEdit(dataGrid);
                Keyboard.Focus(this.dataGrid);  //Ensures that angle/distance, loose focus and commit edits.
                if (CanExecuteOkCommand())
                    OnOkCommandExecute();
                return;
            }
            //Command keys - Back
            if (e.Key == Key.Escape ||
                (e.Key == Key.W && e.KeyboardDevice.Modifiers == ModifierKeys.Control))
            {
                e.Handled = true;
                if (CanExecuteCancelCommand())
                    OnCancelCommandExecute();
                return;
            }
            //Command keys - New
            if ((e.Key == Key.Space && !dataGrid.IsKeyboardFocusWithin) ||
                 (e.Key == Key.N && e.KeyboardDevice.Modifiers == ModifierKeys.Control))
            {
                e.Handled = true;
                CommitEdit(dataGrid);
                Keyboard.Focus(this.dataGrid);  //Ensures that angle/distance, loose focus and commit edits.
                //Note if the edited value in the control is not valid, i.e. angle < 0, then the edits are discarded,
                //and the control reverts to the previous value.
                NewObservationCommandExecute();
                return;
            }

            base.OnKeyDown(e);
        }


        #endregion


        #region UI events

        private void dockPanel_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Keyboard.Focus(angleTextBox);
        }

        private void observationListView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            CommitEdit(dataGrid);
        }

        #endregion



        #region fast edit of data grid cells

        // Pushes combo boxes (and checkboxes) into edit mode on first click/keystroke, instead of standard two click
        // From http://stackoverflow.com/questions/3833536/how-to-perform-single-click-checkbox-selection-in-wpf-datagrid

        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            GridColumnFastEdit(cell, e);
        }

        private void DataGridCell_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            GridColumnFastEdit(cell, e);
        }


        private static void GridColumnFastEdit(DataGridCell cell, RoutedEventArgs e)
        {
            if (cell == null || cell.IsEditing || cell.IsReadOnly)
                return;

            DataGrid dataGrid = FindVisualParent<DataGrid>(cell);
            if (dataGrid == null)
                return;

            if (!cell.IsFocused)
            {
                cell.Focus();
            }

            if (cell.Content is CheckBox)
            {
                if (dataGrid.SelectionUnit != DataGridSelectionUnit.FullRow)
                {
                    if (!cell.IsSelected)
                        cell.IsSelected = true;
                }
                else
                {
                    DataGridRow row = FindVisualParent<DataGridRow>(cell);
                    if (row != null && !row.IsSelected)
                    {
                        row.IsSelected = true;
                    }
                }
            }
            else
            {
                ComboBox cb = cell.Content as ComboBox;
                if (cb != null)
                {
                    //DataGrid dataGrid = FindVisualParent<DataGrid>(cell);
                    dataGrid.BeginEdit(e);
                    cell.Dispatcher.Invoke(
                     DispatcherPriority.Background,
                     new Action(delegate { }));
                    cb.IsDropDownOpen = true;
                }
            }
        }


        private static T FindVisualParent<T>(UIElement element) where T : UIElement
        {
            UIElement parent = element;
            while (parent != null)
            {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null)
                {
                    return correctlyTyped;
                }

                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }
        #endregion
    }
}
