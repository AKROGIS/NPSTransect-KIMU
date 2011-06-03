using System;
using System.Diagnostics;
using System.Windows.Input;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;

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

            //FIXME - Task.CurrentGpsPoint may be null if this thread caught the main thread between states.
            //It only happens on occasion, but enough to be annoying.
            Debug.Assert(Task.CurrentGpsPoint != null, "Fail! Current GPS Point is null when recording an observation.");
            if (Task.CurrentGpsPoint == null)
                return;
            //TODO - add try/catch - CreateWith() may throw exceptions
            Observation observation = Observation.CreateWith(Task.CurrentGpsPoint);
            Task.AddObservationAsActive(observation);
            MobileApplication.Current.Transition(new EditObservationAttributesPage());
        }

        #endregion

        #region Mouse event overrides

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            //get the location of the mouse down event for use by the mouse up event
            //this method is NOT fired when The zoom in/out tool issues a mouse down
            _mouseDownPoint = e.GetPosition(this);
            _myMouseDown = true;
            base.OnMouseDown(e);
            //MouseUp += new MouseButtonEventHandler(RecordTrackLogPage_MouseUp);
        }
        private System.Windows.Point _mouseDownPoint;
        private bool _myMouseDown;

        //TODO drag image while panning

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            //this method IS fired when The zoom in/out tool issues a mouse up,
            //so we need to ignore it if we did not initiate the mouse down.
            if (!_myMouseDown)
            {
                base.OnMouseDown(e);
                Trace.TraceInformation("Ignoring Mouse up for zoom in/out");
                return;
            }
            _myMouseDown = false;

            System.Windows.Point mouseUpPoint = e.GetPosition(this);

            //Check to see if the mouseup location is substantially different than the mouse down location, if so then pan and be done
            int dx = Convert.ToInt32(mouseUpPoint.X - _mouseDownPoint.X);
            int dy = Convert.ToInt32(mouseUpPoint.Y - _mouseDownPoint.Y);
            bool moved = (dx < -2 || 2 < dx || dy < -2 || 2 < dy);

            // and not zooming in/out 
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
                Trace.TraceInformation("Mouse up found observation");
                Task.AddObservationAsActive(observation);
                MobileApplication.Current.Transition(new EditObservationAttributesPage());
                e.Handled = true;
                return;
            }
            Trace.TraceInformation("Mouse up passed to base");
            base.OnMouseDown(e);
        }

        //May return null if no observation is found
        private static Observation GetObservation(System.Drawing.Point drawingPoint)
        {
            Coordinate mapPoint = MobileApplication.Current.Map.ToMap(drawingPoint);
            var extents = new Envelope(mapPoint, MobileUtilities.SearchRadius * 2, MobileUtilities.SearchRadius * 2);
            Observation observation = null;
            try
            {
                //Try and find a bird group in this extent
                BirdGroup birdGroup = null;
                try
                {
                    birdGroup = BirdGroup.FromEnvelope(extents);
                }
                    //TODO - be more descriminating on exceptions, and re-throw where appropriate.
                catch (Exception ex)
                {
                    Trace.TraceError("Caught and ignored exception {0}", ex.Message);
                    //Allow birdGroup to default to null, i.e. no birdGroup found in extents
                }
                if (birdGroup != null)
                    observation =  birdGroup.Observation;

                //We did not find a birdgroup, so try and find an observation in this extent
                //Check to see if it is in our cache, if not, then load from database
                if (observation == null)
                    observation = Observation.FromEnvelope(extents);
            }
                //TODO - be more descriminating on exceptions, and provide error message where appropriate.
            catch (Exception ex)
            {
                Trace.TraceError("Caught and ignored exception {0}", ex.Message);
                //Allow observation to default to null, i.e. no observation found in extents
            }
            return observation;
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
            var newTracklog = TrackLog.CloneFrom(Task.CurrentTrackLog);
            newTracklog.Beaufort = (int)beaufortComboBox.SelectedValue;
            Task.StopRecording();
            Task.CurrentTrackLog = newTracklog;
            Task.StartRecording();
        }

        private void weatherComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var newTracklog = TrackLog.CloneFrom(Task.CurrentTrackLog);
            newTracklog.Weather = (int)weatherComboBox.SelectedValue;
            Task.StopRecording();
            Task.CurrentTrackLog = newTracklog;
            Task.StartRecording();
        }

        private void visibilityComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var newTracklog = TrackLog.CloneFrom(Task.CurrentTrackLog);
            newTracklog.Visibility = (int)visibilityComboBox.SelectedValue;
            Task.StopRecording();
            Task.CurrentTrackLog = newTracklog;
            Task.StartRecording();
        }

        private void onTransectCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            var newTracklog = TrackLog.CloneFrom(Task.CurrentTrackLog);
            newTracklog.OnTransect = onTransectCheckBox.IsChecked;
            Task.StopRecording();
            Task.CurrentTrackLog = newTracklog;
            Task.StartRecording();
        }

        #endregion
    }
}
