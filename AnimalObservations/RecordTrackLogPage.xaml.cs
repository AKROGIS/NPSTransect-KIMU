using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;

//TODO drag image while panning

namespace AnimalObservations
{
    public partial class RecordTrackLogPage
    {
        //These are used in XAML data binding so they must be public properties
        //One-Time, One-Way bindings:
        public CollectTrackLogTask Task { get; private set; }

        //private readonly CollectTrackLogTask Task;
        private readonly TrackLog _trackLog;

        #region Constructor

        public RecordTrackLogPage()
        {
            Task = (CollectTrackLogTask)MobileApplication.Current.FindTask(typeof(CollectTrackLogTask));
            _trackLog = Task.CurrentTrackLog;

            InitializeComponent();


            //We aren't using bindings because when a value changes we need to stop recording, change, start recording
            //And the object being changed (tracklog) know s nothing about stoping and starting recording.
            weatherComboBox.SelectedValue = Task.CurrentTrackLog.Weather;
            visibilityComboBox.SelectedValue = Task.CurrentTrackLog.Visibility;
            beaufortComboBox.SelectedValue = Task.CurrentTrackLog.Beaufort;
            onTransectCheckBox.IsChecked = Task.CurrentTrackLog.OnTransect;
            // wire up the event handlers after initializing the values to revent the initialization from firing the events.
            weatherComboBox.SelectionChanged += weatherComboBox_SelectionChanged;
            visibilityComboBox.SelectionChanged += visibilityComboBox_SelectionChanged;
            beaufortComboBox.SelectionChanged += beaufortComboBox_SelectionChanged;
            onTransectCheckBox.Checked += onTransectCheckBox_Changed;
            onTransectCheckBox.Unchecked += onTransectCheckBox_Changed;



            //Page Captions
            Title = "Transect " + _trackLog.Transect.Name;
            Note = "Capturing GPS points in track log";

            // page icon
            var uri = new Uri("pack://application:,,,/AnimalObservations;Component/duck-icon.png");
            ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);

            // back button
            CancelCommand.Text = "Stop Recording";
            BackCommands.Add(CancelCommand);

            // forward Button
            OkCommand.Text = "New Observation";
            ForwardCommands.Add(OkCommand);

            //Setup desired keyboard behavior
            Focusable = true;

            //do some initialization each time the page is displayed
            Loaded += (s, e) => Keyboard.Focus(this);

        }

        #endregion

        #region Page navigation overrides

        protected override void OnCancelCommandExecute()
        {
            Task.StopRecording();
            MobileApplication.Current.Transition(PreviousPage);
        }

        protected override bool CanExecuteOkCommand()
        {
            //nullity check is required because this override is called after OnCancelCommandExecute()
            if (Task.CurrentTrackLog == null)
                return false;
            return Task.CurrentTrackLog.OnTransect.GetValueOrDefault();
        }

        protected override void OnOkCommandExecute()
        {
            //Log observation point and open observation attribute page

            //We create the observation at the last GPS point.
            //We do not wait for the next GPS point to interpolate, because
            //1) Observations have a 1 to 1 relationship with GPS points,
            //2) we may never get a next GPS point,
            //3) There is a lag in communication between observers and recorders,
            //   therefore the actual point of observation is closer to the last GPS point
            //   than an interpolated point base on when this operation runs.

            // CreateWith() may throw exceptions, but that would be catastrophic, let the app handle it
            Observation observation = Observation.FromGpsPoint(Task.CurrentGpsPoint);
            Task.AddObservationAsActive(observation);
            MobileApplication.Current.Transition(new EditObservationAttributesPage());
        }

        #endregion

        #region Mouse event overrides

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            //get the location of the mouse down event for use by the MouseUp event
            //this method is NOT fired when The zoom in/out tool issues a MouseDown event
            _mouseDownPoint = e.GetPosition(this);
            _myMouseDown = true;
            base.OnMouseDown(e);
            //MouseUp += new MouseButtonEventHandler(RecordTrackLogPage_MouseUp);
        }
        private System.Windows.Point _mouseDownPoint;
        private bool _myMouseDown;

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            //this method is fired when The zoom in/out tool issues a MouseUp event,
            //so we need to ignore it if we did not initiate the mouse down.
            if (!_myMouseDown)
            {
                base.OnMouseUp(e);
                Trace.TraceInformation("Ignoring MouseUp for zoom in/out");
                return;
            }
            _myMouseDown = false;

            System.Windows.Point mouseUpPoint = e.GetPosition(this);

            //Check to see if the mouseup location is substantially different than the mouse down location, if so then pan and be done
            int dx = Convert.ToInt32(mouseUpPoint.X - _mouseDownPoint.X);
            int dy = Convert.ToInt32(mouseUpPoint.Y - _mouseDownPoint.Y);
            bool moved = (dx < -2 || 2 < dx || dy < -2 || 2 < dy);

            if (moved)
            {
                Trace.TraceInformation("Mouse up passed to pan");
                mapControl.Map.Pan(dx, dy);
                e.Handled = true;
                return;
            }

            //Check to see if there is an observation related to this location.  If so - edit it, then be done
            var drawingPoint = new System.Drawing.Point(Convert.ToInt32(mouseUpPoint.X), Convert.ToInt32(mouseUpPoint.Y));
            Observation observation = GetObservation(drawingPoint);
            if (observation != null)
            {
                //Trace.TraceInformation("Mouse up found observation");

                var dlg = new SelectionDialog();
                dlg.ShowDialog();

                if (dlg.Action == SelectionAction.Delete)
                {
                    //Trace.TraceInformation("Delete");
                    observation.Delete();
                    e.Handled = true;
                }
                if (dlg.Action == SelectionAction.Edit)
                {
                    //Trace.TraceInformation("Edit");
                    Task.AddObservationAsActive(observation);
                    MobileApplication.Current.Transition(new EditObservationAttributesPage());
                    e.Handled = true;
                }
                return;
            }
            //Trace.TraceInformation("Mouse up passed to base");
            base.OnMouseUp(e);
        }

        //May return null if no observation is found
        private static Observation GetObservation(System.Drawing.Point drawingPoint)
        {
            //offset for track log information banner (map should do this.  ESRI error??)
            drawingPoint.Y = drawingPoint.Y - 32;
            Coordinate mapPoint = MobileApplication.Current.Map.ToMap(drawingPoint);
            double sideLength = 2 * MobileUtilities.SearchRadius * MobileApplication.Current.Map.Scale / 1000;
            var extents = new Envelope(mapPoint, sideLength, sideLength);

            //Exceptions may get thrown by DB access in xx.FromEnvelope(), let the app handle them
            Observation observation = null;
            //Try and find a bird group in this extent
            BirdGroupFeature birdGroupFeature = BirdGroupFeature.FromEnvelope(extents);
            if (birdGroupFeature != null)
                observation =  birdGroupFeature.Observation;

            //If we didn't get a birdgroup, search for observations in this extent
            return observation ?? Observation.FromEnvelope(extents);
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
                OnCancelCommandExecute();
                return;
            }
            if (onTransectCheckBox.IsFocused && e.Key == Key.Tab && e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                Keyboard.Focus(weatherComboBox);
                return;
            }
            if (weatherComboBox.IsFocused && e.KeyboardDevice.Modifiers == ModifierKeys.Shift && e.Key == Key.Tab)
            {
                e.Handled = true;
                Keyboard.Focus(onTransectCheckBox);
                return;
            }
            base.OnKeyDown(e);
        }
        #endregion

        #region UI Events

        private void beaufortComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var newTracklog = TrackLog.FromTrackLog(Task.CurrentTrackLog);
            newTracklog.Beaufort = (int)beaufortComboBox.SelectedValue;
            Task.StopRecording();
            Task.CurrentTrackLog = newTracklog;
            Task.StartRecording();
        }

        private void weatherComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var newTracklog = TrackLog.FromTrackLog(Task.CurrentTrackLog);
            newTracklog.Weather = (int)weatherComboBox.SelectedValue;
            Task.StopRecording();
            Task.CurrentTrackLog = newTracklog;
            Task.StartRecording();
        }

        private void visibilityComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var newTracklog = TrackLog.FromTrackLog(Task.CurrentTrackLog);
            newTracklog.Visibility = (int)visibilityComboBox.SelectedValue;
            Task.StopRecording();
            Task.CurrentTrackLog = newTracklog;
            Task.StartRecording();
        }

        private void onTransectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var newTracklog = TrackLog.FromTrackLog(Task.CurrentTrackLog);
            newTracklog.OnTransect = onTransectCheckBox.IsChecked;
            Task.StopRecording();
            Task.CurrentTrackLog = newTracklog;
            Task.StartRecording();
        }

        #endregion
    }
}
