using System;
using System.Diagnostics;
using System.Windows.Controls;
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
            Debug.Assert(Task != null,
                "Fail!, Task is null in EditObservationAttributesPage");
            Debug.Assert(Task.ActiveObservation != null,
                "Fail!, Task.ActiveObservation is null in EditObservationAttributesPage");
            //FIXME - prove the above assertions are true for valid code and any data/userinput or add error checking

            //TODO: Consider using the feature attributes for handy property binding to tooltip, validation, etc.
            //FeatureAttribute Angle = Task.ActiveObservation.Feature.GetEditableAttributes()[0];

        }

        #endregion

        #region Defining Bird Groups

        private BirdGroup2 _birdGroupInProgress = new BirdGroup2();

        private void DefineBirdGroup(KeyEventArgs e)
        {
            string keyString = (new KeyConverter()).ConvertToString(e.Key);
            if (string.IsNullOrEmpty(keyString))
                return;
            if (e.KeyboardDevice.Modifiers != ModifierKeys.None)
                return;
            Char keyChar = keyString[0];
            if (BirdGroup2.RecognizeKey(keyChar) && _birdGroupInProgress.AcceptKey(keyChar))
            {
                if (_birdGroupInProgress.IsComplete)
                    CompleteBirdGroup();
            }
            else
            {
                if (_birdGroupInProgress.IsValid)
                    CompleteBirdGroup();
                else
                    _birdGroupInProgress.Reset();
            }
        }

        private void CompleteBirdGroup()
        {
            Task.ActiveObservation.BirdGroups.Add(_birdGroupInProgress);
            _birdGroupInProgress = new BirdGroup2();
        }

        #endregion

        #region Page navigation overrides

        protected override void OnCancelCommandExecute()
        {
            //Discard this observation (if existing, abandon changes; if new, delete new (unsaved) feature)
            //Transition to next in list, or if empty, previous page

            //FIXME - Are we editing an existing observation - discard changes, but do not delete observation
            //FIXME - if we are creating a new observation - delete newly create record.
            //FIXME - if the observation has been saved (during creation?) then it should be deleted
            Task.RemoveObservation(Task.ActiveObservation);
            if (Task.ActiveObservation == null)
                MobileApplication.Current.Transition(PreviousPage);
        }

        void NewObservationCommandExecute()
        {
            //Log observation point and add observation attribute page to the queue, do not change pages

            //CreateWith() may throw exceptions, but that would be catastrophic, so let the app handle it.
            //Only use one of the following based on prefered behavior
            Task.AddObservationAsActive(Observation.CreateWith(Task.CurrentGpsPoint));
            //Task.AddObservationAsInactive(Observation.CreateWith(Task.CurrentGpsPoint));
            //If the new observation is the active observation, set focus on Angle Box; don't change focus if AddObservationAsInactive
            Keyboard.Focus(angleTextBox);
        }

        protected override bool CanExecuteOkCommand()
        {
            //nullity check is required, because this override is called when the page is closing
            if (Task.ActiveObservation == null)
                return false;
            var angleError = (bool)angleTextBox.GetValue(Validation.HasErrorProperty);
            var distanceError = (bool)distanceTextBox.GetValue(Validation.HasErrorProperty);
            bool birdsError = Task.ActiveObservation.BirdGroups.Count < 1;
            return !angleError && !distanceError && !birdsError;
        }

        protected override void OnOkCommandExecute()
        {
            //Save and close the current observation attribute page
            //Transition to next in list, or if empty, previous page

            string validationMessage = Task.ActiveObservation.ValidateBeforeSave();
            if (!string.IsNullOrEmpty(validationMessage))
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(validationMessage, "Incomplete Observation");
                return;
               
            }

            bool saved;
            try
            {
                saved = Task.ActiveObservation.Save();
            }
            catch (Exception ex)
            {
                //TODO provide better options to user on how to proceed if save failed
                Trace.TraceError("Error saving observation/bird groups. " + ex);
                saved = false;
            }
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
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Error saving observation/bird groups", "Save Failed");
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

            //FIXME capture the delete event in the data grid to properly dispose of the birdgroup. 
            //private void RemoveButton_Click(object sender, RoutedEventArgs e)
            //{
            //    if (gridView.SelectedIndex != -1)
            //    {
            //        BirdGroup2 bird = Task.ActiveObservation.BirdGroups[gridView.SelectedIndex];
            //        Task.ActiveObservation.BirdGroups.RemoveAt(gridView.SelectedIndex);
            //        bird.Delete();
            //    }
            //}

            base.OnKeyDown(e);
        }


        #endregion

        private void dockPanel_IsKeyboardFocusWithinChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            Keyboard.Focus(angleTextBox);
        }
    }
}
