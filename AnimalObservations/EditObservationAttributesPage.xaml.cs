using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ESRI.ArcGIS.Mobile.Client;

namespace AnimalObservations
{

    public partial class EditObservationAttributesPage
    {

        //These are public properties so that they are visible in XAML
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

        //#region Defining Bird Groups

        //private BirdGroup2 _birdGroupInProgress = new BirdGroup2();

        //private void DefineBirdGroup(KeyEventArgs e)
        //{
        //    string keyString = (new KeyConverter()).ConvertToString(e.Key);
        //    if (string.IsNullOrEmpty(keyString))
        //        return;
        //    if (e.KeyboardDevice.Modifiers != ModifierKeys.None)
        //        return;
        //    Char keyChar = keyString[0];
        //    if (BirdGroup2.RecognizeKey(keyChar) && _birdGroupInProgress.AcceptKey(keyChar))
        //    {
        //        if (_birdGroupInProgress.IsComplete)
        //            CompleteBirdGroup();
        //    }
        //    else
        //    {
        //        if (_birdGroupInProgress.IsValid)
        //            CompleteBirdGroup();
        //        else
        //            _birdGroupInProgress.Reset();
        //    }
        //}

        //private void CompleteBirdGroup()
        //{
        //    Task.ActiveObservation.BirdGroups.Add(_birdGroupInProgress);
        //    _birdGroupInProgress = new BirdGroup2();
        //}

        //#endregion

        #region Page navigation overrides

        protected override void OnCancelCommandExecute()
        {
            //Discard this observation (if existing, abandon changes; if new, delete new (unsaved) feature)
            //Transition to next in list, or if empty, previous page

            Task.RemoveObservation(Task.ActiveObservation);
            if (Task.ActiveObservation == null)
                MobileApplication.Current.Transition(PreviousPage);
        }

        void NewObservationCommandExecute()
        {
            //Log observation point and add observation attribute page to the queue, do not change pages

            //CreateWith() may throw exceptions, but that would be catastrophic, so let the app handle it.
            //Only use one of the following based on prefered behavior
            Task.AddObservationAsActive(Observation.FromGpsPoint(Task.CurrentGpsPoint));
            //Task.AddObservationAsInactive(Observation.CreateWith(Task.CurrentGpsPoint));
            //If the new observation is the active observation, set focus on Angle Box; don't change focus if AddObservationAsInactive
            Keyboard.Focus(angleTextBox);
        }

        protected override bool CanExecuteOkCommand()
        {
            //nullity check is required because this override is called when the page is closing
            if (Task.ActiveObservation == null)
                return false;
            string errorMessage = "";
            // the Observation.Angle and Distance may be valid, but the UI value is not.
            // there will only be a validation errors if Observation.Angle and Distance are valid
            if (Validation.GetHasError(angleTextBox))
                errorMessage += "Angle must be an integer between 0 and 360, inclusive.\n";
            if (Validation.GetHasError(distanceTextBox))
                errorMessage += "Distance must be a positive integer less than 500.\n";
            errorMessage += Task.ActiveObservation.ValidateBeforeSave();
            errorLabel.Content = errorMessage; 
            return string.IsNullOrEmpty(errorMessage);
        }

        protected override void OnOkCommandExecute()
        {
            //Save and close the current observation attribute page
            //Transition to next in list, or if empty, previous page

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
                string msg = Task.ActiveObservation.ValidateBeforeSave();
                if (string.IsNullOrEmpty(msg))
                    msg = "One or more bird groups are invalid.";
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(msg, "Save Failed");
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
                NewObservationCommandExecute();
                return;
            }

            ////Tab handling
            //if (angleTextBox.IsFocused && e.Key == Key.Tab && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            //{
            //    e.Handled = true;
            //    if (queueDisplay.Visibility == Visibility.Visible)
            //        Keyboard.Focus(observationListView);
            //    else
            //        Keyboard.Focus(dataGrid);
            //    return;
            //}
            //if (dataGrid.IsKeyboardFocusWithin && e.Key == Key.Tab && e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            //{
            //    e.Handled = true;
            //    if (queueDisplay.Visibility == Visibility.Visible)
            //        Keyboard.Focus(observationListView);
            //    else
            //        Keyboard.Focus(angleTextBox);
            //    return;
            //}
            //if (observationListView.IsKeyboardFocusWithin && e.Key == Key.Tab && e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            //{
            //    e.Handled = true;
            //    Keyboard.Focus(angleTextBox);
            //    return;
            //}

            ////Bird Group data entry
            //if (dataGrid.IsFocused && e.Key != Key.Tab)
            //{
            //    //See if this key helps define a bird group
            //    e.Handled = true;
            //    DefineBirdGroup(e);
            //    return;
            //}

            base.OnKeyDown(e);
        }


        #endregion

        private void dockPanel_IsKeyboardFocusWithinChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            Keyboard.Focus(angleTextBox);
        }
    }

    public class RowDataInfoValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value,
                        CultureInfo cultureInfo)
        {
            var group = (BindingGroup)value;

            StringBuilder error = null;
            foreach (var item in group.Items)
            {
                // aggregate errors
                var info = item as IDataErrorInfo;
                if (info != null)
                {
                    if (!string.IsNullOrEmpty(info.Error))
                    {
                        if (error == null)
                            error = new StringBuilder();
                        error.Append((error.Length != 0 ? ", " : "") + info.Error);
                    }
                }
            }

            if (error != null)
                return new ValidationResult(false, error.ToString());

            return ValidationResult.ValidResult;
        }
    }
}
