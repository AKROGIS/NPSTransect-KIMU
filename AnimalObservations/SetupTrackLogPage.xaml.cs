using System;
using System.Diagnostics;
using System.Windows.Input;
using ESRI.ArcGIS.Mobile.Client;

namespace AnimalObservations
{
    public partial class SetupTrackLogPage
    {
        //These are used in XAML data binding so they must be public properties
        //One-Time, One-Way bindings:
        public CollectTrackLogTask Task { get; private set; }

        #region Constructor

        public SetupTrackLogPage()
        {
            //Call before InitializeComponent, so the onetime binding data is available
            InitializeData();

            InitializeComponent();

            Title = "Track Log Setup";
            Note = "Enter information common to pending observations";

            // page icon
            var uri = new Uri("pack://application:,,,/AnimalObservations;Component/duck-icon.png");
            ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);

            // back button
            BackCommands.Add(BackCommand);

            // forward button
            OkCommand.Text = "Start Recording";
            ForwardCommands.Add(OkCommand);

            //Setup desired keyboard behavior
            Focusable = true;

            //do some initialization each time the page is displayed
            Loaded += SetTrackLogAndRefreshNearbyTransects;
        }

        private void InitializeData()
        {
            Task = MobileApplication.Current.FindTask(typeof(CollectTrackLogTask)) as CollectTrackLogTask;
            //WeatherDomain = MobileUtilities.GetCodedValueDictionary<int>(TrackLog.FeatureLayer, "Weather");
            //VisibilityDomain = MobileUtilities.GetCodedValueDictionary<int>(TrackLog.FeatureLayer, "Visibility");
            //BeaufortDomain = MobileUtilities.GetCodedValueDictionary<int>(TrackLog.FeatureLayer, "Beaufort");
        }

        #endregion

        #region Page update (called from Loaded event)

        private void SetTrackLogAndRefreshNearbyTransects(object sender, System.Windows.RoutedEventArgs e)
        {
            //Update transect combobox (XAML bindings reads Task.NearbyTransects)
            Task.RefreshNearbyTransects();
            Transect nearestTransect = Task.NearbyTransects.GetNearest(Task.MostRecentLocation);

            Debug.Assert(nearestTransect != null, "Fail, No nearest transect on SetupTrackLogPage ");

            if (Task.DefaultTrackLog == null)
            {
                //TrackLog.CreateWith() may throw an exception.  If it does, there is no way to recover.
                //Let the exception invoke default behavior - alert user, write error log and exit app.
                Task.CurrentTrackLog = TrackLog.CreateWith(nearestTransect);
            }
            else
            {
                //TrackLog.CloneFrom() may throw an exception.  If it does, there is no way to recover.
                //Let the exception invoke default behavior - alert user, write error log and exit app.
                TrackLog newTracklog = TrackLog.CloneFrom(Task.DefaultTrackLog);
                //make sure the transect is one of those available in the combobox
                newTracklog.Transect = nearestTransect;
                Task.CurrentTrackLog = newTracklog;
            }

            //Set keyboard focus
            if (string.IsNullOrEmpty(vesselTextBox.Text))
                Keyboard.Focus(vesselTextBox);
            else
                Keyboard.Focus(weatherComboBox);
        }

        #endregion

        #region Page navigation overrides

        protected override void OnBackCommandExecute()
        {
            //Cancel editing tracklog attributes, quit task.

            //Delete the new uncommitted tracklog feature
            Task.CurrentTrackLog.DeleteFeature();
            Task.DefaultTrackLog = Task.CurrentTrackLog;
            Task.CurrentTrackLog = null;
            MobileApplication.Current.Transition(PreviousPage);
        }

        protected override void OnOkCommandExecute()
        {
            //Save tracklog attributes, and begin gps logging, transition to new page

            //At this point geometry is invalid, so we don't bother saving the tracklog.
            //The tracklog will save itself as soon as it has two points. 
            Task.StartRecording();
            Task.CurrentTrackLog.StartingTime = DateTime.Now;
            MobileApplication.Current.Transition(new RecordTrackLogPage());
        }
        
        #endregion

        #region Keyboard event overrides

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter ||
                (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.Control))
            {
                e.Handled = true;
                OnOkCommandExecute();
                return;
            }
            if (e.Key == Key.Escape ||
                (e.Key == Key.W && e.KeyboardDevice.Modifiers == ModifierKeys.Control))
            {
                e.Handled = true;
                OnBackCommandExecute();
                return;
            }
            if (onTransectCheckBox.IsFocused && e.Key == Key.Tab && e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                Keyboard.Focus(transectComboBox);
                return;
            }
            if (transectComboBox.IsFocused && e.KeyboardDevice.Modifiers == ModifierKeys.Shift && e.Key == Key.Tab)
            {
                e.Handled = true;
                Keyboard.Focus(onTransectCheckBox);
                return;
            }
            base.OnKeyDown(e);
        }

        #endregion
    }
}
